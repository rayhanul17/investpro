using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Owns every read and write against close_requests and close_approvals.
/// CloseService composes this with InvestmentSnapshotService inside a single
/// DbContext transaction.
/// </summary>
[FcmsScoped]
public class CloseRequestService
{
    private readonly ModuleActivationOptions _opts;
    public CloseRequestService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    // ── Queries ─────────────────────────────────────────────────────────

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

    public Task<bool> HasPendingForInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        var db = OpenDb();
        try
        {
            return db.CloseRequests
                .AnyAsync(r => r.InvestmentId == investmentId
                               && r.RequestStatus == CloseRequestStatus.Pending
                               && r.Status != EntityStatus.Deleted, ct);
        }
        finally { db.Dispose(); }
    }

    // ── Shared-context writers ──────────────────────────────────────────

    public Task<bool> HasPendingForInvestmentOnContextAsync(InvestProDbContext db, Guid investmentId, CancellationToken ct = default)
        => db.CloseRequests
            .AnyAsync(r => r.InvestmentId == investmentId
                           && r.RequestStatus == CloseRequestStatus.Pending
                           && r.Status != EntityStatus.Deleted, ct);

    public Task<CloseRequest?> GetByIdOnContextAsync(InvestProDbContext db, Guid id, CancellationToken ct = default)
        => db.CloseRequests.Include(r => r.Approvals).FirstOrDefaultAsync(r => r.Id == id, ct);

    public void StageRequestOnContext(InvestProDbContext db, CloseRequest req) => db.CloseRequests.Add(req);
    public void StageApprovalOnContext(InvestProDbContext db, CloseApproval approval) => db.CloseApprovals.Add(approval);
}
