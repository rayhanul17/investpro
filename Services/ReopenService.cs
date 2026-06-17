using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Orchestrates the Closed -> Active "reopen" flow. Owns no entities
/// directly — composes <see cref="ReopenRequestService"/>,
/// <see cref="InvestmentSnapshotService"/>, and <see cref="InvestmentPartnerService"/>
/// inside a single DbContext transaction.
///
/// <para>
/// All partners must approve to reopen. The current Active snapshot stays
/// in the database — it is only demoted to Superseded at the moment the
/// reclose generates the next snapshot. This keeps the audit trail intact
/// even if the reopen is approved but the reclose never happens.
/// </para>
/// </summary>
[FcmsScoped]
public class ReopenService
{
    private readonly ModuleActivationOptions _opts;
    private readonly ReopenRequestService _requests;
    private readonly InvestmentSnapshotService _snapshots;
    private readonly InvestmentPartnerService _partners;

    public ReopenService(
        ModuleActivationOptions opts,
        ReopenRequestService requests,
        InvestmentSnapshotService snapshots,
        InvestmentPartnerService partners)
    {
        _opts = opts;
        _requests = requests;
        _snapshots = snapshots;
        _partners = partners;
    }

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    public async Task<(bool ok, string? error, ReopenRequest? request)> RequestReopenAsync(
        Guid investmentId, Guid initiatedByUserId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            return (false, "A reason of at least 5 characters is required.", null);

        await using var db = OpenDb();
        var inv = await db.Investments.FirstOrDefaultAsync(x => x.Id == investmentId, ct);
        if (inv is null) return (false, "Investment not found.", null);
        if (inv.LifecycleStatus != InvestmentLifecycle.Closed)
            return (false, $"Reopen is only available for Closed investments (current: {inv.LifecycleStatus}).", null);

        if (await _requests.HasPendingForInvestmentOnContextAsync(db, investmentId, ct))
            return (false, "A reopen request is already pending for this investment.", null);

        var currentSnap = await _snapshots.GetActiveByInvestmentOnContextAsync(db, investmentId, ct);
        if (currentSnap is null) return (false, "No active snapshot to reopen.", null);

        var partnerIds = await _partners.GetPartnerIdsForInvestmentOnContextAsync(db, investmentId, ct);

        var req = new ReopenRequest
        {
            Id = Guid.NewGuid(),
            InvestmentId = investmentId,
            CurrentSnapshotId = currentSnap.Id,
            InitiatedByUserId = initiatedByUserId,
            InitiatedAt = DateTime.UtcNow,
            Reason = reason.Trim(),
            RequestStatus = ReopenRequestStatus.Pending,
        };
        _requests.StageRequestOnContext(db, req);

        foreach (var pid in partnerIds)
        {
            _requests.StageApprovalOnContext(db, new ReopenApproval
            {
                Id = Guid.NewGuid(),
                ReopenRequestId = req.Id,
                PartnerId = pid,
                Decision = DecisionKind.Pending,
            });
        }

        await db.SaveChangesAsync(ct);
        return (true, null, req);
    }

    public Task<List<ReopenRequest>> GetByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
        => _requests.GetByInvestmentAsync(investmentId, ct);

    public Task<ReopenRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _requests.GetByIdAsync(id, ct);

    public Task<bool> HasApprovedForInvestmentAsync(Guid investmentId, CancellationToken ct = default)
        => _requests.HasApprovedForInvestmentAsync(investmentId, ct);

    public async Task<(bool ok, string? error, ReopenRequestStatus finalStatus, bool transitioned)> DecideAsync(
        Guid requestId, Guid partnerId, DecisionKind decision, string? comment, CancellationToken ct = default)
    {
        if (decision == DecisionKind.Pending)
            return (false, "Pending is not a valid decision.", ReopenRequestStatus.Pending, false);

        await using var db = OpenDb();
        var req = await _requests.GetByIdOnContextAsync(db, requestId, ct);
        if (req is null) return (false, "Reopen request not found.", ReopenRequestStatus.Pending, false);
        if (req.RequestStatus != ReopenRequestStatus.Pending)
            return (false, $"Request is already {req.RequestStatus}.", req.RequestStatus, false);

        var slot = req.Approvals.FirstOrDefault(a => a.PartnerId == partnerId);
        if (slot is null) return (false, "You are not a partner on this investment.", req.RequestStatus, false);
        if (slot.Decision != DecisionKind.Pending)
            return (false, "You have already decided.", req.RequestStatus, false);

        slot.Decision = decision;
        slot.DecidedAt = DateTime.UtcNow;
        slot.Comment = comment?.Trim();

        if (decision == DecisionKind.Rejected)
        {
            req.RequestStatus = ReopenRequestStatus.Rejected;
            req.FinalizedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return (true, null, req.RequestStatus, false);
        }

        var pendingLeft = req.Approvals.Count(a => a.Decision == DecisionKind.Pending);
        if (pendingLeft > 0)
        {
            await db.SaveChangesAsync(ct);
            return (true, null, req.RequestStatus, false);
        }

        // All partners approved — perform the transition.
        req.RequestStatus = ReopenRequestStatus.Approved;
        req.FinalizedAt = DateTime.UtcNow;

        var inv = await db.Investments.FirstAsync(x => x.Id == req.InvestmentId, ct);
        inv.LifecycleStatus = InvestmentLifecycle.Active;
        inv.ClosedAt = null;

        // Stamp the snapshot's superseded reason now (the timestamp + status
        // flip are set later, at the moment the reclose generates v2).
        await _snapshots.StampSupersededReasonOnContextAsync(db, req.CurrentSnapshotId, req.Reason, ct);

        await db.SaveChangesAsync(ct);
        return (true, null, req.RequestStatus, true);
    }
}
