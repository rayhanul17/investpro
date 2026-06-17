using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/approvals")]
[FcmsAuthorize(InvestProPermissions.ApprovalView)]
public class ApprovalController : InvestProControllerBase
{
    private readonly ApprovalService _service;
    private readonly InvestmentService _investments;
    private readonly PartnerService _partners;
    private readonly IFcmsLogService _log;

    public ApprovalController(ApprovalService service, InvestmentService investments, PartnerService partners, IFcmsLogService log)
    {
        _service = service;
        _investments = investments;
        _partners = partners;
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
        // For all-partner approvals we have to bind the calling user to a
        // partner row before recording the vote (defeats vote-as-another).
        // SingleApprover requests don't carry a PartnerId at all — anyone
        // with the ApprovalDecide permission can act.
        var req = await _service.GetByIdAsync(id, ct);
        if (req is null) return NotFound();

        Guid? actingPartnerId = null;
        if (req.RequiredApproverMode == ApproverMode.AllPartners)
        {
            var (resolved, partnerErr) = await ResolveActingPartnerAsync(_partners, form.PartnerId, ct);
            if (resolved is null)
            {
                TempData["Error"] = partnerErr;
                return RedirectToAction(nameof(Detail), new { id });
            }
            actingPartnerId = resolved;
        }

        var (ok, error, finalStatus) = await _service.DecideAsync(
            id, form.Decision, actingPartnerId, CurrentUserId(), form.Comment, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Decision failed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        await _log.LogAsync($"investpro.approval.{form.Decision.ToString().ToLower()}", nameof(ApprovalRequest), id.ToString(),
            value: new { id, form.Decision, finalStatus = finalStatus.ToString(), actingPartnerId, decidedByUser = CurrentUserId() },
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
