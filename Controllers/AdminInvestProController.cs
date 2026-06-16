using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

/// <summary>
/// Admin CRUD controller. Wire-in is automatic — modules' assemblies are added
/// as MVC ApplicationParts during framework startup, so attribute-routed
/// actions become reachable without any host-side registration. Razor views
/// under <c>Views/AdminInvestPro/</c> are compiled into this DLL by the
/// Razor SDK and served by the host.
///
/// <para>
/// Permission keys must match the strings registered by the module's
/// <c>GetPermissions()</c>. They are fully-qualified (module ID prefix included)
/// — the helper constants on <see cref="InvestProPermissions"/> encode that.
/// </para>
/// </summary>
[Route("admin/investpro")]
[FcmsAuthorize(InvestProPermissions.View)]
public class AdminInvestProController : Controller
{
    private readonly InvestProService _service;
    private readonly IFcmsLogService _log;

    public AdminInvestProController(InvestProService service, IFcmsLogService log)
    {
        _service = service;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetAllAsync(ct));

    [HttpGet("create")]
    [FcmsAuthorize(InvestProPermissions.Create)]
    public IActionResult Create()
    {
        ViewData["IsNew"] = true;
        return View("Edit", new InvestProItem { Id = Guid.Empty });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.Create)]
    public async Task<IActionResult> Create(InvestProItem model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        if (!ModelState.IsValid) { ViewData["IsNew"] = true; return View("Edit", model); }

        var saved = await _service.CreateAsync(model, ct);
        await _log.LogAsync("investpro.create", nameof(InvestProItem), saved.Id.ToString(),
            value: saved, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "InvestPro created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    [FcmsAuthorize(InvestProPermissions.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var row = await _service.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        return View(row);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.Edit)]
    public async Task<IActionResult> Edit(Guid id, InvestProItem model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        if (!ModelState.IsValid) return View(model);

        var ok = await _service.UpdateAsync(id, model.Title, model.Description, model.IsPublished, ct);
        if (!ok) return NotFound();
        await _log.LogAsync("investpro.update", nameof(InvestProItem), id.ToString(),
            value: new { id, model.Title, model.IsPublished }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "InvestPro updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (deleted is null) return Json(new { isSuccess = false, message = "Not found." });
        await _log.LogAsync("investpro.delete", nameof(InvestProItem), id.ToString(),
            value: deleted, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Deleted." });
    }
}
