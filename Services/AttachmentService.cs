using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.Framework.Storage;
using FlexCms.InvestPro.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using EntityStatus = FlexCms.Framework.Db.EntityStatus;

namespace FlexCms.InvestPro.Services;

[FcmsScoped]
public class AttachmentService
{
    private readonly ModuleActivationOptions _opts;
    private readonly IFcmsFileUploadService _uploader;

    public AttachmentService(ModuleActivationOptions opts, IFcmsFileUploadService uploader)
    {
        _opts = opts;
        _uploader = uploader;
    }

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    public const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    public const int MaxFilesPerOwner = 20;

    private static LedgerKind? LedgerKindFor(AttachmentOwnerType owner) => owner switch
    {
        AttachmentOwnerType.Capital => LedgerKind.Capital,
        AttachmentOwnerType.Labor   => LedgerKind.Labor,
        AttachmentOwnerType.Expense => LedgerKind.Expense,
        AttachmentOwnerType.Revenue => LedgerKind.Revenue,
        _ => null,
    };

    private static string FolderFor(AttachmentOwnerType owner) => owner switch
    {
        AttachmentOwnerType.Partner    => "investpro/partners",
        AttachmentOwnerType.Investment => "investpro/investments",
        AttachmentOwnerType.Capital    => "investpro/capital",
        AttachmentOwnerType.Labor      => "investpro/labor",
        AttachmentOwnerType.Expense    => "investpro/expenses",
        AttachmentOwnerType.Revenue    => "investpro/revenues",
        _ => "investpro/misc",
    };

    public async Task<List<LedgerAttachment>> GetForOwnerAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.LedgerAttachments
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.Status != EntityStatus.Deleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    // ── Back-compat shim for ledger-entry-only callers ──────────────────
    public Task<List<LedgerAttachment>> GetForEntryAsync(LedgerKind kind, Guid entryId, CancellationToken ct = default)
        => GetForOwnerAsync(kind switch
        {
            LedgerKind.Capital => AttachmentOwnerType.Capital,
            LedgerKind.Labor   => AttachmentOwnerType.Labor,
            LedgerKind.Expense => AttachmentOwnerType.Expense,
            LedgerKind.Revenue => AttachmentOwnerType.Revenue,
            _ => AttachmentOwnerType.Investment,
        }, entryId, ct);

    public async Task<LedgerAttachment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        return await db.LedgerAttachments.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<(bool ok, string? error, LedgerAttachment? saved)> UploadAsync(
        AttachmentOwnerType ownerType, Guid ownerId,
        IFormFile file, AttachmentLabel label,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return (false, "No file uploaded.", null);
        if (file.Length > MaxFileSize) return (false, $"File too large. Max {MaxFileSize / 1024 / 1024} MB.", null);

        await using var db = OpenDb();
        var existing = await db.LedgerAttachments.CountAsync(
            x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.Status != EntityStatus.Deleted, ct);
        if (existing >= MaxFilesPerOwner)
            return (false, $"Maximum {MaxFilesPerOwner} files per owner reached.", null);

        var compress = new ImageCompressionOptions(); // defaults
        UploadResult result;
        try
        {
            result = await _uploader.SaveAsync(file, FolderFor(ownerType), compress, ct);
        }
        catch (Exception ex)
        {
            return (false, $"Upload failed: {ex.Message}", null);
        }

        var row = new LedgerAttachment
        {
            Id = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            LedgerType = LedgerKindFor(ownerType),
            LedgerEntryId = LedgerKindFor(ownerType) is null ? null : ownerId,
            FilePath = result.PublicUrl,
            FileName = file.FileName,
            FileType = result.ContentType,
            FileSize = result.FileSize,
            AttachmentLabel = label,
        };
        db.LedgerAttachments.Add(row);
        await db.SaveChangesAsync(ct);
        return (true, null, row);
    }

    /// <summary>Legacy ledger-kind upload — wraps the polymorphic path.</summary>
    public Task<(bool ok, string? error, LedgerAttachment? saved)> UploadAsync(
        Guid investmentId, LedgerKind kind, Guid entryId,
        IFormFile file, AttachmentLabel label, CancellationToken ct = default)
        => UploadAsync(kind switch
        {
            LedgerKind.Capital => AttachmentOwnerType.Capital,
            LedgerKind.Labor   => AttachmentOwnerType.Labor,
            LedgerKind.Expense => AttachmentOwnerType.Expense,
            LedgerKind.Revenue => AttachmentOwnerType.Revenue,
            _ => AttachmentOwnerType.Investment,
        }, entryId, file, label, ct);

    public async Task<(bool ok, string? error, LedgerAttachment? deleted)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        var row = await db.LedgerAttachments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return (false, "Not found.", null);

        row.Status = EntityStatus.Deleted;
        row.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try { await _uploader.DeleteAsync(row.FilePath.TrimStart('/'), ct); }
        catch { /* best-effort */ }

        return (true, null, row);
    }
}
