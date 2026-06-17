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
    public DbSet<CloseRequest> CloseRequests => Set<CloseRequest>();
    public DbSet<CloseApproval> CloseApprovals => Set<CloseApproval>();
    public DbSet<InvestmentSnapshot> InvestmentSnapshots => Set<InvestmentSnapshot>();
    public DbSet<SnapshotPartnerDetail> SnapshotPartnerDetails => Set<SnapshotPartnerDetail>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<ReopenRequest> ReopenRequests => Set<ReopenRequest>();
    public DbSet<ReopenApproval> ReopenApprovals => Set<ReopenApproval>();

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
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
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
            e.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.LedgerType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.FileType).HasMaxLength(100);
            e.Property(x => x.AttachmentLabel).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.OwnerType, x.OwnerId });
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

        modelBuilder.Entity<CloseRequest>(e =>
        {
            e.Property(x => x.RequestStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.RequestStatus);
        });

        modelBuilder.Entity<CloseApproval>(e =>
        {
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.CloseRequest!).WithMany(r => r.Approvals).HasForeignKey(x => x.CloseRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvestmentSnapshot>(e =>
        {
            e.Property(x => x.InvestmentCode).HasMaxLength(40).IsRequired();
            e.Property(x => x.InvestmentName).HasMaxLength(200).IsRequired();
            e.Property(x => x.GrossRevenue).HasColumnType("numeric(18,2)");
            e.Property(x => x.GrossExpense).HasColumnType("numeric(18,2)");
            e.Property(x => x.NetPL).HasColumnType("numeric(18,2)");
            e.Property(x => x.TotalCapital).HasColumnType("numeric(18,2)");
            e.Property(x => x.TotalLaborValue).HasColumnType("numeric(18,2)");
            e.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
            e.Property(x => x.ClosedByUserName).HasMaxLength(200);
            e.Property(x => x.SnapshotStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.SupersededReason).HasMaxLength(2000);
            // Drop unique constraint on InvestmentId — multiple snapshots
            // per investment are now allowed (one Active + many Superseded).
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => new { x.InvestmentId, x.SnapshotStatus });
        });

        modelBuilder.Entity<SnapshotPartnerDetail>(e =>
        {
            e.Property(x => x.PreviousSettlementAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.AdjustmentAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<ReopenRequest>(e =>
        {
            e.Property(x => x.RequestStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.InvestmentId);
            e.HasIndex(x => x.RequestStatus);
        });

        modelBuilder.Entity<ReopenApproval>(e =>
        {
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.ReopenRequest!).WithMany(r => r.Approvals).HasForeignKey(x => x.ReopenRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnapshotPartnerDetail>(e =>
        {
            e.Property(x => x.PartnerName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PartnerNid).HasMaxLength(50);
            e.Property(x => x.PartnerPhone).HasMaxLength(30);
            e.Property(x => x.PartnerEmail).HasMaxLength(200);
            e.Property(x => x.ContractTypeAtClose).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.PartnerRoleAtClose).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.CapitalContributed).HasColumnType("numeric(18,2)");
            e.Property(x => x.LaborValueContributed).HasColumnType("numeric(18,2)");
            e.Property(x => x.ProfitSharePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.LossSharePercent).HasColumnType("numeric(7,4)");
            e.Property(x => x.ProfitShareAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.LossShareAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.WithdrawalsDuringInvestment).HasColumnType("numeric(18,2)");
            e.Property(x => x.FinalSettlementAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.ZakatEligibleAmount).HasColumnType("numeric(18,2)");
            e.HasIndex(x => new { x.SnapshotId, x.PartnerId }).IsUnique();
            e.HasOne(x => x.Snapshot!).WithMany(s => s.PartnerDetails).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payout>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ReferenceNo).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => x.SnapshotId);
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
    /// <summary>
    /// Link to the app-user identity that may log in AS this partner.
    /// Required for close/reopen/approval decision endpoints to bind the
    /// caller to a partner row (otherwise any user holding the
    /// .decide permission could vote on behalf of another partner).
    /// Null when the partner has no user account — only SuperAdmin can
    /// record their votes in that case.
    /// </summary>
    public Guid? UserId { get; set; }
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

public enum AttachmentOwnerType
{
    Partner    = 1,
    Investment = 2,
    Capital    = 3,
    Labor      = 4,
    Expense    = 5,
    Revenue    = 6,
}

/// <summary>
/// Polymorphic attachment row. Files for partner KYC documents, ledger
/// entries, and (future) investment-level files all live in this single
/// table — owner is identified by the (<see cref="OwnerType"/>,
/// <see cref="OwnerId"/>) pair. The legacy LedgerType field is kept and
/// auto-populated for ledger-kind owners so existing reports keep working.
/// </summary>
public class LedgerAttachment : BaseEfEntity
{
    public AttachmentOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary>Mirrors OwnerType when OwnerType is Capital/Labor/Expense/Revenue. Null when OwnerType is Partner or Investment.</summary>
    public LedgerKind? LedgerType { get; set; }

    /// <summary>Same as OwnerId when OwnerType is a ledger kind. Kept for query-shape backwards compatibility.</summary>
    public Guid? LedgerEntryId { get; set; }

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

public enum CloseRequestStatus
{
    Pending   = 1,
    Approved  = 2,
    Rejected  = 3,
    Cancelled = 4,
}

public enum PayoutStatus
{
    Pending = 1,
    Paid    = 2,
    Failed  = 3,
}

public class CloseRequest : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public DateTime InitiatedAt { get; set; }
    public CloseRequestStatus RequestStatus { get; set; } = CloseRequestStatus.Pending;
    public DateTime? FinalizedAt { get; set; }
    public string? Notes { get; set; }

    public List<CloseApproval> Approvals { get; set; } = [];
}

public class CloseApproval : BaseEfEntity
{
    public Guid CloseRequestId { get; set; }
    public CloseRequest? CloseRequest { get; set; }

    public Guid PartnerId { get; set; }
    public DecisionKind Decision { get; set; } = DecisionKind.Pending;
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }
}

public enum SnapshotStatus
{
    Active     = 1,  // current authoritative snapshot for the investment
    Superseded = 2,  // replaced by a newer one after a reopen → reclose
}

public class InvestmentSnapshot : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    public string InvestmentCode { get; set; } = "";
    public string InvestmentName { get; set; } = "";
    public DateTime InvestmentStartDate { get; set; }
    public DateTime ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }

    public decimal GrossRevenue { get; set; }
    public decimal GrossExpense { get; set; }
    public decimal NetPL { get; set; }
    public decimal TotalCapital { get; set; }
    public decimal TotalLaborValue { get; set; }
    public int PartnerCount { get; set; }

    /// <summary>SHA256 hex of the snapshot + its partner details (in canonical order). Recomputed and compared on read for tamper detection.</summary>
    public string Checksum { get; set; } = "";

    // ── Versioning (Phase 7) ────────────────────────────────────────────
    /// <summary>1 for the first close. Bumps every reclose after a reopen.</summary>
    public int Version { get; set; } = 1;
    public SnapshotStatus SnapshotStatus { get; set; } = SnapshotStatus.Active;
    /// <summary>Previous version of this snapshot, when this row was produced after a reopen. Null on v1.</summary>
    public Guid? PreviousSnapshotId { get; set; }
    public string? SupersededReason { get; set; }
    public DateTime? SupersededAt { get; set; }

    public List<SnapshotPartnerDetail> PartnerDetails { get; set; } = [];
}

public class SnapshotPartnerDetail : BaseEfEntity
{
    public Guid SnapshotId { get; set; }
    public InvestmentSnapshot? Snapshot { get; set; }

    public Guid PartnerId { get; set; }

    // ── Identity at-close (immutable copies) ────────────────────────────
    // Partner row in the global pool can be edited / soft-deleted later —
    // these fields freeze who-was-who when the investment was closed.
    public string PartnerName { get; set; } = "";
    public string? PartnerNid { get; set; }
    public string? PartnerPhone { get; set; }
    public string? PartnerEmail { get; set; }

    public ContractType ContractTypeAtClose { get; set; }
    public PartnerRole PartnerRoleAtClose { get; set; }

    public decimal CapitalContributed { get; set; }
    public decimal LaborValueContributed { get; set; }
    public decimal ProfitSharePercent { get; set; }
    public decimal LossSharePercent { get; set; }
    public decimal ProfitShareAmount { get; set; }
    public decimal LossShareAmount { get; set; }
    public decimal WithdrawalsDuringInvestment { get; set; }
    public decimal FinalSettlementAmount { get; set; }
    public decimal ZakatEligibleAmount { get; set; }

    // ── Versioning (Phase 7) ────────────────────────────────────────────
    /// <summary>Settlement amount from the previous snapshot version. 0 for v1, prior value for v2+.</summary>
    public decimal PreviousSettlementAmount { get; set; }
    /// <summary>FinalSettlementAmount − PreviousSettlementAmount. Drives the adjustment payout row direction.</summary>
    public decimal AdjustmentAmount { get; set; }
}

public enum ReopenRequestStatus
{
    Pending   = 1,
    Approved  = 2,
    Rejected  = 3,
    Cancelled = 4,
}

public class ReopenRequest : BaseEfEntity
{
    public Guid InvestmentId { get; set; }
    /// <summary>The snapshot the requester wants to invalidate.</summary>
    public Guid CurrentSnapshotId { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public DateTime InitiatedAt { get; set; }
    /// <summary>Mandatory — why the close is being undone.</summary>
    public string Reason { get; set; } = "";
    public ReopenRequestStatus RequestStatus { get; set; } = ReopenRequestStatus.Pending;
    public DateTime? FinalizedAt { get; set; }

    public List<ReopenApproval> Approvals { get; set; } = [];
}

public class ReopenApproval : BaseEfEntity
{
    public Guid ReopenRequestId { get; set; }
    public ReopenRequest? ReopenRequest { get; set; }

    public Guid PartnerId { get; set; }
    public DecisionKind Decision { get; set; } = DecisionKind.Pending;
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }
}

public enum PayoutDirection
{
    /// <summary>Investment owes the partner. Standard case.</summary>
    Outgoing = 1,
    /// <summary>Partner owes back to the investment pool. Used after a reclose lowers a partner's settlement below what was already paid.</summary>
    Incoming = 2,
}

public class Payout : BaseEfEntity
{
    public Guid SnapshotId { get; set; }
    public Guid PartnerDetailId { get; set; }
    public Guid InvestmentId { get; set; }
    public Guid PartnerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public PayoutStatus PaymentStatus { get; set; } = PayoutStatus.Pending;
    public PayoutDirection Direction { get; set; } = PayoutDirection.Outgoing;
    /// <summary>True when this row was emitted to settle a Δ between two snapshot versions, not the headline payout itself.</summary>
    public bool IsAdjustment { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Notes { get; set; }
}
