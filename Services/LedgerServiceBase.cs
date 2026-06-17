using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;
using FlexCms.Framework.Modules;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// Shared CRUD shell for the four ledger services. Each concrete service
/// inherits this and supplies its own DbSet selector + entity-specific copy
/// logic. The framework forbids editing entries on closed investments — that
/// rule lives here so all four ledgers behave the same.
/// </summary>
public abstract class LedgerServiceBase<T> where T : LedgerEntryBase, new()
{
    protected readonly ModuleActivationOptions Opts;
    protected LedgerServiceBase(ModuleActivationOptions opts) => Opts = opts;

    protected InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(Opts.ConnectionString, Opts.Provider)!;

    protected abstract DbSet<T> Set(InvestProDbContext db);
    protected abstract LedgerKind Kind { get; }

    public virtual async Task<List<T>> GetByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await Set(db)
            .Include(x => x.Partner)
            .Where(x => x.InvestmentId == investmentId && x.Status != EntityStatus.Deleted)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.EntryDate)
            .ToListAsync(ct);
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await Set(db)
            .Include(x => x.Partner)
            .Include(x => x.Investment)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Shared-context variant for orchestrators (e.g. <see cref="CloseService"/>)
    /// that load every ledger for a snapshot inside one transaction.
    /// </summary>
    public Task<List<T>> GetByInvestmentOnContextAsync(InvestProDbContext db, Guid investmentId, CancellationToken ct = default)
        => Set(db)
            .Where(x => x.InvestmentId == investmentId && x.Status != EntityStatus.Deleted)
            .ToListAsync(ct);

    protected async Task<(bool ok, string? error)> EnsureInvestmentEditableAsync(InvestProDbContext db, Guid investmentId, CancellationToken ct)
    {
        var inv = await db.Investments.FirstOrDefaultAsync(x => x.Id == investmentId, ct);
        if (inv is null) return (false, "Investment not found.");
        if (inv.LifecycleStatus == InvestmentLifecycle.Closed)
            return (false, "Investment is closed. Ledger entries are locked.");
        if (inv.LifecycleStatus == InvestmentLifecycle.Draft)
            return (false, "Investment is still Draft. Activate it before adding ledger entries.");

        var partnerOk = await db.InvestmentPartners
            .AnyAsync(x => x.InvestmentId == investmentId && x.Status != EntityStatus.Deleted, ct);
        if (!partnerOk) return (false, "Investment has no partner contracts.");
        return (true, null);
    }

    public virtual async Task<(bool ok, string? error, T? saved)> CreateAsync(Guid investmentId, T model, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var (ok, err) = await EnsureInvestmentEditableAsync(db, investmentId, ct);
        if (!ok) return (false, err, null);

        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        model.InvestmentId = investmentId;
        if (model.EntryDate == default) model.EntryDate = DateTime.UtcNow;
        model.TransactionDate = DateTime.SpecifyKind(model.TransactionDate == default ? DateTime.UtcNow : model.TransactionDate, DateTimeKind.Utc);
        model.EntryDate = DateTime.SpecifyKind(model.EntryDate, DateTimeKind.Utc);
        model.ApprovalStatus = LedgerApprovalStatus.AutoApproved; // Phase 4 will replace this

        var repo = new EfRepository<T>(db);
        await repo.AddAsync(model, ct);
        await db.SaveChangesAsync(ct);
        return (true, null, model);
    }

    protected abstract void CopyForUpdate(T from, T to);

    public virtual async Task<(bool ok, string? error)> UpdateAsync(Guid id, T input, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var row = await Set(db).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return (false, "Not found.");
        var (ok, err) = await EnsureInvestmentEditableAsync(db, row.InvestmentId, ct);
        if (!ok) return (false, err);

        CopyForUpdate(input, row);
        row.TransactionDate = DateTime.SpecifyKind(input.TransactionDate == default ? row.TransactionDate : input.TransactionDate, DateTimeKind.Utc);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public virtual async Task<(bool ok, string? error, T? deleted)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var row = await Set(db).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return (false, "Not found.", null);
        var (ok, err) = await EnsureInvestmentEditableAsync(db, row.InvestmentId, ct);
        if (!ok) return (false, err, null);

        var repo = new EfRepository<T>(db);
        await repo.SoftDeleteAsync(row, ct);
        await db.SaveChangesAsync(ct);
        return (true, null, row);
    }
}
