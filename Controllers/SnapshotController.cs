using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/investments/{investmentId:guid}/snapshot")]
[FcmsAuthorize(InvestProPermissions.SnapshotView)]
public class SnapshotController : Controller
{
    private readonly CloseService _close;
    private readonly PayoutService _payouts;
    private readonly InvestmentService _investments;
    private readonly IFcmsLogService _log;

    public SnapshotController(CloseService close, PayoutService payouts, InvestmentService investments, IFcmsLogService log)
    {
        _close = close;
        _payouts = payouts;
        _investments = investments;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid investmentId, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        if (inv is null) return NotFound();
        var snap = await _close.GetSnapshotByInvestmentAsync(investmentId, ct);
        if (snap is null) return NotFound("No snapshot for this investment yet.");
        return RedirectToAction(nameof(Detail), new { investmentId, id = snap.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid investmentId, Guid id, CancellationToken ct)
    {
        var snap = await _close.GetSnapshotByIdAsync(id, ct);
        if (snap is null) return NotFound();
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        var payouts = await _payouts.GetBySnapshotAsync(id, ct);

        ViewData["Investment"] = inv;
        ViewData["Payouts"]    = payouts;
        ViewData["ChecksumOk"] = _close.VerifyChecksum(snap);
        return View(snap);
    }

    public record PayForm(PaymentMethod PaymentMethod, string? ReferenceNo, string? Notes);

    [HttpPost("{snapshotId:guid}/payouts/{payoutId:guid}/pay")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.PayoutManage)]
    public async Task<IActionResult> Pay(Guid investmentId, Guid snapshotId, Guid payoutId, [FromForm] PayForm form, CancellationToken ct)
    {
        var (ok, error) = await _payouts.MarkPaidAsync(payoutId, form.PaymentMethod, form.ReferenceNo, form.Notes, ct);
        if (!ok) { TempData["Error"] = error; }
        else
        {
            await _log.LogAsync("investpro.payout.mark-paid", nameof(Payout), payoutId.ToString(),
                value: new { investmentId, snapshotId, payoutId, form.PaymentMethod, form.ReferenceNo },
                module: InvestProModule.ModuleIdValue, ct: ct);
            TempData["Success"] = "Payout marked as paid.";
        }
        return RedirectToAction(nameof(Detail), new { investmentId, id = snapshotId });
    }
}
