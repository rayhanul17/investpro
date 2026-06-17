using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/investments")]
[FcmsAuthorize(InvestProPermissions.InvestmentView)]
public class InvestmentController : Controller
{
    private readonly InvestmentService _service;
    private readonly InvestmentPartnerService _partners;
    private readonly IFcmsLogService _log;

    public InvestmentController(InvestmentService service, InvestmentPartnerService partners, IFcmsLogService log)
    {
        _service = service;
        _partners = partners;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetAllAsync(ct));

    [HttpGet("create")]
    [FcmsAuthorize(InvestProPermissions.InvestmentCreate)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["IsNew"] = true;
        var suggested = await _service.SuggestCodeAsync(ct);
        return View("Edit", new Investment
        {
            Id = Guid.Empty,
            Code = suggested,
            StartDate = DateTime.UtcNow.Date,
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentCreate)]
    public async Task<IActionResult> Create(Investment model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (!ModelState.IsValid) { ViewData["IsNew"] = true; return View("Edit", model); }

        var (ok, error, saved) = await _service.CreateAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create.");
            ViewData["IsNew"] = true;
            return View("Edit", model);
        }

        await _log.LogAsync("investpro.investment.create", nameof(Investment), saved!.Id.ToString(),
            value: saved, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = $"Investment '{saved.Code}' created. Add partner contracts next.";
        return RedirectToAction(nameof(Detail), new { id = saved.Id });
    }

    [HttpGet("{id:guid}/edit")]
    [FcmsAuthorize(InvestProPermissions.InvestmentEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var row = await _service.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        if (row.LifecycleStatus != InvestmentLifecycle.Draft)
        {
            TempData["Error"] = "Only Draft investments can be edited.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        return View(row);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentEdit)]
    public async Task<IActionResult> Edit(Guid id, Investment model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (!ModelState.IsValid) return View(model);

        var (ok, error) = await _service.UpdateAsync(id, model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update.");
            return View(model);
        }

        await _log.LogAsync("investpro.investment.update", nameof(Investment), id.ToString(),
            value: new { id, model.Code, model.Name }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Investment updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, [FromServices] PartnerService partnerPool, [FromServices] ReopenRequestService reopens, CancellationToken ct)
    {
        var inv = await _service.GetByIdAsync(id, ct);
        if (inv is null) return NotFound();
        var contracts = await _partners.GetByInvestmentAsync(id, ct);
        ViewData["Partners"] = contracts;
        var allPartners = await partnerPool.GetAllAsync(ct);
        var alreadyJoined = contracts.Select(c => c.PartnerId).ToHashSet();
        ViewData["AvailablePartners"] = allPartners.Where(p => p.IsActive && !alreadyJoined.Contains(p.Id)).ToList();

        // Post-reopen Active investments need the contract editor + Close
        // button visible again. Signal: an Approved reopen request exists
        // for this investment, meaning the reclose hasn't happened yet.
        var isPostReopen = inv.LifecycleStatus == InvestmentLifecycle.Active
                           && await reopens.HasApprovedForInvestmentAsync(id, ct);
        ViewData["IsContractEditable"] = inv.LifecycleStatus == InvestmentLifecycle.Draft || isPostReopen;
        ViewData["IsPostReopen"]       = isPostReopen;
        return View(inv);
    }

    [HttpPost("{id:guid}/activate")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var (ok, error) = await _service.ActivateAsync(id, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Activation failed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        await _log.LogAsync("investpro.investment.activate", nameof(Investment), id.ToString(),
            value: new { id }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Investment activated. Ledger entries are now allowed.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (ok, error, deleted) = await _service.DeleteAsync(id, ct);
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Failed." });
        await _log.LogAsync("investpro.investment.delete", nameof(Investment), id.ToString(),
            value: deleted!, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Deleted." });
    }
}
