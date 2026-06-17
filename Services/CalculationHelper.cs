using FlexCms.InvestPro.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FlexCms.InvestPro.Services;

/// <summary>
/// All money / share / zakat math lives here. Each method is a pure function
/// taking primitive inputs and returning a single decimal (or a small record)
/// so the calculation is easy to read, easy to step through in the debugger,
/// and easy to swap when Shariah rules need to be refined.
///
/// <para>
/// We deliberately avoid LINQ-heavy expression trees inside formulas. Every
/// step is a separate, named, individually-testable method — read the
/// snapshot generator top-to-bottom and the math reads like a recipe.
/// </para>
/// </summary>
public static class CalculationHelper
{
    /// <summary>2-decimal banker's rounding (matches Postgres numeric(18,2) storage).</summary>
    public static decimal RoundMoney(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>4-decimal rounding for percentage values (matches numeric(7,4) storage).</summary>
    public static decimal RoundPercent(decimal percent)
        => Math.Round(percent, 4, MidpointRounding.AwayFromZero);

    // ── Step 1: Totals ──────────────────────────────────────────────────

    /// <summary>Sum of approved-only revenue entries. Rejected and Pending entries are excluded from final totals.</summary>
    public static decimal CalcGrossRevenue(IEnumerable<Revenue> revenues)
    {
        decimal total = 0m;
        foreach (var r in revenues)
        {
            if (r.ApprovalStatus == LedgerApprovalStatus.Rejected) continue;
            if (r.ApprovalStatus == LedgerApprovalStatus.Pending)  continue;
            total += r.Amount;
        }
        return RoundMoney(total);
    }

    /// <summary>Sum of approved-only expense entries.</summary>
    public static decimal CalcGrossExpense(IEnumerable<Expense> expenses)
    {
        decimal total = 0m;
        foreach (var e in expenses)
        {
            if (e.ApprovalStatus == LedgerApprovalStatus.Rejected) continue;
            if (e.ApprovalStatus == LedgerApprovalStatus.Pending)  continue;
            total += e.Amount;
        }
        return RoundMoney(total);
    }

    /// <summary>Sum of approved-only capital contributions across all partners.</summary>
    public static decimal CalcTotalCapital(IEnumerable<CapitalContribution> contributions)
    {
        decimal total = 0m;
        foreach (var c in contributions)
        {
            if (c.ApprovalStatus == LedgerApprovalStatus.Rejected) continue;
            if (c.ApprovalStatus == LedgerApprovalStatus.Pending)  continue;
            total += c.Amount;
        }
        return RoundMoney(total);
    }

    /// <summary>Sum of approved-only labor value contributions.</summary>
    public static decimal CalcTotalLaborValue(IEnumerable<LaborContribution> labor)
    {
        decimal total = 0m;
        foreach (var l in labor)
        {
            if (l.ApprovalStatus == LedgerApprovalStatus.Rejected) continue;
            if (l.ApprovalStatus == LedgerApprovalStatus.Pending)  continue;
            total += l.Amount;
        }
        return RoundMoney(total);
    }

    // ── Step 2: Net P/L ─────────────────────────────────────────────────

    /// <summary>Net = GrossRevenue - GrossExpense. Positive = profit, Negative = loss.</summary>
    public static decimal CalcNetPL(decimal grossRevenue, decimal grossExpense)
        => RoundMoney(grossRevenue - grossExpense);

    /// <summary>True if the investment ended with a profit (NetPL > 0).</summary>
    public static bool IsProfit(decimal netPL) => netPL > 0m;

    /// <summary>True if the investment ended with a loss (NetPL < 0).</summary>
    public static bool IsLoss(decimal netPL) => netPL < 0m;

    // ── Step 3: Per-partner profit share ────────────────────────────────

    /// <summary>
    /// Partner's share of profit. ONLY runs when NetPL is positive — pass
    /// the positive value as <paramref name="netProfit"/>. Returns 0 if the
    /// investment had no profit.
    /// </summary>
    public static decimal CalcPartnerProfitShare(decimal netProfit, decimal profitSharePercent)
    {
        if (netProfit <= 0m) return 0m;
        var share = netProfit * (profitSharePercent / 100m);
        return RoundMoney(share);
    }

    // ── Step 4: Per-partner loss share ──────────────────────────────────

    /// <summary>
    /// Partner's share of loss, taking Shariah rules into account.
    /// <list type="bullet">
    /// <item>Pass the loss as a POSITIVE number in <paramref name="netLossAbsolute"/>.</item>
    /// <item>Labor-only partner (Mudarib): loss share = 0. Time lost only.</item>
    /// <item>Otherwise: lossPercent applied directly. (Musharakah convention
    ///   is that loss must follow capital ratio; this module assumes the
    ///   loss % was already set to match the capital ratio at contract time
    ///   — see <see cref="ShariahValidator"/> for the activation check.)</item>
    /// </list>
    /// </summary>
    public static decimal CalcPartnerLossShare(decimal netLossAbsolute, decimal lossSharePercent, ContractType contractType)
    {
        if (netLossAbsolute <= 0m) return 0m;
        if (contractType == ContractType.LaborOnly) return 0m;
        var share = netLossAbsolute * (lossSharePercent / 100m);
        return RoundMoney(share);
    }

    // ── Step 5: Final per-partner settlement ────────────────────────────

    /// <summary>
    /// Net amount the investment owes the partner (or partner owes back to
    /// the pool, if negative). Sequence is intentional and human-readable:
    /// <code>
    /// settlement = CapitalContributed
    ///            + ProfitShareAmount
    ///            - LossShareAmount
    ///            - WithdrawalsDuringInvestment
    /// </code>
    /// For Labor-only partners CapitalContributed will normally be 0 and
    /// LossShareAmount will be 0, so the formula reduces to just the
    /// profit share less any withdrawals.
    /// </summary>
    public static decimal CalcPartnerSettlement(
        decimal capitalContributed,
        decimal profitShareAmount,
        decimal lossShareAmount,
        decimal withdrawalsDuringInvestment)
    {
        var settlement = capitalContributed
                       + profitShareAmount
                       - lossShareAmount
                       - withdrawalsDuringInvestment;
        return RoundMoney(settlement);
    }

    // ── Step 6: Zakat (informational only) ──────────────────────────────

    /// <summary>
    /// Returns the amount on which zakat MAY be due — usually the partner's
    /// capital plus their profit share, when positive. Caller decides nisab,
    /// hawl, and personal zakat liability. We just expose the base; we never
    /// auto-deduct.
    /// </summary>
    public static decimal CalcZakatEligibleBase(decimal capitalContributed, decimal profitShareAmount)
    {
        var basis = capitalContributed + profitShareAmount;
        return basis > 0m ? RoundMoney(basis) : 0m;
    }

    // ── Step 6.5: Adjustment delta (used when reclosing after a reopen) ──

    /// <summary>
    /// Delta between the freshly calculated settlement and the previous
    /// snapshot's settlement. Positive = investment still owes the partner
    /// more (Outgoing adjustment). Negative = partner owes back (Incoming
    /// adjustment).
    /// </summary>
    public static decimal CalcAdjustmentDelta(decimal currentSettlement, decimal previousSettlement)
        => RoundMoney(currentSettlement - previousSettlement);

    // ── Step 7: Tamper-detection checksum ───────────────────────────────

    /// <summary>
    /// SHA256 of a canonical, sort-stable string built from the snapshot
    /// header + each partner detail. Recomputed on read to detect any silent
    /// DB edits. The format intentionally uses '|' separators and 'P=' /
    /// 'L=' prefixes so a future schema addition doesn't change the hash
    /// for existing rows.
    /// </summary>
    public static string CalcChecksum(InvestmentSnapshot snap, IEnumerable<SnapshotPartnerDetail> details)
    {
        // Money is formatted with fixed 2-decimal scale + invariant culture so
        // the string is stable regardless of how the decimal was constructed
        // (Math.Round vs DB round-trip can leave different trailing-zero
        // representations on the same underlying value).
        static string M(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string P(decimal v) => v.ToString("F4", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("snap|").Append(snap.InvestmentCode).Append('|')
          .Append(M(snap.GrossRevenue)).Append('|')
          .Append(M(snap.GrossExpense)).Append('|')
          .Append(M(snap.NetPL)).Append('|')
          .Append(M(snap.TotalCapital)).Append('|')
          .Append(M(snap.TotalLaborValue)).Append('|')
          .Append(snap.PartnerCount).Append('|')
          .Append(snap.ClosedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        foreach (var d in details.OrderBy(d => d.PartnerId))
        {
            sb.Append("|pd|")
              .Append(d.PartnerId).Append(';')
              .Append(d.PartnerName).Append(';')
              .Append(d.PartnerNid ?? "").Append(';')
              .Append(d.ContractTypeAtClose).Append(';')
              .Append(d.PartnerRoleAtClose).Append(';')
              .Append("Cap=").Append(M(d.CapitalContributed)).Append(';')
              .Append("Lab=").Append(M(d.LaborValueContributed)).Append(';')
              .Append("P%=").Append(P(d.ProfitSharePercent)).Append(';')
              .Append("L%=").Append(P(d.LossSharePercent)).Append(';')
              .Append("P$=").Append(M(d.ProfitShareAmount)).Append(';')
              .Append("L$=").Append(M(d.LossShareAmount)).Append(';')
              .Append("Set=").Append(M(d.FinalSettlementAmount));
        }

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
