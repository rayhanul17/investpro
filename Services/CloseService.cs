using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Orchestrates the lifecycle transition Active -> Closing -> Closed and the
/// snapshot it produces. Snapshot rows are written once and never updated —
/// edits to investments, partners, or ledger entries after close cannot
/// shift these numbers. The math itself lives in <see cref="CalculationHelper"/>
/// so each formula stays single-purpose and individually verifiable.
/// </summary>
[FcmsScoped]
public class CloseService
{
    private readonly ModuleActivationOptions _opts;
    public CloseService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    /// <summary>
    /// Start the close workflow. Moves the investment to Closing, creates a
    /// CloseRequest, and seeds one Pending CloseApproval row per partner.
    /// Snapshot generation only fires when every partner approves.
    /// </summary>
    public async Task<(bool ok, string? error, CloseRequest? request)> RequestCloseAsync(Guid investmentId, Guid initiatedByUserId, string? notes, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var inv = await db.Investments.FirstOrDefaultAsync(x => x.Id == investmentId, ct);
        if (inv is null) return (false, "Investment not found.", null);
        if (inv.LifecycleStatus != InvestmentLifecycle.Active)
            return (false, $"Only Active investments can be closed (current state: {inv.LifecycleStatus}).", null);

        var openRequest = await db.CloseRequests
            .AnyAsync(r => r.InvestmentId == investmentId && r.RequestStatus == CloseRequestStatus.Pending && r.Status != EntityStatus.Deleted, ct);
        if (openRequest) return (false, "A close request is already pending for this investment.", null);

        var partners = await db.InvestmentPartners
            .Where(p => p.InvestmentId == investmentId && p.Status != EntityStatus.Deleted)
            .Select(p => p.PartnerId)
            .ToListAsync(ct);
        if (partners.Count == 0) return (false, "Investment has no partner contracts to approve closing.", null);

        var req = new CloseRequest
        {
            Id = Guid.NewGuid(),
            InvestmentId = investmentId,
            InitiatedByUserId = initiatedByUserId,
            InitiatedAt = DateTime.UtcNow,
            RequestStatus = CloseRequestStatus.Pending,
            Notes = notes?.Trim(),
        };
        db.CloseRequests.Add(req);

        foreach (var pid in partners)
        {
            db.CloseApprovals.Add(new CloseApproval
            {
                Id = Guid.NewGuid(),
                CloseRequestId = req.Id,
                PartnerId = pid,
                Decision = DecisionKind.Pending,
            });
        }

        inv.LifecycleStatus = InvestmentLifecycle.Closing;
        await db.SaveChangesAsync(ct);
        return (true, null, req);
    }

    public async Task<List<CloseRequest>> GetByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.CloseRequests
            .Include(r => r.Approvals)
            .Where(r => r.InvestmentId == investmentId && r.Status != EntityStatus.Deleted)
            .OrderByDescending(r => r.InitiatedAt)
            .ToListAsync(ct);
    }

    public async Task<CloseRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.CloseRequests
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>
    /// Record one partner's decision on a pending close request.
    /// <list type="bullet">
    /// <item>Reject: closes the request, rolls Investment back to Active.</item>
    /// <item>All approve: closes the request, generates the snapshot,
    ///   transitions Investment to Closed.</item>
    /// </list>
    /// </summary>
    public async Task<(bool ok, string? error, CloseRequestStatus finalStatus, Guid? snapshotId)> DecideAsync(
        Guid closeRequestId, Guid partnerId, DecisionKind decision, string? comment, Guid? userId, CancellationToken ct = default)
    {
        if (decision == DecisionKind.Pending) return (false, "Pending is not a valid decision.", CloseRequestStatus.Pending, null);

        await using var db = OpenDb();
        var req = await db.CloseRequests
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == closeRequestId, ct);
        if (req is null) return (false, "Close request not found.", CloseRequestStatus.Pending, null);
        if (req.RequestStatus != CloseRequestStatus.Pending)
            return (false, $"Close request is already {req.RequestStatus}.", req.RequestStatus, null);

        var slot = req.Approvals.FirstOrDefault(a => a.PartnerId == partnerId);
        if (slot is null) return (false, "You are not a partner on this investment.", req.RequestStatus, null);
        if (slot.Decision != DecisionKind.Pending) return (false, "You have already decided.", req.RequestStatus, null);

        slot.Decision = decision;
        slot.DecidedAt = DateTime.UtcNow;
        slot.Comment = comment?.Trim();

        var inv = await db.Investments.FirstAsync(x => x.Id == req.InvestmentId, ct);

        if (decision == DecisionKind.Rejected)
        {
            req.RequestStatus = CloseRequestStatus.Rejected;
            req.FinalizedAt = DateTime.UtcNow;
            inv.LifecycleStatus = InvestmentLifecycle.Active;
            await db.SaveChangesAsync(ct);
            return (true, null, req.RequestStatus, null);
        }

        var pendingLeft = req.Approvals.Count(a => a.Decision == DecisionKind.Pending);
        if (pendingLeft > 0)
        {
            await db.SaveChangesAsync(ct);
            return (true, null, req.RequestStatus, null);
        }

        // All partners approved — finalise + snapshot.
        req.RequestStatus = CloseRequestStatus.Approved;
        req.FinalizedAt = DateTime.UtcNow;
        inv.LifecycleStatus = InvestmentLifecycle.Closed;
        inv.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var snapshotId = await GenerateSnapshotAsync(db, inv, userId, ct);
        return (true, null, req.RequestStatus, snapshotId);
    }

    /// <summary>
    /// Build the immutable snapshot for an investment. Walks each input
    /// through CalculationHelper one step at a time so the audit trail is
    /// linear and easy to step through in the debugger.
    /// </summary>
    private static async Task<Guid> GenerateSnapshotAsync(InvestProDbContext db, Investment inv, Guid? userId, CancellationToken ct)
    {
        // ── 1. Pull approved-only ledger rows ───────────────────────────
        var caps = await db.CapitalContributions
            .Where(x => x.InvestmentId == inv.Id && x.Status != EntityStatus.Deleted)
            .ToListAsync(ct);
        var labs = await db.LaborContributions
            .Where(x => x.InvestmentId == inv.Id && x.Status != EntityStatus.Deleted)
            .ToListAsync(ct);
        var exps = await db.Expenses
            .Where(x => x.InvestmentId == inv.Id && x.Status != EntityStatus.Deleted)
            .ToListAsync(ct);
        var revs = await db.Revenues
            .Where(x => x.InvestmentId == inv.Id && x.Status != EntityStatus.Deleted)
            .ToListAsync(ct);

        var contracts = await db.InvestmentPartners
            .Include(c => c.Partner)
            .Where(c => c.InvestmentId == inv.Id && c.Status != EntityStatus.Deleted)
            .ToListAsync(ct);

        // ── 1.5. Previous snapshot (if this is a reclose after reopen) ──
        // The most recent Active snapshot for this investment becomes the
        // baseline for the per-partner adjustment delta + gets stamped
        // Superseded once the new snapshot is built.
        var previousSnapshot = await db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.InvestmentId == inv.Id
                                      && s.SnapshotStatus == SnapshotStatus.Active
                                      && s.Status != EntityStatus.Deleted, ct);

        int version = (previousSnapshot?.Version ?? 0) + 1;

        // ── 2. Totals via CalculationHelper ─────────────────────────────
        var grossRevenue    = CalculationHelper.CalcGrossRevenue(revs);
        var grossExpense    = CalculationHelper.CalcGrossExpense(exps);
        var totalCapital    = CalculationHelper.CalcTotalCapital(caps);
        var totalLaborValue = CalculationHelper.CalcTotalLaborValue(labs);
        var netPL           = CalculationHelper.CalcNetPL(grossRevenue, grossExpense);

        var snap = new InvestmentSnapshot
        {
            Id = Guid.NewGuid(),
            InvestmentId = inv.Id,
            InvestmentCode = inv.Code,
            InvestmentName = inv.Name,
            InvestmentStartDate = inv.StartDate,
            ClosedAt = inv.ClosedAt ?? DateTime.UtcNow,
            ClosedByUserId = userId,
            ClosedByUserName = null, // controller can backfill after the call
            GrossRevenue = grossRevenue,
            GrossExpense = grossExpense,
            NetPL = netPL,
            TotalCapital = totalCapital,
            TotalLaborValue = totalLaborValue,
            PartnerCount = contracts.Count,
            Version = version,
            SnapshotStatus = SnapshotStatus.Active,
            PreviousSnapshotId = previousSnapshot?.Id,
        };
        db.InvestmentSnapshots.Add(snap);

        // ── 3. Per-partner breakdown ────────────────────────────────────
        var details = new List<SnapshotPartnerDetail>();
        decimal netLossAbs = netPL < 0m ? Math.Abs(netPL) : 0m;
        decimal netProfit  = netPL > 0m ? netPL : 0m;

        foreach (var c in contracts)
        {
            decimal partnerCapital = CalculationHelper.CalcTotalCapital(
                caps.Where(x => x.PartnerId == c.PartnerId));
            decimal partnerLabor   = CalculationHelper.CalcTotalLaborValue(
                labs.Where(x => x.PartnerId == c.PartnerId));

            decimal profitShare = CalculationHelper.CalcPartnerProfitShare(netProfit, c.ProfitSharePercent);
            decimal lossShare   = CalculationHelper.CalcPartnerLossShare(netLossAbs, c.LossSharePercent, c.ContractType);

            decimal withdrawals = 0m;
            decimal settlement  = CalculationHelper.CalcPartnerSettlement(partnerCapital, profitShare, lossShare, withdrawals);
            decimal zakatBase   = CalculationHelper.CalcZakatEligibleBase(partnerCapital, profitShare);

            // Reclose: find this partner's previous settlement to compute
            // the adjustment delta. First close: previous = 0, delta = full
            // settlement (matches the old behaviour exactly).
            decimal previousSettlement = previousSnapshot?
                .PartnerDetails
                .FirstOrDefault(pd => pd.PartnerId == c.PartnerId)
                ?.FinalSettlementAmount ?? 0m;
            decimal adjustment = CalculationHelper.CalcAdjustmentDelta(settlement, previousSettlement);

            var detail = new SnapshotPartnerDetail
            {
                Id = Guid.NewGuid(),
                SnapshotId = snap.Id,
                PartnerId = c.PartnerId,
                PartnerName = c.Partner?.Name ?? "—",
                PartnerNid = c.Partner?.Nid,
                PartnerPhone = c.Partner?.Phone,
                PartnerEmail = c.Partner?.Email,
                ContractTypeAtClose = c.ContractType,
                PartnerRoleAtClose = c.PartnerRole,
                CapitalContributed = partnerCapital,
                LaborValueContributed = partnerLabor,
                ProfitSharePercent = c.ProfitSharePercent,
                LossSharePercent = c.LossSharePercent,
                ProfitShareAmount = profitShare,
                LossShareAmount = lossShare,
                WithdrawalsDuringInvestment = withdrawals,
                FinalSettlementAmount = settlement,
                ZakatEligibleAmount = zakatBase,
                PreviousSettlementAmount = previousSettlement,
                AdjustmentAmount = adjustment,
            };
            details.Add(detail);
            db.SnapshotPartnerDetails.Add(detail);

            // Payout seeding:
            // - v1 close: seed Pending payout = full settlement (legacy shape).
            // - v2+ close: seed Pending adjustment payout = absolute Δ,
            //              direction Outgoing if Δ>0 (we owe partner more),
            //              Incoming if Δ<0 (partner owes back).
            //   The original headline payouts from v1 keep their own state
            //   (Paid / Pending) — they're not touched here.
            if (previousSnapshot is null)
            {
                if (settlement > 0m)
                {
                    db.Payouts.Add(new Payout
                    {
                        Id = Guid.NewGuid(),
                        SnapshotId = snap.Id,
                        PartnerDetailId = detail.Id,
                        InvestmentId = inv.Id,
                        PartnerId = c.PartnerId,
                        Amount = settlement,
                        Direction = PayoutDirection.Outgoing,
                        IsAdjustment = false,
                        PaymentMethod = PaymentMethod.Cash,
                        PaymentStatus = PayoutStatus.Pending,
                    });
                }
            }
            else if (adjustment != 0m)
            {
                db.Payouts.Add(new Payout
                {
                    Id = Guid.NewGuid(),
                    SnapshotId = snap.Id,
                    PartnerDetailId = detail.Id,
                    InvestmentId = inv.Id,
                    PartnerId = c.PartnerId,
                    Amount = Math.Abs(adjustment),
                    Direction = adjustment > 0m ? PayoutDirection.Outgoing : PayoutDirection.Incoming,
                    IsAdjustment = true,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentStatus = PayoutStatus.Pending,
                    Notes = $"Adjustment v{version} − v{(previousSnapshot?.Version ?? 0)}",
                });
            }
        }

        // ── 4. Tamper-detection checksum (computed AFTER all rows set) ──
        snap.Checksum = CalculationHelper.CalcChecksum(snap, details);

        // ── 5. Demote the previous snapshot (if any) ────────────────────
        if (previousSnapshot is not null)
        {
            previousSnapshot.SnapshotStatus = SnapshotStatus.Superseded;
            previousSnapshot.SupersededAt = DateTime.UtcNow;
            // SupersededReason is set by the reopen flow before this point;
            // here we just stamp the timestamp.
        }

        await db.SaveChangesAsync(ct);
        return snap.Id;
    }

    public async Task<InvestmentSnapshot?> GetSnapshotByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        // Pick the Active snapshot (post-reopen there may be Superseded
        // rows too — those open via /snapshot/{id} only, not the default
        // /snapshot landing).
        return await db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.InvestmentId == investmentId
                                      && s.SnapshotStatus == SnapshotStatus.Active
                                      && s.Status != EntityStatus.Deleted, ct);
    }

    /// <summary>Every snapshot for the investment, newest first. Used by the history viewer.</summary>
    public async Task<List<InvestmentSnapshot>> GetSnapshotHistoryAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentSnapshots
            .Where(s => s.InvestmentId == investmentId && s.Status != EntityStatus.Deleted)
            .OrderByDescending(s => s.Version)
            .ToListAsync(ct);
    }

    public async Task<InvestmentSnapshot?> GetSnapshotByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    /// <summary>Recompute the stored checksum and compare. Returns true if intact.</summary>
    public bool VerifyChecksum(InvestmentSnapshot snap)
    {
        var fresh = CalculationHelper.CalcChecksum(snap, snap.PartnerDetails);
        return string.Equals(fresh, snap.Checksum, StringComparison.OrdinalIgnoreCase);
    }
}
