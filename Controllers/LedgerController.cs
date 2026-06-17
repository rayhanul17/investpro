using FlexCms.Framework.Auth;
using FlexCms.Framework.Cms;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

/// <summary>
/// All four ledger types (Capital / Labor / Expense / Revenue) share their
/// CRUD surface through this single controller. The path's <c>kind</c>
/// segment selects which service+view set runs — keeps the routing flat
/// while letting each ledger keep its own typed entity + view files.
///
/// Mutations are gated on <see cref="InvestProPermissions.LedgerWrite"/>;
/// the class-level <see cref="InvestProPermissions.LedgerView"/> allows
/// list / detail reads.
/// </summary>
[Route("investpro/admin/investments/{investmentId:guid}/ledger/{kind}")]
[FcmsAuthorize(InvestProPermissions.LedgerView)]
public class LedgerController : Controller
{
    private readonly CapitalContributionService _capital;
    private readonly LaborContributionService _labor;
    private readonly ExpenseEntryService _expense;
    private readonly RevenueEntryService _revenue;
    private readonly InvestmentService _investments;
    private readonly PartnerService _partners;
    private readonly ExpenseCategoryService _categories;
    private readonly AttachmentService _attachments;
    private readonly ApprovalService _approvals;
    private readonly IFcmsLogService _log;

    public LedgerController(CapitalContributionService capital, LaborContributionService labor,
        ExpenseEntryService expense, RevenueEntryService revenue,
        InvestmentService investments, PartnerService partners, ExpenseCategoryService categories,
        AttachmentService attachments, ApprovalService approvals, IFcmsLogService log)
    {
        _capital = capital; _labor = labor; _expense = expense; _revenue = revenue;
        _investments = investments; _partners = partners; _categories = categories;
        _attachments = attachments; _approvals = approvals; _log = log;
    }

    private static LedgerKind Parse(string kind) => kind.ToLowerInvariant() switch
    {
        "capital" => LedgerKind.Capital,
        "labor"   => LedgerKind.Labor,
        "expense" => LedgerKind.Expense,
        "revenue" => LedgerKind.Revenue,
        _ => throw new ArgumentException($"Unknown ledger kind: {kind}", nameof(kind))
    };

    private async Task<(Investment? inv, List<InvestmentPartner> contracts)> LoadContextAsync(Guid investmentId, CancellationToken ct)
    {
        var inv = await _investments.GetByIdAsync(investmentId, ct, withPartners: true);
        var contracts = inv?.PartnerContracts.ToList() ?? new();
        return (inv, contracts);
    }

    // ── List ──────────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid investmentId, string kind, CancellationToken ct)
    {
        var k = Parse(kind);
        var (inv, _) = await LoadContextAsync(investmentId, ct);
        if (inv is null) return NotFound();

        ViewData["Investment"] = inv;
        ViewData["Kind"]       = k;

        return k switch
        {
            LedgerKind.Capital => View("Index", await _capital.GetByInvestmentAsync(investmentId, ct) as IEnumerable<LedgerEntryBase>),
            LedgerKind.Labor   => View("Index", await _labor.GetByInvestmentAsync(investmentId, ct) as IEnumerable<LedgerEntryBase>),
            LedgerKind.Expense => View("Index", await _expense.GetByInvestmentAsync(investmentId, ct) as IEnumerable<LedgerEntryBase>),
            LedgerKind.Revenue => View("Index", await _revenue.GetByInvestmentAsync(investmentId, ct) as IEnumerable<LedgerEntryBase>),
            _ => NotFound()
        };
    }

    // ── Create ────────────────────────────────────────────────────────────

    [HttpGet("create")]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    public async Task<IActionResult> Create(Guid investmentId, string kind, CancellationToken ct)
    {
        var k = Parse(kind);
        var (inv, contracts) = await LoadContextAsync(investmentId, ct);
        if (inv is null) return NotFound();

        ViewData["Investment"]    = inv;
        ViewData["Kind"]          = k;
        ViewData["IsNew"]         = true;
        ViewData["PartnerOptions"] = contracts.Select(c => (Id: c.PartnerId, Name: c.Partner?.Name ?? "—")).ToList();
        if (k == LedgerKind.Expense)
            ViewData["CategoryOptions"] = (await _categories.GetAllAsync(ct)).Where(c => c.IsActive).ToList();

        return View("Edit", BlankModel(k, investmentId));
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    [RequestSizeLimit(220 * 1024 * 1024)]
    public async Task<IActionResult> CreatePost(Guid investmentId, string kind, CancellationToken ct)
    {
        var k = Parse(kind);

        (bool ok, string? error, Guid newId) result = k switch
        {
            LedgerKind.Capital => await CreateCapital(investmentId, ct),
            LedgerKind.Labor   => await CreateLabor(investmentId, ct),
            LedgerKind.Expense => await CreateExpense(investmentId, ct),
            LedgerKind.Revenue => await CreateRevenue(investmentId, ct),
            _ => (false, "Unknown ledger.", Guid.Empty)
        };

        if (!result.ok)
        {
            TempData["Error"] = result.error ?? "Failed.";
            return RedirectToAction(nameof(Index), new { investmentId, kind });
        }

        await HandleUploadsAsync(investmentId, k, result.newId, ct);

        decimal amount = decimal.TryParse(Request.Form["Amount"], out var amt) ? amt : 0m;
        if (k == LedgerKind.Labor)
        {
            var hours = decimal.TryParse(Request.Form["HoursOrDays"], out var h) ? h : 0m;
            var rate = decimal.TryParse(Request.Form["RatePerUnit"], out var r) ? r : 0m;
            amount = hours * rate;
        }

        var (mode, role, _) = await _approvals.ResolveModeAsync(k, amount, ct);
        if (mode != ApproverMode.Auto)
        {
            await _approvals.CreateRequestAsync(investmentId, k, result.newId, amount, mode, role, ct);
            await SetLedgerStatusPendingAsync(k, result.newId, ct);
            TempData["Success"] = $"Entry recorded. Waiting for {(mode == ApproverMode.AllPartners ? "all partners" : "approver")} to decide.";
        }
        else
        {
            TempData["Success"] = "Entry recorded.";
        }

        await _log.LogAsync($"investpro.ledger.{kind}.create", k.ToString(), result.newId.ToString(),
            value: new { investmentId, kind, amount, approvalMode = mode.ToString() },
            module: InvestProModule.ModuleIdValue, ct: ct);

        return RedirectToAction(nameof(Index), new { investmentId, kind });
    }

    private async Task SetLedgerStatusPendingAsync(LedgerKind kind, Guid entryId, CancellationToken ct)
    {
        await _approvals.SetLedgerEntryStatusAsync(kind, entryId, LedgerApprovalStatus.Pending, ct);
    }

    // ── Edit ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/edit")]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    public async Task<IActionResult> Edit(Guid investmentId, string kind, Guid id, CancellationToken ct)
    {
        var k = Parse(kind);
        var (inv, contracts) = await LoadContextAsync(investmentId, ct);
        if (inv is null) return NotFound();

        LedgerEntryBase? entry = k switch
        {
            LedgerKind.Capital => await _capital.GetByIdAsync(id, ct),
            LedgerKind.Labor   => await _labor.GetByIdAsync(id, ct),
            LedgerKind.Expense => await _expense.GetByIdAsync(id, ct),
            LedgerKind.Revenue => await _revenue.GetByIdAsync(id, ct),
            _ => null
        };
        if (entry is null) return NotFound();

        ViewData["Investment"]     = inv;
        ViewData["Kind"]           = k;
        ViewData["IsNew"]          = false;
        ViewData["PartnerOptions"] = contracts.Select(c => (Id: c.PartnerId, Name: c.Partner?.Name ?? "—")).ToList();
        ViewData["Attachments"]    = await _attachments.GetForEntryAsync(k, id, ct);
        if (k == LedgerKind.Expense)
            ViewData["CategoryOptions"] = (await _categories.GetAllAsync(ct)).Where(c => c.IsActive).ToList();

        return View("Edit", entry);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    [RequestSizeLimit(220 * 1024 * 1024)]
    public async Task<IActionResult> EditPost(Guid investmentId, string kind, Guid id, CancellationToken ct)
    {
        var k = Parse(kind);

        (bool ok, string? error) result = k switch
        {
            LedgerKind.Capital => await UpdateCapital(id, ct),
            LedgerKind.Labor   => await UpdateLabor(id, ct),
            LedgerKind.Expense => await UpdateExpense(id, ct),
            LedgerKind.Revenue => await UpdateRevenue(id, ct),
            _ => (false, "Unknown ledger.")
        };

        if (!result.ok)
        {
            TempData["Error"] = result.error ?? "Failed.";
            return RedirectToAction(nameof(Edit), new { investmentId, kind, id });
        }

        await HandleUploadsAsync(investmentId, k, id, ct);
        await _log.LogAsync($"investpro.ledger.{kind}.update", k.ToString(), id.ToString(),
            value: new { investmentId, kind }, module: InvestProModule.ModuleIdValue, ct: ct);

        TempData["Success"] = "Entry updated.";
        return RedirectToAction(nameof(Index), new { investmentId, kind });
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    public async Task<IActionResult> Delete(Guid investmentId, string kind, Guid id, CancellationToken ct)
    {
        var k = Parse(kind);
        (bool ok, string? error) = k switch
        {
            LedgerKind.Capital => await DropCapital(id, ct),
            LedgerKind.Labor   => await DropLabor(id, ct),
            LedgerKind.Expense => await DropExpense(id, ct),
            LedgerKind.Revenue => await DropRevenue(id, ct),
            _ => (false, "Unknown ledger.")
        };
        if (!ok) return Json(new { isSuccess = false, message = error ?? "Failed." });
        await _log.LogAsync($"investpro.ledger.{kind}.delete", k.ToString(), id.ToString(),
            value: new { investmentId, kind }, module: InvestProModule.ModuleIdValue, ct: ct);
        return Json(new { isSuccess = true, message = "Deleted." });
    }

    // ── Attachment endpoints ──────────────────────────────────────────────

    [HttpPost("{id:guid}/attachments/{aid:guid}/delete")]
    [ValidateAntiForgeryToken]
    [FcmsAuthorize(InvestProPermissions.LedgerWrite)]
    public async Task<IActionResult> DeleteAttachment(Guid investmentId, string kind, Guid id, Guid aid, CancellationToken ct)
    {
        var (ok, error, _) = await _attachments.DeleteAsync(aid, ct);
        return Json(new { isSuccess = ok, message = ok ? "Removed." : (error ?? "Failed.") });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static LedgerEntryBase BlankModel(LedgerKind k, Guid invId) => k switch
    {
        LedgerKind.Capital => new CapitalContribution { InvestmentId = invId, TransactionDate = DateTime.UtcNow.Date },
        LedgerKind.Labor   => new LaborContribution { InvestmentId = invId, TransactionDate = DateTime.UtcNow.Date, UnitType = LaborUnitType.Hours, RatePerUnit = 0 },
        LedgerKind.Expense => new Expense { InvestmentId = invId, TransactionDate = DateTime.UtcNow.Date },
        LedgerKind.Revenue => new Revenue { InvestmentId = invId, TransactionDate = DateTime.UtcNow.Date },
        _ => throw new InvalidOperationException()
    };

    private static decimal D(IFormCollection form, string k, decimal fallback = 0)
        => decimal.TryParse(form[k], out var v) ? v : fallback;
    private static DateTime DT(IFormCollection form, string k)
        => DateTime.TryParse(form[k], out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.UtcNow;
    private static T Enm<T>(IFormCollection form, string k, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(form[k], out var e) ? e : fallback;
    private static Guid G(IFormCollection form, string k)
        => Guid.TryParse(form[k], out var g) ? g : Guid.Empty;
    private static string? S(IFormCollection form, string k)
    {
        var v = form[k].ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private async Task<(bool ok, string? error, Guid newId)> CreateCapital(Guid invId, CancellationToken ct)
    {
        var m = new CapitalContribution
        {
            PartnerId        = G(Request.Form, "PartnerId"),
            Amount           = D(Request.Form, "Amount"),
            TransactionDate  = DT(Request.Form, "TransactionDate"),
            Description      = S(Request.Form, "Description") ?? "",
            Details          = S(Request.Form, "Details"),
            ContributionType = Enm(Request.Form, "ContributionType", ContributionType.Cash),
            AssetDescription = S(Request.Form, "AssetDescription"),
            PaymentMethod    = Enm(Request.Form, "PaymentMethod", PaymentMethod.Cash),
            ReferenceNo      = S(Request.Form, "ReferenceNo"),
            Notes            = S(Request.Form, "Notes"),
        };
        var (ok, err, saved) = await _capital.CreateAsync(invId, m, ct);
        return (ok, err, saved?.Id ?? Guid.Empty);
    }

    private async Task<(bool ok, string? error, Guid newId)> CreateLabor(Guid invId, CancellationToken ct)
    {
        var m = new LaborContribution
        {
            PartnerId       = G(Request.Form, "PartnerId"),
            TransactionDate = DT(Request.Form, "TransactionDate"),
            Description     = S(Request.Form, "Description") ?? "",
            Details         = S(Request.Form, "Details"),
            UnitType        = Enm(Request.Form, "UnitType", LaborUnitType.Hours),
            HoursOrDays     = D(Request.Form, "HoursOrDays"),
            RatePerUnit     = D(Request.Form, "RatePerUnit"),
            TaskType        = S(Request.Form, "TaskType"),
            WorkDescription = S(Request.Form, "WorkDescription"),
            Notes           = S(Request.Form, "Notes"),
        };
        var (ok, err, saved) = await _labor.CreateAsync(invId, m, ct);
        return (ok, err, saved?.Id ?? Guid.Empty);
    }

    private async Task<(bool ok, string? error, Guid newId)> CreateExpense(Guid invId, CancellationToken ct)
    {
        var m = new Expense
        {
            PartnerId         = G(Request.Form, "PartnerId"),
            Amount            = D(Request.Form, "Amount"),
            TransactionDate   = DT(Request.Form, "TransactionDate"),
            Description       = S(Request.Form, "Description") ?? "",
            Details           = S(Request.Form, "Details"),
            ExpenseCategoryId = G(Request.Form, "ExpenseCategoryId"),
            PaidTo            = S(Request.Form, "PaidTo"),
            PaymentMethod     = Enm(Request.Form, "PaymentMethod", PaymentMethod.Cash),
            ReceiptNo         = S(Request.Form, "ReceiptNo"),
            Notes             = S(Request.Form, "Notes"),
        };
        var (ok, err, saved) = await _expense.CreateAsync(invId, m, ct);
        return (ok, err, saved?.Id ?? Guid.Empty);
    }

    private async Task<(bool ok, string? error, Guid newId)> CreateRevenue(Guid invId, CancellationToken ct)
    {
        var m = new Revenue
        {
            PartnerId       = G(Request.Form, "PartnerId"),
            Amount          = D(Request.Form, "Amount"),
            TransactionDate = DT(Request.Form, "TransactionDate"),
            Description     = S(Request.Form, "Description") ?? "",
            Details         = S(Request.Form, "Details"),
            SourceType      = Enm(Request.Form, "SourceType", RevenueSourceType.Sales),
            Customer        = S(Request.Form, "Customer"),
            SalesChannel    = S(Request.Form, "SalesChannel"),
            InvoiceNo       = S(Request.Form, "InvoiceNo"),
            Notes           = S(Request.Form, "Notes"),
        };
        var (ok, err, saved) = await _revenue.CreateAsync(invId, m, ct);
        return (ok, err, saved?.Id ?? Guid.Empty);
    }

    private async Task<(bool ok, string? error)> UpdateCapital(Guid id, CancellationToken ct)
    {
        var m = new CapitalContribution
        {
            PartnerId        = G(Request.Form, "PartnerId"),
            Amount           = D(Request.Form, "Amount"),
            TransactionDate  = DT(Request.Form, "TransactionDate"),
            Description      = S(Request.Form, "Description") ?? "",
            Details          = S(Request.Form, "Details"),
            ContributionType = Enm(Request.Form, "ContributionType", ContributionType.Cash),
            AssetDescription = S(Request.Form, "AssetDescription"),
            PaymentMethod    = Enm(Request.Form, "PaymentMethod", PaymentMethod.Cash),
            ReferenceNo      = S(Request.Form, "ReferenceNo"),
            Notes            = S(Request.Form, "Notes"),
        };
        return await _capital.UpdateAsync(id, m, ct);
    }

    private async Task<(bool ok, string? error)> UpdateLabor(Guid id, CancellationToken ct)
    {
        var m = new LaborContribution
        {
            PartnerId       = G(Request.Form, "PartnerId"),
            TransactionDate = DT(Request.Form, "TransactionDate"),
            Description     = S(Request.Form, "Description") ?? "",
            Details         = S(Request.Form, "Details"),
            UnitType        = Enm(Request.Form, "UnitType", LaborUnitType.Hours),
            HoursOrDays     = D(Request.Form, "HoursOrDays"),
            RatePerUnit     = D(Request.Form, "RatePerUnit"),
            TaskType        = S(Request.Form, "TaskType"),
            WorkDescription = S(Request.Form, "WorkDescription"),
            Notes           = S(Request.Form, "Notes"),
        };
        return await _labor.UpdateAsync(id, m, ct);
    }

    private async Task<(bool ok, string? error)> UpdateExpense(Guid id, CancellationToken ct)
    {
        var m = new Expense
        {
            PartnerId         = G(Request.Form, "PartnerId"),
            Amount            = D(Request.Form, "Amount"),
            TransactionDate   = DT(Request.Form, "TransactionDate"),
            Description       = S(Request.Form, "Description") ?? "",
            Details           = S(Request.Form, "Details"),
            ExpenseCategoryId = G(Request.Form, "ExpenseCategoryId"),
            PaidTo            = S(Request.Form, "PaidTo"),
            PaymentMethod     = Enm(Request.Form, "PaymentMethod", PaymentMethod.Cash),
            ReceiptNo         = S(Request.Form, "ReceiptNo"),
            Notes             = S(Request.Form, "Notes"),
        };
        return await _expense.UpdateAsync(id, m, ct);
    }

    private async Task<(bool ok, string? error)> UpdateRevenue(Guid id, CancellationToken ct)
    {
        var m = new Revenue
        {
            PartnerId       = G(Request.Form, "PartnerId"),
            Amount          = D(Request.Form, "Amount"),
            TransactionDate = DT(Request.Form, "TransactionDate"),
            Description     = S(Request.Form, "Description") ?? "",
            Details         = S(Request.Form, "Details"),
            SourceType      = Enm(Request.Form, "SourceType", RevenueSourceType.Sales),
            Customer        = S(Request.Form, "Customer"),
            SalesChannel    = S(Request.Form, "SalesChannel"),
            InvoiceNo       = S(Request.Form, "InvoiceNo"),
            Notes           = S(Request.Form, "Notes"),
        };
        return await _revenue.UpdateAsync(id, m, ct);
    }

    private async Task<(bool ok, string? error)> DropCapital(Guid id, CancellationToken ct)
    { var (ok, e, _) = await _capital.DeleteAsync(id, ct); return (ok, e); }
    private async Task<(bool ok, string? error)> DropLabor(Guid id, CancellationToken ct)
    { var (ok, e, _) = await _labor.DeleteAsync(id, ct); return (ok, e); }
    private async Task<(bool ok, string? error)> DropExpense(Guid id, CancellationToken ct)
    { var (ok, e, _) = await _expense.DeleteAsync(id, ct); return (ok, e); }
    private async Task<(bool ok, string? error)> DropRevenue(Guid id, CancellationToken ct)
    { var (ok, e, _) = await _revenue.DeleteAsync(id, ct); return (ok, e); }

    private async Task HandleUploadsAsync(Guid investmentId, LedgerKind kind, Guid entryId, CancellationToken ct)
    {
        if (Request.Form.Files.Count == 0) return;
        var labels = Request.Form["AttachmentLabels"];
        for (int i = 0; i < Request.Form.Files.Count; i++)
        {
            var f = Request.Form.Files[i];
            var labelStr = i < labels.Count ? labels[i] : "Other";
            var label = Enum.TryParse<AttachmentLabel>(labelStr, out var lbl) ? lbl : AttachmentLabel.Other;
            await _attachments.UploadAsync(investmentId, kind, entryId, f, label, ct);
        }
    }
}
