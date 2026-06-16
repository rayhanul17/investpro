using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("admin/investpro/approval-config")]
[FcmsAuthorize(InvestProPermissions.ApprovalConfigView)]
public class ApprovalConfigController : Controller
{
    private readonly ApprovalConfigService _service;
    private readonly IFcmsLogService _log;

    public ApprovalConfigController(ApprovalConfigService service, IFcmsLogService log)
    {
        _service = service;
        _log = log;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetAllAsync(ct));

    public record ApprovalConfigForm(Guid Id, decimal AutoApproveBelow, decimal RequireApprovalAbove, decimal RequireAllPartnersAbove, ApproverRole ApproverRole);

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.ApprovalConfigEdit)]
    public async Task<IActionResult> Save([FromForm] List<ApprovalConfigForm> rows, CancellationToken ct)
    {
        if (rows is null || rows.Count == 0)
        {
            TempData["Error"] = "No data submitted.";
            return RedirectToAction(nameof(Index));
        }

        int updated = 0;
        foreach (var r in rows)
        {
            var ok = await _service.UpdateAsync(r.Id, r.AutoApproveBelow, r.RequireApprovalAbove, r.RequireAllPartnersAbove, r.ApproverRole, ct);
            if (ok) updated++;
        }
        await _log.LogAsync("investpro.approval-config.update", "ApprovalConfig", "bulk",
            value: new { updated, total = rows.Count }, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = $"Saved {updated} configuration row(s).";
        return RedirectToAction(nameof(Index));
    }
}
