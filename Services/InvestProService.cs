using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Module-owned service. Mirrors host services (CategoryService etc.) by
/// depending on the framework's generic <see cref="IRepository{T}"/> and
/// <see cref="EfRepository{T}"/> abstractions — but because the entity lives
/// in this module's own <see cref="InvestProDbContext"/> (not host DI),
/// the service rebuilds the context + repository per request from
/// <see cref="ModuleActivationOptions"/>. The CRUD shape stays identical to
/// what a host author would write.
/// </summary>
[FcmsScoped]
public class InvestProService
{
    private readonly ModuleActivationOptions _opts;
    public InvestProService(ModuleActivationOptions opts) => _opts = opts;

    private (InvestProDbContext db, IRepository<InvestProItem> repo) Open()
    {
        var db = (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;
        var repo = new EfRepository<InvestProItem>(db);
        return (db, repo);
    }

    public async Task<List<InvestProItem>> GetAllAsync(
        CancellationToken ct = default,
        bool includeDeleted = false,
        bool includeInactive = true)
    {
        var (db, repo) = Open();
        await using (db)
            return (await repo.FindAsync(x => true, ct,
                        includeDeleted: includeDeleted,
                        includeInactive: includeInactive))
                .OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<InvestProItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db) return await repo.GetByIdAsync(id, ct);
    }

    public async Task<InvestProItem> CreateAsync(InvestProItem model, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            await repo.AddAsync(model, ct);
            await db.SaveChangesAsync(ct);
        }
        return model;
    }

    public async Task<bool> UpdateAsync(Guid id, string title, string description, bool isPublished, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return false;
            row.Title = title.Trim();
            row.Description = description?.Trim() ?? "";
            row.IsPublished = isPublished;
            await repo.UpdateAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }

    public async Task<InvestProItem?> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return null;
            await repo.SoftDeleteAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return row;
        }
    }
}
