using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/partners")]
[FcmsAuthorize(InvestProPermissions.PartnerView)]
public class PartnerController : Controller
{
    private readonly PartnerService _service;
    private readonly AttachmentService _attachments;
    private readonly IFcmsLogService _log;

    public PartnerController(PartnerService service, AttachmentService attachments, IFcmsLogService log)
    {
        _service = service;
        _attachments = attachments;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetAllAsync(ct));

    [HttpGet("create")]
    [FcmsAuthorize(InvestProPermissions.PartnerCreate)]
    public IActionResult Create()
    {
        ViewData["IsNew"] = true;
        return View("Edit", new Partner { Id = Guid.Empty, IsActive = true });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PartnerCreate)]
    public async Task<IActionResult> Create(Partner model, CancellationToken ct)
    {
        Validate(model);
        if (!ModelState.IsValid) { ViewData["IsNew"] = true; return View("Edit", model); }

        var saved = await _service.CreateAsync(model, ct);
        await _log.LogAsync("investpro.partner.create", nameof(Partner), saved.Id.ToString(),
            value: saved, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Partner created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    [FcmsAuthorize(InvestProPermissions.PartnerEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var row = await _service.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        ViewData["Attachments"] = await _attachments.GetForOwnerAsync(AttachmentOwnerType.Partner, id, ct);
        return View(row);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PartnerEdit)]
    public async Task<IActionResult> Edit(Guid id, Partner model, CancellationToken ct)
    {
        Validate(model);
        if (!ModelState.IsValid) return View(model);

        var ok = await _service.UpdateAsync(id, model, ct);
        if (!ok) return NotFound();
        await _log.LogAsync("investpro.partner.update", nameof(Partner), id.ToString(),
            value: new { id, model.Name, model.Phone, model.IsActive }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Partner updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PartnerDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (deleted is null) return Json(new { isSuccess = false, message = "Not found." });
        await _log.LogAsync("investpro.partner.delete", nameof(Partner), id.ToString(),
            value: deleted, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Deleted." });
    }

    // ── Polymorphic attachment endpoints used by _FcmsUploader ─────────

    [HttpPost("{id:guid}/attachments/upload")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PartnerEdit)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        var partner = await _service.GetByIdAsync(id, ct);
        if (partner is null) return Json(new { isSuccess = false, message = "Partner not found." });

        var (ok, error, saved) = await _attachments.UploadAsync(
            AttachmentOwnerType.Partner, id, file, AttachmentLabel.Document, ct);
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Upload failed." });

        await _log.LogAsync("investpro.partner.attachment-upload", nameof(LedgerAttachment), saved!.Id.ToString(),
            value: new { partnerId = id, saved.FileName, saved.FileSize }, module: InvestProModule.ModuleIdValue, ct: ct);

        return Json(new
        {
            isSuccess = true,
            id = saved.Id,
            url = saved.FilePath,
            fileName = saved.FileName,
            size = saved.FileSize,
        });
    }

    [HttpPost("attachments/{aid:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PartnerEdit)]
    public async Task<IActionResult> DeleteAttachment(Guid aid, CancellationToken ct)
    {
        var (ok, error, deleted) = await _attachments.DeleteAsync(aid, ct);
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Failed." });
        await _log.LogAsync("investpro.partner.attachment-delete", nameof(LedgerAttachment), aid.ToString(),
            value: deleted!, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Removed." });
    }

    private void Validate(Partner m)
    {
        if (string.IsNullOrWhiteSpace(m.Name))
            ModelState.AddModelError(nameof(m.Name), "Name is required.");
        if (!string.IsNullOrWhiteSpace(m.Email) && !m.Email.Contains('@'))
            ModelState.AddModelError(nameof(m.Email), "Email format is invalid.");
    }
}
