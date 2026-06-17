using FlexCms.Framework.Models;
using FlexCms.Framework.Modules;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexCms.InvestPro;

public class InvestProModule : BaseModule
{
    public const string ModuleIdValue = "FlexCms.InvestPro";

    public override string ModuleId    => ModuleIdValue;
    public override string ModuleName  => "InvestPro";
    public override string Version     => "1.0.0";
    public override string TablePrefix => "investpro";

    public override void RegisterServices(IServiceCollection services)
    {
    }

    public override DbContext? CreateMigrationContext(string connectionString, string provider)
    {
        var builder = new DbContextOptionsBuilder<InvestProDbContext>();
        switch (provider)
        {
            case "mysql":
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            case "mssql":
                builder.UseSqlServer(connectionString);
                break;
            case "postgresql":
                builder.UseNpgsql(connectionString);
                break;
        }
        return new InvestProDbContext(builder.Options);
    }

    public override async Task SeedDataAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var opts = sp.GetRequiredService<ModuleActivationOptions>();
        var ctx = CreateMigrationContext(opts.ConnectionString, opts.Provider) as InvestProDbContext;
        if (ctx is null) return;
        await using (ctx)
        {
            if (!await ctx.Database.CanConnectAsync(ct)) return;
            var applied = await ctx.Database.GetAppliedMigrationsAsync(ct);
            if (!applied.Any())
            {
                Console.WriteLine($"[{ModuleId}] No EF migrations found — run `dotnet ef migrations add InitialSchema` to enable seeding.");
                return;
            }

            await SeedExpenseCategoriesAsync(ctx, ct);
            await SeedApprovalConfigsAsync(ctx, ct);
        }
    }

    private static async Task SeedExpenseCategoriesAsync(InvestProDbContext ctx, CancellationToken ct)
    {
        if (await ctx.ExpenseCategories.AnyAsync(ct)) return;

        var defaults = new (string Name, string Desc, int Order)[]
        {
            ("Product/Inventory", "Goods purchased for the investment activity.", 10),
            ("Transportation",   "Truck, courier, fuel, intra-city travel.",     20),
            ("Legal & Compliance","Trade license, lawyer fees, government filings.", 30),
            ("Rent",             "Warehouse, office, shop rent.",                  40),
            ("Salary & Wages",   "Worker pay and stipends.",                       50),
            ("Utility",          "Electricity, gas, water, internet.",             60),
            ("Marketing",        "Ads, promotions, branding.",                     70),
            ("Office Supplies",  "Stationery, packaging, small equipment.",        80),
            ("Repair & Maintenance","Servicing, tools, machine upkeep.",            90),
            ("Tax & Govt Fees",  "Income tax, VAT, licensing renewals.",          100),
            ("Bank Charges",     "Transaction fees, MFS charges.",                110),
            ("Insurance",        "Asset and shipment insurance.",                 120),
            ("Others",           "Anything that does not fit other categories.",  999),
        };

        foreach (var (name, desc, order) in defaults)
        {
            ctx.ExpenseCategories.Add(new ExpenseCategory
            {
                Id          = Guid.NewGuid(),
                Name        = name,
                Description = desc,
                IsSystem    = true,
                IsActive    = true,
                SortOrder   = order,
            });
        }
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task SeedApprovalConfigsAsync(InvestProDbContext ctx, CancellationToken ct)
    {
        if (await ctx.ApprovalConfigs.AnyAsync(ct)) return;

        var defaults = new (LedgerKind L, decimal AutoBelow, decimal RequireAbove, decimal AllPartnersAbove, ApproverRole Role)[]
        {
            (LedgerKind.Expense, 5_000m,   5_000m,   50_000m,  ApproverRole.LeadPartner),
            (LedgerKind.Revenue, 10_000m,  10_000m,  100_000m, ApproverRole.LeadPartner),
            (LedgerKind.Capital, 0m,       0m,       0m,       ApproverRole.AllPartners),
            (LedgerKind.Labor,   0m,       0m,       0m,       ApproverRole.LeadPartner),
        };

        foreach (var (l, autoBelow, reqAbove, allAbove, role) in defaults)
        {
            ctx.ApprovalConfigs.Add(new ApprovalConfig
            {
                Id                      = Guid.NewGuid(),
                LedgerType              = l,
                AutoApproveBelow        = autoBelow,
                RequireApprovalAbove    = reqAbove,
                RequireAllPartnersAbove = allAbove,
                ApproverRole            = role,
            });
        }
        await ctx.SaveChangesAsync(ct);
    }

    public override async Task OnUpgradeAsync(string fromVersion, IServiceProvider sp, CancellationToken ct = default)
    {
        await Task.CompletedTask;
    }

    public override async Task DropTablesAsync(string connectionString, string provider, CancellationToken ct = default)
    {
        var ctx = CreateMigrationContext(connectionString, provider);
        if (ctx is null) return;
        await using (ctx)
            await ctx.Database.EnsureDeletedAsync(ct);
    }

    public override List<FcmsMenuItemDef> GetMenuItems() =>
    [
        new FcmsMenuItemDef
        {
            DefaultName        = "InvestPro",
            Icon               = "bi bi-bank2",
            Url                = "/investpro/admin/investments",
            Order              = 500,
            RequiredPermission = InvestProPermissions.InvestmentView,
        },
    ];

    public override List<FcmsPermissionDef> GetPermissions() =>
    [
        new(InvestProPermissions.PartnerViewKey,           "View partners",                "InvestPro"),
        new(InvestProPermissions.PartnerCreateKey,         "Create partners",              "InvestPro"),
        new(InvestProPermissions.PartnerEditKey,           "Edit partners",                "InvestPro"),
        new(InvestProPermissions.PartnerDeleteKey,         "Delete partners",              "InvestPro"),

        new(InvestProPermissions.CategoryViewKey,          "View expense categories",      "InvestPro"),
        new(InvestProPermissions.CategoryCreateKey,        "Create expense categories",    "InvestPro"),
        new(InvestProPermissions.CategoryEditKey,          "Edit expense categories",      "InvestPro"),
        new(InvestProPermissions.CategoryDeleteKey,        "Delete expense categories",    "InvestPro"),

        new(InvestProPermissions.ApprovalConfigViewKey,    "View approval configuration",  "InvestPro"),
        new(InvestProPermissions.ApprovalConfigEditKey,    "Edit approval configuration",  "InvestPro"),

        new(InvestProPermissions.InvestmentViewKey,        "View investments",             "InvestPro"),
        new(InvestProPermissions.InvestmentCreateKey,      "Create investments",           "InvestPro"),
        new(InvestProPermissions.InvestmentEditKey,        "Edit investments",             "InvestPro"),
        new(InvestProPermissions.InvestmentDeleteKey,      "Delete investments",           "InvestPro"),
        new(InvestProPermissions.InvestmentActivateKey,    "Activate investments",         "InvestPro"),

        new(InvestProPermissions.LedgerViewKey,            "View ledger entries",          "InvestPro"),
        new(InvestProPermissions.LedgerWriteKey,           "Create/edit/delete ledger entries", "InvestPro"),

        new(InvestProPermissions.ApprovalViewKey,          "View approval requests",       "InvestPro"),
        new(InvestProPermissions.ApprovalDecideKey,        "Approve or reject requests",   "InvestPro"),

        new(InvestProPermissions.CloseRequestKey,          "Request investment close",     "InvestPro"),
        new(InvestProPermissions.CloseDecideKey,           "Approve / reject a close",     "InvestPro"),
        new(InvestProPermissions.SnapshotViewKey,          "View closure snapshots",       "InvestPro"),
        new(InvestProPermissions.PayoutManageKey,          "Manage payouts (mark paid)",   "InvestPro"),

        new(InvestProPermissions.ReportViewKey,            "View reports + downloads",     "InvestPro"),

        new(InvestProPermissions.ReopenRequestKey,         "Request investment reopen",    "InvestPro"),
        new(InvestProPermissions.ReopenDecideKey,          "Approve / reject a reopen",    "InvestPro"),
    ];
}

public static class InvestProPermissions
{
    // ── Short keys ──
    // Only used inside this assembly to register defs in GetPermissions().
    // Controllers and external callers MUST use the fully-qualified consts
    // below (e.g. PartnerView, PartnerCreate). Marking these `internal`
    // makes the compiler refuse short-string [FcmsAuthorize] uses from
    // outside the module — that would bypass the module-prefix and check
    // for the wrong permission row in fcms_permissions.
    internal const string PartnerViewKey   = "partner.view";
    internal const string PartnerCreateKey = "partner.create";
    internal const string PartnerEditKey   = "partner.edit";
    internal const string PartnerDeleteKey = "partner.delete";

    internal const string CategoryViewKey   = "category.view";
    internal const string CategoryCreateKey = "category.create";
    internal const string CategoryEditKey   = "category.edit";
    internal const string CategoryDeleteKey = "category.delete";

    internal const string ApprovalConfigViewKey = "approval-config.view";
    internal const string ApprovalConfigEditKey = "approval-config.edit";

    internal const string InvestmentViewKey     = "investment.view";
    internal const string InvestmentCreateKey   = "investment.create";
    internal const string InvestmentEditKey     = "investment.edit";
    internal const string InvestmentDeleteKey   = "investment.delete";
    internal const string InvestmentActivateKey = "investment.activate";

    internal const string LedgerViewKey  = "ledger.view";
    internal const string LedgerWriteKey = "ledger.write";

    internal const string ApprovalViewKey   = "approval.view";
    internal const string ApprovalDecideKey = "approval.decide";

    internal const string CloseRequestKey  = "close.request";
    internal const string CloseDecideKey   = "close.decide";
    internal const string SnapshotViewKey  = "snapshot.view";
    internal const string PayoutManageKey  = "payout.manage";

    internal const string ReportViewKey    = "report.view";

    internal const string ReopenRequestKey = "reopen.request";
    internal const string ReopenDecideKey  = "reopen.decide";

    private const string P = "flexcms.investpro.";

    public const string PartnerView   = P + PartnerViewKey;
    public const string PartnerCreate = P + PartnerCreateKey;
    public const string PartnerEdit   = P + PartnerEditKey;
    public const string PartnerDelete = P + PartnerDeleteKey;

    public const string CategoryView   = P + CategoryViewKey;
    public const string CategoryCreate = P + CategoryCreateKey;
    public const string CategoryEdit   = P + CategoryEditKey;
    public const string CategoryDelete = P + CategoryDeleteKey;

    public const string ApprovalConfigView = P + ApprovalConfigViewKey;
    public const string ApprovalConfigEdit = P + ApprovalConfigEditKey;

    public const string InvestmentView     = P + InvestmentViewKey;
    public const string InvestmentCreate   = P + InvestmentCreateKey;
    public const string InvestmentEdit     = P + InvestmentEditKey;
    public const string InvestmentDelete   = P + InvestmentDeleteKey;
    public const string InvestmentActivate = P + InvestmentActivateKey;

    public const string LedgerView  = P + LedgerViewKey;
    public const string LedgerWrite = P + LedgerWriteKey;

    public const string ApprovalView   = P + ApprovalViewKey;
    public const string ApprovalDecide = P + ApprovalDecideKey;

    public const string CloseRequest  = P + CloseRequestKey;
    public const string CloseDecide   = P + CloseDecideKey;
    public const string SnapshotView  = P + SnapshotViewKey;
    public const string PayoutManage  = P + PayoutManageKey;

    public const string ReportView    = P + ReportViewKey;

    public const string ReopenRequest = P + ReopenRequestKey;
    public const string ReopenDecide  = P + ReopenDecideKey;
}
