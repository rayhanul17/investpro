using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class ApprovalService
{
    private readonly ModuleActivationOptions _opts;
    public ApprovalService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    /// <summary>
    /// Looks at the configured thresholds for this ledger kind and returns
    /// which approval flow applies for a given amount. Used by the ledger
    /// services right after creating an entry to decide whether to auto-
    /// approve it inline or create a pending ApprovalRequest.
    /// </summary>
    public async Task<(ApproverMode mode, ApproverRole? role, ApprovalConfig? config)> ResolveModeAsync(LedgerKind kind, decimal amount, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var cfg = await db.ApprovalConfigs.FirstOrDefaultAsync(c => c.LedgerType == kind, ct);
        if (cfg is null) return (ApproverMode.Auto, null, null);

        if (cfg.RequireAllPartnersAbove > 0 && amount >= cfg.RequireAllPartnersAbove)
            return (ApproverMode.AllPartners, cfg.ApproverRole, cfg);
        if (cfg.RequireApprovalAbove > 0 && amount >= cfg.RequireApprovalAbove)
            return (ApproverMode.SingleApprover, cfg.ApproverRole, cfg);
        if (amount < cfg.AutoApproveBelow || cfg.AutoApproveBelow == 0)
            return (ApproverMode.Auto, cfg.ApproverRole, cfg);

        return (ApproverMode.SingleApprover, cfg.ApproverRole, cfg);
    }

    /// <summary>
    /// Creates an ApprovalRequest row for a freshly inserted ledger entry.
    /// For AllPartners mode, seeds one pending ApprovalDecision per active
    /// contracted partner — the request stays Pending until every partner
    /// approves. For SingleApprover, the controller's approver simply
    /// stamps the request directly.
    /// </summary>
    public async Task<ApprovalRequest?> CreateRequestAsync(Guid investmentId, LedgerKind kind, Guid entryId, decimal amount, ApproverMode mode, ApproverRole? role, CancellationToken ct = default)
    {
        if (mode == ApproverMode.Auto) return null;

        await using var db = OpenDb();
        var req = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            InvestmentId = investmentId,
            LedgerType = kind,
            LedgerEntryId = entryId,
            Amount = amount,
            RequiredApproverMode = mode,
            ApproverRole = role,
            RequestStatus = ApprovalRequestStatus.Pending,
            InitiatedAt = DateTime.UtcNow,
        };
        db.ApprovalRequests.Add(req);

        if (mode == ApproverMode.AllPartners)
        {
            var partners = await db.InvestmentPartners
                .Where(p => p.InvestmentId == investmentId && p.Status != EntityStatus.Deleted)
                .Select(p => p.PartnerId)
                .ToListAsync(ct);

            foreach (var pid in partners)
            {
                db.ApprovalDecisions.Add(new ApprovalDecision
                {
                    Id = Guid.NewGuid(),
                    ApprovalRequestId = req.Id,
                    PartnerId = pid,
                    Decision = DecisionKind.Pending,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<List<ApprovalRequest>> GetPendingForInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.ApprovalRequests
            .Where(r => r.InvestmentId == investmentId && r.RequestStatus == ApprovalRequestStatus.Pending && r.Status != EntityStatus.Deleted)
            .OrderBy(r => r.InitiatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<ApprovalRequest>> GetAllPendingAsync(CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.ApprovalRequests
            .Where(r => r.RequestStatus == ApprovalRequestStatus.Pending && r.Status != EntityStatus.Deleted)
            .OrderBy(r => r.InitiatedAt)
            .ToListAsync(ct);
    }

    public async Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.ApprovalRequests
            .Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<(bool ok, string? error, ApprovalRequestStatus finalStatus)> DecideAsync(Guid requestId, DecisionKind decision, Guid? partnerId, Guid? userId, string? comment, CancellationToken ct = default)
    {
        if (decision == DecisionKind.Pending) return (false, "Pending is not a valid decision.", ApprovalRequestStatus.Pending);

        await using var db = OpenDb();
        var req = await db.ApprovalRequests.Include(r => r.Decisions).FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null) return (false, "Request not found.", ApprovalRequestStatus.Pending);
        if (req.RequestStatus != ApprovalRequestStatus.Pending) return (false, $"Request is already {req.RequestStatus}.", req.RequestStatus);

        if (req.RequiredApproverMode == ApproverMode.SingleApprover)
        {
            db.ApprovalDecisions.Add(new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                ApprovalRequestId = req.Id,
                PartnerId = partnerId,
                UserId = userId,
                Decision = decision,
                DecidedAt = DateTime.UtcNow,
                Comment = comment?.Trim(),
            });
            req.RequestStatus = decision == DecisionKind.Approved
                ? ApprovalRequestStatus.Approved
                : ApprovalRequestStatus.Rejected;
            req.DecidedAt = DateTime.UtcNow;
        }
        else if (req.RequiredApproverMode == ApproverMode.AllPartners)
        {
            if (partnerId is null) return (false, "Partner id is required for all-partner approvals.", req.RequestStatus);
            var d = req.Decisions.FirstOrDefault(x => x.PartnerId == partnerId);
            if (d is null) return (false, "You are not in the partner list for this investment.", req.RequestStatus);
            if (d.Decision != DecisionKind.Pending) return (false, "You have already decided on this request.", req.RequestStatus);

            d.Decision = decision;
            d.UserId   = userId;
            d.DecidedAt = DateTime.UtcNow;
            d.Comment   = comment?.Trim();

            if (decision == DecisionKind.Rejected)
            {
                // One reject closes the request — no need to wait for the rest.
                req.RequestStatus = ApprovalRequestStatus.Rejected;
                req.DecidedAt = DateTime.UtcNow;
            }
            else
            {
                var pendingLeft = req.Decisions.Count(x => x.Decision == DecisionKind.Pending);
                if (pendingLeft == 0)
                {
                    req.RequestStatus = ApprovalRequestStatus.Approved;
                    req.DecidedAt = DateTime.UtcNow;
                }
            }
        }
        else
        {
            return (false, "Auto-approval cannot be decided manually.", req.RequestStatus);
        }

        await db.SaveChangesAsync(ct);
        await ReflectIntoLedgerEntryAsync(db, req, ct);
        return (true, null, req.RequestStatus);
    }

    /// <summary>
    /// Directly set the ApprovalStatus on a ledger row, bypassing the entry's
    /// own update flow (which doesn't touch this field). Used after creating
    /// an entry that requires approval — flips it from AutoApproved (default)
    /// to Pending so totals and badges line up.
    /// </summary>
    public async Task SetLedgerEntryStatusAsync(LedgerKind kind, Guid entryId, LedgerApprovalStatus status, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        switch (kind)
        {
            case LedgerKind.Capital:
                var c = await db.CapitalContributions.FirstOrDefaultAsync(x => x.Id == entryId, ct);
                if (c is not null) c.ApprovalStatus = status;
                break;
            case LedgerKind.Labor:
                var l = await db.LaborContributions.FirstOrDefaultAsync(x => x.Id == entryId, ct);
                if (l is not null) l.ApprovalStatus = status;
                break;
            case LedgerKind.Expense:
                var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == entryId, ct);
                if (e is not null) e.ApprovalStatus = status;
                break;
            case LedgerKind.Revenue:
                var r = await db.Revenues.FirstOrDefaultAsync(x => x.Id == entryId, ct);
                if (r is not null) r.ApprovalStatus = status;
                break;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task ReflectIntoLedgerEntryAsync(InvestProDbContext db, ApprovalRequest req, CancellationToken ct)
    {
        // If the request just landed on Approved/Rejected, push the matching
        // status onto the source ledger row so timeline/lists pick it up.
        LedgerApprovalStatus newStatus = req.RequestStatus switch
        {
            ApprovalRequestStatus.Approved => LedgerApprovalStatus.Approved,
            ApprovalRequestStatus.Rejected => LedgerApprovalStatus.Rejected,
            _ => LedgerApprovalStatus.Pending,
        };

        switch (req.LedgerType)
        {
            case LedgerKind.Capital:
                var c = await db.CapitalContributions.FirstOrDefaultAsync(x => x.Id == req.LedgerEntryId, ct);
                if (c is not null) c.ApprovalStatus = newStatus;
                break;
            case LedgerKind.Labor:
                var l = await db.LaborContributions.FirstOrDefaultAsync(x => x.Id == req.LedgerEntryId, ct);
                if (l is not null) l.ApprovalStatus = newStatus;
                break;
            case LedgerKind.Expense:
                var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == req.LedgerEntryId, ct);
                if (e is not null) e.ApprovalStatus = newStatus;
                break;
            case LedgerKind.Revenue:
                var r = await db.Revenues.FirstOrDefaultAsync(x => x.Id == req.LedgerEntryId, ct);
                if (r is not null) r.ApprovalStatus = newStatus;
                break;
        }
        await db.SaveChangesAsync(ct);
    }
}
