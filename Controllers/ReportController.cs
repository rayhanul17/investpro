using FlexCms.Framework.Auth;
using FlexCms.Framework.Documents;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace FlexCms.InvestPro.Controllers;

[Route("investpro/admin/reports")]
[FcmsAuthorize(InvestProPermissions.ReportView)]
public class ReportController : Controller
{
    private readonly ReportService _reports;
    private readonly InvestmentService _investments;
    private readonly PartnerService _partners;
    private readonly IFcmsPdfService _pdf;
    private readonly IFcmsExcelService _excel;

    public ReportController(ReportService reports, InvestmentService investments, PartnerService partners,
        IFcmsPdfService pdf, IFcmsExcelService excel)
    {
        _reports = reports;
        _investments = investments;
        _partners = partners;
        _pdf = pdf;
        _excel = excel;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Investments"] = await _investments.GetAllAsync(ct);
        ViewData["Partners"]    = await _partners.GetAllAsync(ct);
        return View();
    }

    // ── Closure Report ──────────────────────────────────────────────────

    [HttpGet("closure/{investmentId:guid}")]
    public async Task<IActionResult> Closure(Guid investmentId, string? format, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        if (inv is null) return NotFound();
        var rows = await _reports.GetClosureReportAsync(investmentId, ct);

        if (format == "csv")  return CsvDownload($"closure-{inv.Code}.csv",
            new[] { "Ledger", "Date", "Partner", "Description", "Amount", "Status" },
            rows.Select(r => new object?[] { r.LedgerType, r.TransactionDate.ToString("yyyy-MM-dd"), r.PartnerName, r.Description, r.Amount, r.ApprovalStatus }));
        if (format == "xlsx") return await ExcelDownloadAsync($"closure-{inv.Code}.xlsx", "Closure",
            new[] { "Ledger", "Date", "Partner", "Description", "Amount", "Status" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.LedgerType, r.TransactionDate, r.PartnerName, r.Description, r.Amount, r.ApprovalStatus }).ToList(),
            ct);
        if (format == "pdf")  return await PdfTableDownloadAsync($"closure-{inv.Code}.pdf",
            $"Closure Report — {inv.Code}",
            new[] { "Ledger", "Date", "Partner", "Description", "Amount", "Status" },
            rows.Select(r => new[] { r.LedgerType, r.TransactionDate.ToString("yyyy-MM-dd"), r.PartnerName, r.Description, r.Amount.ToString("N2"), r.ApprovalStatus }).ToList(),
            ct);

        ViewData["Investment"] = inv;
        ViewData["Rows"]       = rows;
        return View(rows);
    }

    // ── Lifetime Statement ──────────────────────────────────────────────

    [HttpGet("lifetime/{partnerId:guid}")]
    public async Task<IActionResult> Lifetime(Guid partnerId, string? format, CancellationToken ct)
    {
        var partner = await _partners.GetByIdAsync(partnerId, ct);
        if (partner is null) return NotFound();
        var rows = await _reports.GetLifetimeStatementAsync(partnerId, ct);

        if (format == "csv")  return CsvDownload($"lifetime-{Slug(partner.Name)}.csv",
            new[] { "Code", "Name", "Status", "Started", "Closed", "Capital", "Labor", "Profit %", "Loss %", "+Profit", "−Loss", "Settlement" },
            rows.Select(r => new object?[] { r.InvestmentCode, r.InvestmentName, r.LifecycleStatus, r.StartDate.ToString("yyyy-MM-dd"), r.ClosedAt?.ToString("yyyy-MM-dd"), r.CapitalContributed, r.LaborValueContributed, r.ProfitSharePercent, r.LossSharePercent, r.ProfitShareAmount, r.LossShareAmount, r.FinalSettlementAmount }));
        if (format == "xlsx") return await ExcelDownloadAsync($"lifetime-{Slug(partner.Name)}.xlsx", "Lifetime",
            new[] { "Code", "Name", "Status", "Started", "Closed", "Capital", "Labor", "Profit %", "Loss %", "+Profit", "−Loss", "Settlement" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.InvestmentCode, r.InvestmentName, r.LifecycleStatus, r.StartDate, r.ClosedAt, r.CapitalContributed, r.LaborValueContributed, r.ProfitSharePercent, r.LossSharePercent, r.ProfitShareAmount, r.LossShareAmount, r.FinalSettlementAmount }).ToList(),
            ct);
        if (format == "pdf")  return await PdfTableDownloadAsync($"lifetime-{Slug(partner.Name)}.pdf",
            $"Lifetime Statement — {partner.Name}",
            new[] { "Code", "Name", "Status", "Started", "Closed", "Capital", "Settlement" },
            rows.Select(r => new[] { r.InvestmentCode, r.InvestmentName, r.LifecycleStatus, r.StartDate.ToString("yyyy-MM-dd"), r.ClosedAt?.ToString("yyyy-MM-dd") ?? "—", r.CapitalContributed.ToString("N2"), r.FinalSettlementAmount?.ToString("N2") ?? "—" }).ToList(),
            ct);

        ViewData["Partner"] = partner;
        ViewData["Rows"]    = rows;
        return View(rows);
    }

    // ── Zakat Report ────────────────────────────────────────────────────

    [HttpGet("zakat")]
    public async Task<IActionResult> Zakat(int? year, string? format, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var rows = await _reports.GetZakatReportAsync(y, ct);

        if (format == "csv")  return CsvDownload($"zakat-{y}.csv",
            new[] { "Partner", "NID", "Investment", "Closed", "Capital", "Profit Share", "Zakat Base" },
            rows.Select(r => new object?[] { r.PartnerName, r.PartnerNid, r.InvestmentCode, r.ClosedAt.ToString("yyyy-MM-dd"), r.CapitalContributed, r.ProfitShareAmount, r.ZakatEligibleAmount }));
        if (format == "xlsx") return await ExcelDownloadAsync($"zakat-{y}.xlsx", $"Zakat {y}",
            new[] { "Partner", "NID", "Investment", "Closed", "Capital", "Profit Share", "Zakat Base" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.PartnerName, r.PartnerNid, r.InvestmentCode, r.ClosedAt, r.CapitalContributed, r.ProfitShareAmount, r.ZakatEligibleAmount }).ToList(),
            ct);
        if (format == "pdf")  return await PdfTableDownloadAsync($"zakat-{y}.pdf",
            $"Annual Zakat Report — {y}",
            new[] { "Partner", "NID", "Investment", "Closed", "Capital", "Profit Share", "Zakat Base" },
            rows.Select(r => new[] { r.PartnerName, r.PartnerNid ?? "", r.InvestmentCode, r.ClosedAt.ToString("yyyy-MM-dd"), r.CapitalContributed.ToString("N2"), r.ProfitShareAmount.ToString("N2"), r.ZakatEligibleAmount.ToString("N2") }).ToList(),
            ct);

        ViewData["Year"] = y;
        ViewData["Rows"] = rows;
        return View(rows);
    }

    // ── Comparison Report ───────────────────────────────────────────────

    [HttpGet("comparison/{investmentId:guid}")]
    public async Task<IActionResult> Comparison(Guid investmentId, string? format, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct);
        if (inv is null) return NotFound();
        var rows = await _reports.GetComparisonReportAsync(investmentId, ct);

        if (format == "csv")  return CsvDownload($"comparison-{inv.Code}.csv",
            new[] { "Partner", "NID", "Planned Capital", "Planned Labor", "Profit %", "Actual Capital", "Actual Labor", "+Profit", "−Loss", "Settlement" },
            rows.Select(r => new object?[] { r.PartnerName, r.PartnerNid, r.PlannedCapital, r.PlannedLaborValue, r.ProfitSharePercent, r.ActualCapitalContributed, r.ActualLaborValueContributed, r.ProfitShareAmount, r.LossShareAmount, r.FinalSettlementAmount }));
        if (format == "xlsx") return await ExcelDownloadAsync($"comparison-{inv.Code}.xlsx", "Comparison",
            new[] { "Partner", "NID", "Planned Capital", "Planned Labor", "Profit %", "Actual Capital", "Actual Labor", "+Profit", "−Loss", "Settlement" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.PartnerName, r.PartnerNid, r.PlannedCapital, r.PlannedLaborValue, r.ProfitSharePercent, r.ActualCapitalContributed, r.ActualLaborValueContributed, r.ProfitShareAmount, r.LossShareAmount, r.FinalSettlementAmount }).ToList(),
            ct);
        if (format == "pdf")  return await PdfTableDownloadAsync($"comparison-{inv.Code}.pdf",
            $"Comparison Report — {inv.Code}",
            new[] { "Partner", "Planned Cap", "Profit %", "Actual Cap", "+Profit", "−Loss", "Settlement" },
            rows.Select(r => new[] { r.PartnerName, r.PlannedCapital.ToString("N2"), r.ProfitSharePercent.ToString("0.##"), r.ActualCapitalContributed.ToString("N2"), r.ProfitShareAmount?.ToString("N2") ?? "—", r.LossShareAmount?.ToString("N2") ?? "—", r.FinalSettlementAmount?.ToString("N2") ?? "—" }).ToList(),
            ct);

        ViewData["Investment"] = inv;
        ViewData["Rows"]       = rows;
        return View(rows);
    }

    // ── Download helpers ────────────────────────────────────────────────

    private FileContentResult CsvDownload(string fileName, IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.Select(c => CsvEscape(c?.ToString() ?? ""))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", fileName);
    }

    private async Task<FileContentResult> ExcelDownloadAsync(string fileName, string sheet, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, CancellationToken ct)
    {
        var bytes = await _excel.RenderTableAsync(sheet, headers, rows, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<FileContentResult> PdfTableDownloadAsync(string fileName, string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, CancellationToken ct)
    {
        var bytes = await _pdf.RenderTableAsync(title, headers, rows, ct);
        return File(bytes, "application/pdf", fileName);
    }

    private static string CsvEscape(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    private static string Slug(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "row";
        return new string(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
    }
}
