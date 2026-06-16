using FlexCms.Framework.Db.Ef;
using FlexCms.Framework.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Data;

public class InvestProDbContext : DbContext
{
    public const string Prefix = "investpro";

    public InvestProDbContext(DbContextOptions<InvestProDbContext> options) : base(options) { }

    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ApprovalConfig> ApprovalConfigs => Set<ApprovalConfig>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<InvestmentPartner> InvestmentPartners => Set<InvestmentPartner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEfEntity).IsAssignableFrom(entityType.ClrType)) continue;
            var method = typeof(InvestProDbContext)
                .GetMethod(nameof(ApplyNaming),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(null, [modelBuilder]);
        }

        modelBuilder.Entity<Partner>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Nid).HasMaxLength(50);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccountNo).HasMaxLength(50);
            e.Property(x => x.MfsProvider).HasMaxLength(30);
            e.Property(x => x.MfsNumber).HasMaxLength(30);
            e.Property(x => x.NomineeName).HasMaxLength(200);
            e.Property(x => x.NomineePhone).HasMaxLength(30);
            e.Property(x => x.NomineeRelation).HasMaxLength(50);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Nid);
        });

        modelBuilder.Entity<ExpenseCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ApprovalConfig>(e =>
        {
            e.Property(x => x.LedgerType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.AutoApproveBelow).HasColumnType("numeric(18,2)");
            e.Property(x => x.RequireApprovalAbove).HasColumnType("numeric(18,2)");
            e.Property(x => x.RequireAllPartnersAbove).HasColumnType("numeric(18,2)");
            e.Property(x => x.ApproverRole).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.LedgerType).IsUnique();
        });

        modelBuilder.Entity<Investment>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.LifecycleStatus);
            e.HasMany(x => x.PartnerContracts)
             .WithOne(c => c.Investment!)
             .HasForeignKey(c => c.InvestmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvestmentPartner>(e =>
        {
            e.Property(x => x.ContractType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.PartnerRole).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.AgreedCapital).HasColumnType("numeric(18,2)");
            e.Property(x => x.AgreedLaborValue).HasColumnType("numeric(18,2)");
            e.Property(x => x.ProfitSharePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.LossSharePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.SpecialTerms).HasMaxLength(2000);
            e.HasIndex(x => new { x.InvestmentId, x.PartnerId }).IsUnique();
            e.HasOne(c => c.Partner!)
             .WithMany()
             .HasForeignKey(c => c.PartnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ApplyNaming<T>(ModelBuilder builder) where T : BaseEfEntity
        => builder.Entity<T>().ToTable(FcmsHelper.GetTableName<T>(Prefix));
}

public class Partner : BaseEfEntity
{
    public string Name { get; set; } = "";
    public string? Nid { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNo { get; set; }
    public string? MfsProvider { get; set; }
    public string? MfsNumber { get; set; }
    public string? NomineeName { get; set; }
    public string? NomineePhone { get; set; }
    public string? NomineeRelation { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ExpenseCategory : BaseEfEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum LedgerKind
{
    Expense = 1,
    Revenue = 2,
    Capital = 3,
    Labor   = 4,
}

public enum ApproverRole
{
    Admin       = 1,
    LeadPartner = 2,
    AnyPartner  = 3,
    AllPartners = 4,
}

public class ApprovalConfig : BaseEfEntity
{
    public LedgerKind LedgerType { get; set; }
    public decimal AutoApproveBelow { get; set; }
    public decimal RequireApprovalAbove { get; set; }
    public decimal RequireAllPartnersAbove { get; set; }
    public ApproverRole ApproverRole { get; set; } = ApproverRole.Admin;
}

public enum InvestmentLifecycle
{
    Draft   = 1,
    Active  = 2,
    Closing = 3,
    Closed  = 4,
}

public enum ContractType
{
    Mudarabah   = 1,
    Musharakah  = 2,
    LaborOnly   = 3,
    Mixed       = 4,
}

public enum PartnerRole
{
    RabUlMal       = 1,
    Mudarib        = 2,
    WorkingPartner = 3,
    SilentPartner  = 4,
}

public class Investment : BaseEfEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public InvestmentLifecycle LifecycleStatus { get; set; } = InvestmentLifecycle.Draft;
    public DateTime StartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }

    public List<InvestmentPartner> PartnerContracts { get; set; } = [];
}

public class InvestmentPartner : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    public Investment? Investment { get; set; }

    public Guid PartnerId { get; set; }
    public Partner? Partner { get; set; }

    public ContractType ContractType { get; set; } = ContractType.Musharakah;
    public PartnerRole PartnerRole { get; set; } = PartnerRole.WorkingPartner;

    public decimal AgreedCapital { get; set; }
    public decimal AgreedLaborValue { get; set; }
    public decimal ProfitSharePercent { get; set; }
    public decimal LossSharePercent { get; set; }

    public DateTime JoinedDate { get; set; }
    public string? SpecialTerms { get; set; }
}
