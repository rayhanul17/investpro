using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Owns every read and write against investment_snapshots,
/// snapshot_partner_details, and payouts (the snapshot's settlement rows).
///
/// <para>
/// Two flavours of API:
/// <list type="bullet">
/// <item>Public Get*Async / Has*Async open their own DbContext.</item>
/// <item>Public Stage*OnContext + DemoteSupersedingOnContext + StampSupersededReasonOnContext
///   take an open <see cref="InvestProDbContext"/> so an orchestrator
///   (CloseService, ReopenService) can compose multiple entity-service
///   writes into one SaveChangesAsync transaction.</item>
/// </list>
/// </para>
/// </summary>
[FcmsScoped]
public class InvestmentSnapshotService
{
    private readonly ModuleActivationOptions _opts;
    public InvestmentSnapshotService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<InvestmentSnapshot?> GetActiveByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.InvestmentId == investmentId
                                      && s.SnapshotStatus == SnapshotStatus.Active
                                      && s.Status != EntityStatus.Deleted, ct);
    }

    public async Task<InvestmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<InvestmentSnapshot>> GetHistoryAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentSnapshots
            .Where(s => s.InvestmentId == investmentId && s.Status != EntityStatus.Deleted)
            .OrderByDescending(s => s.Version)
            .ToListAsync(ct);
    }

    // ── Shared-context writers (no SaveChanges) ─────────────────────────

    /// <summary>
    /// Load the current Active snapshot (with PartnerDetails) on the caller's
    /// DbContext so a reclose can compute per-partner adjustment deltas
    /// without opening a second connection.
    /// </summary>
    public Task<InvestmentSnapshot?> GetActiveByInvestmentOnContextAsync(InvestProDbContext db, Guid investmentId, CancellationToken ct = default)
        => db.InvestmentSnapshots
            .Include(s => s.PartnerDetails)
            .FirstOrDefaultAsync(s => s.InvestmentId == investmentId
                                      && s.SnapshotStatus == SnapshotStatus.Active
                                      && s.Status != EntityStatus.Deleted, ct);

    public void StageSnapshotOnContext(InvestProDbContext db, InvestmentSnapshot snap)
        => db.InvestmentSnapshots.Add(snap);

    public void StagePartnerDetailOnContext(InvestProDbContext db, SnapshotPartnerDetail detail)
        => db.SnapshotPartnerDetails.Add(detail);

    public void StagePayoutOnContext(InvestProDbContext db, Payout payout)
        => db.Payouts.Add(payout);

    /// <summary>
    /// Mark the supplied (previous) snapshot as Superseded. SupersededReason
    /// is expected to be set already (by the reopen flow); we only stamp
    /// SnapshotStatus + SupersededAt here.
    /// </summary>
    public void DemoteSupersedingOnContext(InvestmentSnapshot previous)
    {
        previous.SnapshotStatus = SnapshotStatus.Superseded;
        previous.SupersededAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Stamp the reopen reason onto the current Active snapshot. Loads the
    /// row on the caller's DbContext so it participates in the same save.
    /// </summary>
    public async Task<bool> StampSupersededReasonOnContextAsync(InvestProDbContext db, Guid snapshotId, string reason, CancellationToken ct = default)
    {
        var snap = await db.InvestmentSnapshots.FirstOrDefaultAsync(s => s.Id == snapshotId, ct);
        if (snap is null) return false;
        snap.SupersededReason = reason;
        return true;
    }

    /// <summary>Recompute the stored checksum and compare. Returns true if intact.</summary>
    public bool VerifyChecksum(InvestmentSnapshot snap)
    {
        var fresh = CalculationHelper.CalcChecksum(snap, snap.PartnerDetails);
        return string.Equals(fresh, snap.Checksum, StringComparison.OrdinalIgnoreCase);
    }
}
