using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/expense-categories")]
[FcmsAuthorize(InvestProPermissions.CategoryView)]
public class ExpenseCategoryController : Controller
{
    private readonly ExpenseCategoryService _service;
    private readonly IFcmsLogService _log;

    public ExpenseCategoryController(ExpenseCategoryService service, IFcmsLogService log)
    {
        _service = service;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetAllAsync(ct));

    [HttpGet("create")]
    [FcmsAuthorize(InvestProPermissions.CategoryCreate)]
    public IActionResult Create()
    {
        ViewData["IsNew"] = true;
        return View("Edit", new ExpenseCategory { Id = Guid.Empty, IsActive = true, SortOrder = 500 });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.CategoryCreate)]
    public async Task<IActionResult> Create(ExpenseCategory model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (!ModelState.IsValid) { ViewData["IsNew"] = true; return View("Edit", model); }

        var saved = await _service.CreateAsync(model, ct);
        await _log.LogAsync("investpro.category.create", nameof(ExpenseCategory), saved.Id.ToString(),
            value: saved, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    [FcmsAuthorize(InvestProPermissions.CategoryEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var row = await _service.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        return View(row);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.CategoryEdit)]
    public async Task<IActionResult> Edit(Guid id, ExpenseCategory model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (!ModelState.IsValid) return View(model);

        var ok = await _service.UpdateAsync(id, model.Name, model.Description, model.IsActive, model.SortOrder, ct);
        if (!ok) return NotFound();
        await _log.LogAsync("investpro.category.update", nameof(ExpenseCategory), id.ToString(),
            value: new { id, model.Name, model.IsActive }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.CategoryDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (ok, error, deleted) = await _service.DeleteAsync(id, ct);
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Failed." });
        await _log.LogAsync("investpro.category.delete", nameof(ExpenseCategory), id.ToString(),
            value: deleted!, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Deleted." });
    }
}
