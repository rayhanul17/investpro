using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/partners")]
[FcmsAuthorize(InvestProPermissions.PartnerView)]
public class PartnerController : Controller
{
    private readonly PartnerService _service;
    private readonly IFcmsLogService _log;

    public PartnerController(PartnerService service, IFcmsLogService log)
    {
        _service = service;
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

    private void Validate(Partner m)
    {
        if (string.IsNullOrWhiteSpace(m.Name))
            ModelState.AddModelError(nameof(m.Name), "Name is required.");
        if (!string.IsNullOrWhiteSpace(m.Email) && !m.Email.Contains('@'))
            ModelState.AddModelError(nameof(m.Email), "Email format is invalid.");
    }
}
