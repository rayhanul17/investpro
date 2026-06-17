using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/approvals")]
[FcmsAuthorize(InvestProPermissions.ApprovalView)]
public class ApprovalController : Controller
{
    private readonly ApprovalService _service;
    private readonly InvestmentService _investments;
    private readonly IFcmsLogService _log;

    public ApprovalController(ApprovalService service, InvestmentService investments, IFcmsLogService log)
    {
        _service = service;
        _investments = investments;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var requests = await _service.GetAllPendingAsync(ct);
        var investmentIds = requests.Select(r => r.InvestmentId).Distinct().ToList();
        var investments = new Dictionary<Guid, Investment>();
        foreach (var id in investmentIds)
        {
            var inv = await _investments.GetByIdAsync(id, ct);
            if (inv is not null) investments[id] = inv;
        }
        ViewData["Investments"] = investments;
        return View(requests);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var req = await _service.GetByIdAsync(id, ct);
        if (req is null) return NotFound();
        var inv = await _investments.GetByIdAsync(req.InvestmentId, ct);
        ViewData["Investment"] = inv;
        return View(req);
    }

    public record DecideForm(DecisionKind Decision, Guid? PartnerId, string? Comment);

    [HttpPost("{id:guid}/decide")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.ApprovalDecide)]
    public async Task<IActionResult> Decide(Guid id, [FromForm] DecideForm form, CancellationToken ct)
    {
        var (ok, error, finalStatus) = await _service.DecideAsync(id, form.Decision, form.PartnerId, null, form.Comment, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Decision failed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        await _log.LogAsync($"investpro.approval.{form.Decision.ToString().ToLower()}", nameof(ApprovalRequest), id.ToString(),
            value: new { id, form.Decision, finalStatus = finalStatus.ToString() },
            module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = finalStatus switch
        {
            ApprovalRequestStatus.Approved => "Request approved.",
            ApprovalRequestStatus.Rejected => "Request rejected.",
            _ => "Decision recorded; awaiting other partners."
        };
        return RedirectToAction(nameof(Index));
    }
}
