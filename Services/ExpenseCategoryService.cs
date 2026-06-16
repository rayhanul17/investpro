using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class ExpenseCategoryService
{
    private readonly ModuleActivationOptions _opts;
    public ExpenseCategoryService(ModuleActivationOptions opts) => _opts = opts;

    private (InvestProDbContext db, IRepository<ExpenseCategory> repo) Open()
    {
        var db = (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;
        var repo = new EfRepository<ExpenseCategory>(db);
        return (db, repo);
    }

    public async Task<List<ExpenseCategory>> GetAllAsync(CancellationToken ct = default, bool includeDeleted = false)
    {
        var (db, repo) = Open();
        await using (db)
            return (await repo.FindAsync(x => true, ct, includeDeleted: includeDeleted))
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
    }

    public async Task<ExpenseCategory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db) return await repo.GetByIdAsync(id, ct);
    }

    public async Task<ExpenseCategory> CreateAsync(ExpenseCategory model, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            model.IsSystem = false; // user-created can never be system
            await repo.AddAsync(model, ct);
            await db.SaveChangesAsync(ct);
        }
        return model;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string? description, bool isActive, int sortOrder, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return false;
            row.Name        = name.Trim();
            row.Description = description?.Trim();
            row.IsActive    = isActive;
            row.SortOrder   = sortOrder;
            await repo.UpdateAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }

    public async Task<(bool ok, string? error, ExpenseCategory? deleted)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return (false, "Not found.", null);
            if (row.IsSystem) return (false, "System categories cannot be deleted. Deactivate instead.", null);
            await repo.SoftDeleteAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return (true, null, row);
        }
    }
}
