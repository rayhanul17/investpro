using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/investments/{investmentId:guid}/close")]
[FcmsAuthorize(InvestProPermissions.InvestmentView)]
public class CloseController : InvestProControllerBase
{
    private readonly CloseService _close;
    private readonly InvestmentService _investments;
    private readonly InvestmentPartnerService _contracts;
    private readonly PartnerService _partners;
    private readonly IFcmsLogService _log;

    public CloseController(CloseService close, InvestmentService investments, InvestmentPartnerService contracts, PartnerService partners, IFcmsLogService log)
    {
        _close = close;
        _investments = investments;
        _contracts = contracts;
        _partners = partners;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid investmentId, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        if (inv is null) return NotFound();
        var requests = await _close.GetByInvestmentAsync(investmentId, ct);
        var contracts = await _contracts.GetByInvestmentAsync(investmentId, ct);

        ViewData["Investment"] = inv;
        ViewData["Contracts"]  = contracts;
        return View(requests);
    }

    [HttpPost("request")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.CloseRequest)]
    public async Task<IActionResult> RequestClose(Guid investmentId, [FromForm] string? notes, CancellationToken ct)
    {
        var initiatedBy = CurrentUserId() ?? Guid.Empty;
        var (ok, error, req) = await _close.RequestCloseAsync(investmentId, initiatedBy, notes, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Could not start close.";
            return RedirectToAction("Detail", "Investment", new { id = investmentId });
        }
        await _log.LogAsync("investpro.close.request", nameof(CloseRequest), req!.Id.ToString(),
            value: new { investmentId, req.Id, initiatedBy }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Close request created. All partners must approve to finalise.";
        return RedirectToAction(nameof(Index), new { investmentId });
    }

    public record DecideForm(Guid? PartnerId, DecisionKind Decision, string? Comment);

    [HttpPost("{requestId:guid}/decide")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.CloseDecide)]
    public async Task<IActionResult> Decide(Guid investmentId, Guid requestId, [FromForm] DecideForm form, CancellationToken ct)
    {
        var (actingPartnerId, partnerErr) = await ResolveActingPartnerAsync(_partners, form.PartnerId, ct);
        if (actingPartnerId is null)
        {
            TempData["Error"] = partnerErr;
            return RedirectToAction(nameof(Index), new { investmentId });
        }

        var (ok, error, finalStatus, snapshotId) = await _close.DecideAsync(
            requestId, actingPartnerId.Value, form.Decision, form.Comment, CurrentUserId(), ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Decision failed.";
            return RedirectToAction(nameof(Index), new { investmentId });
        }
        await _log.LogAsync($"investpro.close.{form.Decision.ToString().ToLower()}", nameof(CloseRequest), requestId.ToString(),
            value: new { investmentId, requestId, actingPartnerId, form.Decision, finalStatus = finalStatus.ToString(), snapshotId, decidedByUser = CurrentUserId() },
            module: InvestProModule.ModuleIdValue, ct: ct);

        if (snapshotId.HasValue)
        {
            TempData["Success"] = "Investment closed. Snapshot generated.";
            return RedirectToAction("Detail", "Snapshot", new { investmentId, id = snapshotId.Value });
        }

        TempData["Success"] = finalStatus switch
        {
            CloseRequestStatus.Rejected => "Close request rejected — investment is back to Active.",
            _ => "Decision recorded; awaiting other partners."
        };
        return RedirectToAction(nameof(Index), new { investmentId });
    }
}
