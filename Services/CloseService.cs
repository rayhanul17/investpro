using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Orchestrates the lifecycle transition Active -> Closing -> Closed and the
/// snapshot it produces. Owns no entities directly — composes
/// <see cref="CloseRequestService"/>, <see cref="InvestmentSnapshotService"/>,
/// <see cref="InvestmentPartnerService"/>, and the four ledger services
/// inside a single DbContext transaction. The math itself lives in
/// <see cref="CalculationHelper"/> so each formula stays single-purpose.
/// Snapshot rows are written once and never updated — edits to investments,
/// partners, or ledger entries after close cannot shift these numbers.
/// </summary>
[FcmsScoped]
public class CloseService
{
    private readonly ModuleActivationOptions _opts;
    private readonly CloseRequestService _requests;
    private readonly InvestmentSnapshotService _snapshots;
    private readonly InvestmentPartnerService _partners;
    private readonly CapitalContributionService _capitals;
    private readonly LaborContributionService _labors;
    private readonly ExpenseEntryService _expenses;
    private readonly RevenueEntryService _revenues;

    public CloseService(
        ModuleActivationOptions opts,
        CloseRequestService requests,
        InvestmentSnapshotService snapshots,
        InvestmentPartnerService partners,
        CapitalContributionService capitals,
        LaborContributionService labors,
        ExpenseEntryService expenses,
        RevenueEntryService revenues)
    {
        _opts = opts;
        _requests = requests;
        _snapshots = snapshots;
        _partners = partners;
        _capitals = capitals;
        _labors = labors;
        _expenses = expenses;
        _revenues = revenues;
    }

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

        if (await _requests.HasPendingForInvestmentOnContextAsync(db, investmentId, ct))
            return (false, "A close request is already pending for this investment.", null);

        var partnerIds = await _partners.GetPartnerIdsForInvestmentOnContextAsync(db, investmentId, ct);
        if (partnerIds.Count == 0) return (false, "Investment has no partner contracts to approve closing.", null);

        var req = new CloseRequest
        {
            Id = Guid.NewGuid(),
            InvestmentId = investmentId,
            InitiatedByUserId = initiatedByUserId,
            InitiatedAt = DateTime.UtcNow,
            RequestStatus = CloseRequestStatus.Pending,
            Notes = notes?.Trim(),
        };
        _requests.StageRequestOnContext(db, req);

        foreach (var pid in partnerIds)
        {
            _requests.StageApprovalOnContext(db, new CloseApproval
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

    public Task<List<CloseRequest>> GetByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
        => _requests.GetByInvestmentAsync(investmentId, ct);

    public Task<CloseRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _requests.GetByIdAsync(id, ct);

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
        var req = await _requests.GetByIdOnContextAsync(db, closeRequestId, ct);
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

        // All partners approved — finalise + snapshot in the same transaction.
        req.RequestStatus = CloseRequestStatus.Approved;
        req.FinalizedAt = DateTime.UtcNow;
        inv.LifecycleStatus = InvestmentLifecycle.Closed;
        inv.ClosedAt = DateTime.UtcNow;

        var snapshotId = await GenerateSnapshotAsync(db, inv, userId, ct);
        await db.SaveChangesAsync(ct);
        return (true, null, req.RequestStatus, snapshotId);
    }

    /// <summary>
    /// Build the immutable snapshot for an investment. Walks each input
    /// through CalculationHelper one step at a time so the audit trail is
    /// linear and easy to step through in the debugger.
    /// Caller is responsible for SaveChangesAsync.
    /// </summary>
    private async Task<Guid> GenerateSnapshotAsync(InvestProDbContext db, Investment inv, Guid? userId, CancellationToken ct)
    {
        // ── 1. Pull approved-only ledger rows via each ledger service ───
        var caps = await _capitals.GetByInvestmentOnContextAsync(db, inv.Id, ct);
        var labs = await _labors.GetByInvestmentOnContextAsync(db, inv.Id, ct);
        var exps = await _expenses.GetByInvestmentOnContextAsync(db, inv.Id, ct);
        var revs = await _revenues.GetByInvestmentOnContextAsync(db, inv.Id, ct);
        var contracts = await _partners.GetByInvestmentOnContextAsync(db, inv.Id, ct);

        // ── 1.5. Previous Active snapshot (set on reclose after reopen) ─
        var previousSnapshot = await _snapshots.GetActiveByInvestmentOnContextAsync(db, inv.Id, ct);
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
        _snapshots.StageSnapshotOnContext(db, snap);

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
            _snapshots.StagePartnerDetailOnContext(db, detail);

            // Payout seeding:
            // - v1 close: seed Pending payout = full settlement (legacy shape).
            // - v2+ close: seed Pending adjustment payout = absolute Δ,
            //              direction Outgoing if Δ>0, Incoming if Δ<0.
            if (previousSnapshot is null)
            {
                if (settlement > 0m)
                {
                    _snapshots.StagePayoutOnContext(db, new Payout
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
                _snapshots.StagePayoutOnContext(db, new Payout
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
            _snapshots.DemoteSupersedingOnContext(previousSnapshot);

        return snap.Id;
    }

    // ── Snapshot proxy methods (preserve old CloseService surface) ──────
    // Older callers (SnapshotController, InvestmentController) still ask
    // CloseService for snapshots. These keep the public surface stable
    // while the storage actually lives in InvestmentSnapshotService.

    public Task<InvestmentSnapshot?> GetSnapshotByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
        => _snapshots.GetActiveByInvestmentAsync(investmentId, ct);

    public Task<List<InvestmentSnapshot>> GetSnapshotHistoryAsync(Guid investmentId, CancellationToken ct = default)
        => _snapshots.GetHistoryAsync(investmentId, ct);

    public Task<InvestmentSnapshot?> GetSnapshotByIdAsync(Guid id, CancellationToken ct = default)
        => _snapshots.GetByIdAsync(id, ct);

    public bool VerifyChecksum(InvestmentSnapshot snap) => _snapshots.VerifyChecksum(snap);
}
