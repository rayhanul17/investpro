using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/investments/{investmentId:guid}/reopen")]
[FcmsAuthorize(InvestProPermissions.InvestmentView)]
public class ReopenController : Controller
{
    private readonly ReopenService _reopen;
    private readonly InvestmentService _investments;
    private readonly InvestmentPartnerService _contracts;
    private readonly IFcmsLogService _log;

    public ReopenController(ReopenService reopen, InvestmentService investments, InvestmentPartnerService contracts, IFcmsLogService log)
    {
        _reopen = reopen;
        _investments = investments;
        _contracts = contracts;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid investmentId, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        if (inv is null) return NotFound();
        var requests = await _reopen.GetByInvestmentAsync(investmentId, ct);
        var contracts = await _contracts.GetByInvestmentAsync(investmentId, ct);

        ViewData["Investment"] = inv;
        ViewData["Contracts"]  = contracts;
        return View(requests);
    }

    [HttpPost("request")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.ReopenRequest)]
    public async Task<IActionResult> RequestReopen(Guid investmentId, [FromForm] string reason, CancellationToken ct)
    {
        var (ok, error, req) = await _reopen.RequestReopenAsync(investmentId, Guid.Empty, reason ?? "", ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Could not start reopen.";
            return RedirectToAction(nameof(Index), new { investmentId });
        }
        await _log.LogAsync("investpro.reopen.request", nameof(ReopenRequest), req!.Id.ToString(),
            value: new { investmentId, req.Id, reason = req.Reason },
            module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Reopen request created. All partners must approve to unfreeze the contract.";
        return RedirectToAction(nameof(Index), new { investmentId });
    }

    public record DecideForm(Guid PartnerId, DecisionKind Decision, string? Comment);

    [HttpPost("{requestId:guid}/decide")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.ReopenDecide)]
    public async Task<IActionResult> Decide(Guid investmentId, Guid requestId, [FromForm] DecideForm form, CancellationToken ct)
    {
        var (ok, error, finalStatus, transitioned) = await _reopen.DecideAsync(requestId, form.PartnerId, form.Decision, form.Comment, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Decision failed.";
            return RedirectToAction(nameof(Index), new { investmentId });
        }
        await _log.LogAsync($"investpro.reopen.{form.Decision.ToString().ToLower()}", nameof(ReopenRequest), requestId.ToString(),
            value: new { investmentId, requestId, form.PartnerId, form.Decision, finalStatus = finalStatus.ToString(), transitioned },
            module: InvestProModule.ModuleIdValue, ct: ct);

        if (transitioned)
        {
            TempData["Success"] = "Reopen approved — investment is now Active. Edit the contracts and reclose to issue the corrected snapshot.";
            return RedirectToAction("Detail", "Investment", new { id = investmentId });
        }

        TempData["Success"] = finalStatus switch
        {
            ReopenRequestStatus.Rejected => "Reopen request rejected — investment stays Closed.",
            _ => "Decision recorded; awaiting other partners."
        };
        return RedirectToAction(nameof(Index), new { investmentId });
    }
}
