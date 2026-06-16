using FlexCms.Framework.Db;
using FlexCms.Framework.Db.Ef;
using DbModels = FlexCms.Framework.Db;
using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class InvestmentPartnerService
{
    private readonly ModuleActivationOptions _opts;
    public InvestmentPartnerService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    public async Task<List<InvestmentPartner>> GetByInvestmentAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentPartners
            .Include(x => x.Partner)
            .Where(x => x.InvestmentId == investmentId && x.Status != DbModels.EntityStatus.Deleted)
            .OrderBy(x => x.JoinedDate)
            .ToListAsync(ct);
    }

    public async Task<InvestmentPartner?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.InvestmentPartners
            .Include(x => x.Partner)
            .Include(x => x.Investment)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<(bool ok, string? error, InvestmentPartner? saved)> AddAsync(Guid investmentId, InvestmentPartner model, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var inv = await db.Investments.FirstOrDefaultAsync(x => x.Id == investmentId, ct);
        if (inv is null) return (false, "Investment not found.", null);
        if (inv.LifecycleStatus != InvestmentLifecycle.Draft)
            return (false, "Partners can only be added to Draft investments.", null);

        var partnerExists = await db.Partners.AnyAsync(x => x.Id == model.PartnerId, ct);
        if (!partnerExists) return (false, "Partner not found.", null);

        var duplicate = await db.InvestmentPartners
            .AnyAsync(x => x.InvestmentId == investmentId && x.PartnerId == model.PartnerId, ct);
        if (duplicate) return (false, "This partner is already part of the investment.", null);

        var err = ValidateContract(model);
        if (err is not null) return (false, err, null);

        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        model.InvestmentId = investmentId;
        if (model.JoinedDate == default) model.JoinedDate = DateTime.UtcNow;
        model.JoinedDate = DateTime.SpecifyKind(model.JoinedDate, DateTimeKind.Utc);

        var repo = new EfRepository<InvestmentPartner>(db);
        await repo.AddAsync(model, ct);
        await db.SaveChangesAsync(ct);
        return (true, null, model);
    }

    public async Task<(bool ok, string? error)> UpdateAsync(Guid id, InvestmentPartner input, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var row = await db.InvestmentPartners
            .Include(x => x.Investment)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return (false, "Not found.");
        if (row.Investment is null || row.Investment.LifecycleStatus != InvestmentLifecycle.Draft)
            return (false, "Contracts can only be edited while the investment is in Draft state.");

        var err = ValidateContract(input);
        if (err is not null) return (false, err);

        row.ContractType        = input.ContractType;
        row.PartnerRole         = input.PartnerRole;
        row.AgreedCapital       = input.AgreedCapital;
        row.AgreedLaborValue    = input.AgreedLaborValue;
        row.ProfitSharePercent  = input.ProfitSharePercent;
        row.LossSharePercent    = input.LossSharePercent;
        row.SpecialTerms        = input.SpecialTerms?.Trim();
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var row = await db.InvestmentPartners
            .Include(x => x.Investment)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return (false, "Not found.");
        if (row.Investment is null || row.Investment.LifecycleStatus != InvestmentLifecycle.Draft)
            return (false, "Contracts can only be removed while the investment is in Draft state.");

        var repo = new EfRepository<InvestmentPartner>(db);
        await repo.SoftDeleteAsync(row, ct);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    private static string? ValidateContract(InvestmentPartner m)
    {
        if (m.ProfitSharePercent < 0m || m.ProfitSharePercent > 100m) return "Profit % must be between 0 and 100.";
        if (m.LossSharePercent < 0m || m.LossSharePercent > 100m) return "Loss % must be between 0 and 100.";
        if (m.AgreedCapital < 0m) return "Capital cannot be negative.";
        if (m.AgreedLaborValue < 0m) return "Labor value cannot be negative.";
        if (m.ContractType == ContractType.LaborOnly && m.AgreedCapital > 0m)
            return "Labor-only partner cannot have a capital amount.";
        if (m.ContractType == ContractType.LaborOnly && m.LossSharePercent != 0m)
            return "Labor-only partner (Mudarib) must have 0% loss share (Shariah rule).";
        return null;
    }
}
