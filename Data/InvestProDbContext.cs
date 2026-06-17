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
    public DbSet<CapitalContribution> CapitalContributions => Set<CapitalContribution>();
    public DbSet<LaborContribution> LaborContributions => Set<LaborContribution>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Revenue> Revenues => Set<Revenue>();
    public DbSet<LedgerAttachment> LedgerAttachments => Set<LedgerAttachment>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

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

        modelBuilder.Entity<CapitalContribution>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.ContributionType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.AssetDescription).HasMaxLength(500);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ReferenceNo).HasMaxLength(100);
            e.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.TransactionDate);
            e.HasOne(x => x.Investment!).WithMany().HasForeignKey(x => x.InvestmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Partner!).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LaborContribution>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.HoursOrDays).HasColumnType("numeric(10,2)");
            e.Property(x => x.RatePerUnit).HasColumnType("numeric(18,2)");
            e.Property(x => x.UnitType).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.Property(x => x.TaskType).HasMaxLength(100);
            e.Property(x => x.WorkDescription).HasMaxLength(1000);
            e.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.TransactionDate);
            e.HasOne(x => x.Investment!).WithMany().HasForeignKey(x => x.InvestmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Partner!).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.PaidTo).HasMaxLength(200);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ReceiptNo).HasMaxLength(100);
            e.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.TransactionDate);
            e.HasIndex(x => x.ExpenseCategoryId);
            e.HasOne(x => x.Investment!).WithMany().HasForeignKey(x => x.InvestmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Partner!).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category!).WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Revenue>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Customer).HasMaxLength(200);
            e.Property(x => x.SalesChannel).HasMaxLength(100);
            e.Property(x => x.InvoiceNo).HasMaxLength(100);
            e.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.TransactionDate);
            e.HasOne(x => x.Investment!).WithMany().HasForeignKey(x => x.InvestmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Partner!).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerAttachment>(e =>
        {
            e.Property(x => x.LedgerType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.FileType).HasMaxLength(100);
            e.Property(x => x.AttachmentLabel).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.LedgerType, x.LedgerEntryId });
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.Property(x => x.LedgerType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.RequestStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.RequiredApproverMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ApproverRole).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => new { x.LedgerType, x.LedgerEntryId }).IsUnique();
            e.HasIndex(x => x.RequestStatus);
        });

        modelBuilder.Entity<ApprovalDecision>(e =>
        {
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => x.ApprovalRequestId);
            e.HasOne(x => x.Request!).WithMany(r => r.Decisions).HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
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

public enum ContributionType
{
    Cash   = 1,
    InKind = 2,
}

public enum PaymentMethod
{
    Cash   = 1,
    Bank   = 2,
    MFS    = 3,
    Cheque = 4,
    Other  = 5,
}

public enum LaborUnitType
{
    Hours = 1,
    Days  = 2,
    Task  = 3,
}

public enum RevenueSourceType
{
    Sales   = 1,
    Service = 2,
    Other   = 3,
}

public enum LedgerApprovalStatus
{
    AutoApproved = 1,
    Pending      = 2,
    Approved     = 3,
    Rejected     = 4,
}

public enum AttachmentLabel
{
    Voucher  = 1,
    Invoice  = 2,
    Receipt  = 3,
    Photo    = 4,
    Document = 5,
    Other    = 6,
}

public abstract class LedgerEntryBase : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    public Investment? Investment { get; set; }

    public Guid PartnerId { get; set; }
    public Partner? Partner { get; set; }

    public DateTime TransactionDate { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string? Details { get; set; }
    public LedgerApprovalStatus ApprovalStatus { get; set; } = LedgerApprovalStatus.AutoApproved;
    public string? Notes { get; set; }
}

public class CapitalContribution : LedgerEntryBase
{
    public ContributionType ContributionType { get; set; } = ContributionType.Cash;
    public string? AssetDescription { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReferenceNo { get; set; }
}

public class LaborContribution : LedgerEntryBase
{
    public LaborUnitType UnitType { get; set; } = LaborUnitType.Hours;
    public decimal HoursOrDays { get; set; }
    public decimal RatePerUnit { get; set; }
    public string? TaskType { get; set; }
    public string? WorkDescription { get; set; }
}

public class Expense : LedgerEntryBase
{
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public string? PaidTo { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReceiptNo { get; set; }
}

public class Revenue : LedgerEntryBase
{
    public RevenueSourceType SourceType { get; set; } = RevenueSourceType.Sales;
    public string? Customer { get; set; }
    public string? SalesChannel { get; set; }
    public string? InvoiceNo { get; set; }
}

public class LedgerAttachment : BaseEfEntity
{
    public LedgerKind LedgerType { get; set; }
    public Guid LedgerEntryId { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? FileType { get; set; }
    public long FileSize { get; set; }
    public AttachmentLabel AttachmentLabel { get; set; } = AttachmentLabel.Other;
}

public enum ApprovalRequestStatus
{
    Pending  = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

public enum ApproverMode
{
    Auto         = 1,
    SingleApprover = 2,
    AllPartners  = 3,
}

public enum DecisionKind
{
    Pending  = 1,
    Approved = 2,
    Rejected = 3,
}

public class ApprovalRequest : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    public LedgerKind LedgerType { get; set; }
    public Guid LedgerEntryId { get; set; }
    public decimal Amount { get; set; }
    public ApproverMode RequiredApproverMode { get; set; }
    public ApproverRole? ApproverRole { get; set; }
    public ApprovalRequestStatus RequestStatus { get; set; } = ApprovalRequestStatus.Pending;
    public DateTime InitiatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Reason { get; set; }

    public List<ApprovalDecision> Decisions { get; set; } = [];
}

public class ApprovalDecision : BaseEfEntity
{
    public Guid ApprovalRequestId { get; set; }
    public ApprovalRequest? Request { get; set; }

    public Guid? PartnerId { get; set; }
    public Guid? UserId { get; set; }

    public DecisionKind Decision { get; set; } = DecisionKind.Pending;
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }
}
