using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class PartnerService
{
    private readonly ModuleActivationOptions _opts;
    public PartnerService(ModuleActivationOptions opts) => _opts = opts;

    private (InvestProDbContext db, IRepository<Partner> repo) Open()
    {
        var db = (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;
        var repo = new EfRepository<Partner>(db);
        return (db, repo);
    }

    public async Task<List<Partner>> GetAllAsync(CancellationToken ct = default, bool includeDeleted = false)
    {
        var (db, repo) = Open();
        await using (db)
            return (await repo.FindAsync(x => true, ct, includeDeleted: includeDeleted))
                .OrderBy(x => x.Name).ToList();
    }

    public async Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db) return await repo.GetByIdAsync(id, ct);
    }

    /// <summary>
    /// Resolve which Partner record (if any) belongs to the given app user.
    /// Used by close/reopen/approval decision endpoints to bind the caller's
    /// identity to a partner row before recording a vote — defeats the
    /// "submit a decision as someone else" IDOR by ignoring the PartnerId
    /// the form supplies.
    /// </summary>
    public async Task<Partner?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var (db, _) = Open();
        await using (db)
            return await db.Partners
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Status != EntityStatus.Deleted, ct);
    }

    public async Task<Partner> CreateAsync(Partner model, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            if (model.UserId == Guid.Empty) model.UserId = null;
            await repo.AddAsync(model, ct);
            await db.SaveChangesAsync(ct);
        }
        return model;
    }

    public async Task<bool> UpdateAsync(Guid id, Partner input, CancellationToken ct = default)
    {
        var (db, repo) = Open();
        await using (db)
        {
            var row = await repo.GetByIdAsync(id, ct);
            if (row is null) return false;
            row.Name            = input.Name.Trim();
            row.Nid             = input.Nid?.Trim();
            row.Phone           = input.Phone?.Trim();
            row.Email           = input.Email?.Trim();
            row.Address         = input.Address?.Trim();
            row.BankName        = input.BankName?.Trim();
            row.BankAccountNo   = input.BankAccountNo?.Trim();
            row.MfsProvider     = input.MfsProvider?.Trim();
            row.MfsNumber       = input.MfsNumber?.Trim();
            row.NomineeName     = input.NomineeName?.Trim();
            row.NomineePhone    = input.NomineePhone?.Trim();
            row.NomineeRelation = input.NomineeRelation?.Trim();
            row.Notes           = input.Notes?.Trim();
            row.IsActive        = input.IsActive;
            row.UserId          = input.UserId == Guid.Empty ? null : input.UserId;
            await repo.UpdateAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }

    public async Task<Partner?> DeleteAsync(Guid id, CancellationToken ct = default)
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
