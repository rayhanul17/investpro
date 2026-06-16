using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/investments/{investmentId:guid}/partners")]
[FcmsAuthorize(InvestProPermissions.InvestmentView)]
public class InvestmentPartnerController : Controller
{
    private readonly InvestmentService _investments;
    private readonly InvestmentPartnerService _service;
    private readonly PartnerService _partners;
    private readonly IFcmsLogService _log;

    public InvestmentPartnerController(InvestmentService investments, InvestmentPartnerService service, PartnerService partners, IFcmsLogService log)
    {
        _investments = investments;
        _service = service;
        _partners = partners;
        _log = log;
    }

    public record AddContractForm(Guid PartnerId, ContractType ContractType, PartnerRole PartnerRole,
                                  decimal AgreedCapital, decimal AgreedLaborValue,
                                  decimal ProfitSharePercent, decimal LossSharePercent,
                                  string? SpecialTerms);

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentEdit)]
    public async Task<IActionResult> Add(Guid investmentId, [FromForm] AddContractForm form, CancellationToken ct)
    {
        var contract = new InvestmentPartner
        {
            PartnerId = form.PartnerId,
            ContractType = form.ContractType,
            PartnerRole = form.PartnerRole,
            AgreedCapital = form.AgreedCapital,
            AgreedLaborValue = form.AgreedLaborValue,
            ProfitSharePercent = form.ProfitSharePercent,
            LossSharePercent = form.LossSharePercent,
            SpecialTerms = form.SpecialTerms,
        };
        var (ok, error, saved) = await _service.AddAsync(investmentId, contract, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Could not add partner.";
            return RedirectToAction("Detail", "Investment", new { id = investmentId });
        }
        await _log.LogAsync("investpro.investment.partner-add", nameof(InvestmentPartner), saved!.Id.ToString(),
            value: saved, module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Partner added to the investment.";
        return RedirectToAction("Detail", "Investment", new { id = investmentId });
    }

    public record UpdateContractForm(ContractType ContractType, PartnerRole PartnerRole,
                                     decimal AgreedCapital, decimal AgreedLaborValue,
                                     decimal ProfitSharePercent, decimal LossSharePercent,
                                     string? SpecialTerms);

    [HttpPost("{id:guid}/update")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentEdit)]
    public async Task<IActionResult> Update(Guid investmentId, Guid id, [FromForm] UpdateContractForm form, CancellationToken ct)
    {
        var input = new InvestmentPartner
        {
            ContractType = form.ContractType,
            PartnerRole = form.PartnerRole,
            AgreedCapital = form.AgreedCapital,
            AgreedLaborValue = form.AgreedLaborValue,
            ProfitSharePercent = form.ProfitSharePercent,
            LossSharePercent = form.LossSharePercent,
            SpecialTerms = form.SpecialTerms,
        };
        var (ok, error) = await _service.UpdateAsync(id, input, ct);
        if (!ok)
        {
            TempData["Error"] = error ?? "Could not update contract.";
            return RedirectToAction("Detail", "Investment", new { id = investmentId });
        }
        await _log.LogAsync("investpro.investment.partner-update", nameof(InvestmentPartner), id.ToString(),
            value: new { id, investmentId, form.ContractType, form.ProfitSharePercent, form.LossSharePercent },
            module: InvestProModule.ModuleIdValue, ct: ct);
        TempData["Success"] = "Contract updated.";
        return RedirectToAction("Detail", "Investment", new { id = investmentId });
    }

    [HttpPost("{id:guid}/remove")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.InvestmentEdit)]
    public async Task<IActionResult> Remove(Guid investmentId, Guid id, CancellationToken ct)
    {
        var (ok, error) = await _service.RemoveAsync(id, ct);
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Failed." });
        await _log.LogAsync("investpro.investment.partner-remove", nameof(InvestmentPartner), id.ToString(),
            value: new { id, investmentId }, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Removed." });
    }
}
