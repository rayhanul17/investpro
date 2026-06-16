using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class ApprovalConfigService
{
    private readonly ModuleActivationOptions _opts;
    public ApprovalConfigService(ModuleActivationOptions opts) => _opts = opts;

    private (InvestProDbContext db, IRepository<ApprovalConfig> repo) Open()
    {
        var db = (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;
        var repo = new EfRepository<ApprovalConfig>(db);
        return (db, repo);
    }

    public async Task<List<ApprovalConfig>> GetAllAsync(CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
            return (await repo.FindAsync(x => true, ct))
                .OrderBy(x => x.LedgerType).ToList();
    }

    public async Task<bool> UpdateAsync(Guid id, decimal autoBelow, decimal reqAbove, decimal allAbove, ApproverRole role, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return false;
            row.AutoApproveBelow        = autoBelow;
            row.RequireApprovalAbove    = reqAbove;
            row.RequireAllPartnersAbove = allAbove;
            row.ApproverRole            = role;
            await repo.UpdateAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
