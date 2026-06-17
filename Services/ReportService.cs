using FlexCms.Framework.Modules;
using FlexCms.Framework.Modules.Attributes;
using FlexCms.InvestPro.Data;
using Microsoft.EntityFrameworkCore;

namespace FlexCms.InvestPro.Services;

// ── Report row DTOs ─────────────────────────────────────────────────────
//
// Every row type is a flat record that maps 1:1 onto a SELECT column list.
// Raw SQL chosen deliberately: each report's join shape is fixed by
// business intent, the column order matches the on-screen / CSV column
// order, and a future tweak (add a column / change rounding) lands in a
// single string instead of an EF expression tree.

public sealed record ClosureRow(
    string LedgerType,
    DateTime TransactionDate,
    string PartnerName,
    string Description,
    decimal Amount,
    string ApprovalStatus
);

public sealed record LifetimeRow(
    string InvestmentCode,
    string InvestmentName,
    string LifecycleStatus,
    DateTime StartDate,
    DateTime? ClosedAt,
    decimal CapitalContributed,
    decimal LaborValueContributed,
    decimal ProfitSharePercent,
    decimal LossSharePercent,
    decimal? ProfitShareAmount,
    decimal? LossShareAmount,
    decimal? FinalSettlementAmount
);

public sealed record ZakatRow(
    string PartnerName,
    string? PartnerNid,
    int Year,
    string InvestmentCode,
    DateTime ClosedAt,
    decimal CapitalContributed,
    decimal ProfitShareAmount,
    decimal ZakatEligibleAmount
);

public sealed record ComparisonRow(
    string PartnerName,
    string? PartnerNid,
    decimal PlannedCapital,
    decimal PlannedLaborValue,
    decimal ProfitSharePercent,
    decimal ActualCapitalContributed,
    decimal ActualLaborValueContributed,
    decimal? ProfitShareAmount,
    decimal? LossShareAmount,
    decimal? FinalSettlementAmount
);

[FcmsScoped]
public class ReportService
{
    private readonly ModuleActivationOptions _opts;
    public ReportService(ModuleActivationOptions opts) => _opts = opts;

    private InvestProDbContext OpenDb() =>
        (InvestProDbContext)new InvestProModule().CreateMigrationContext(_opts.ConnectionString, _opts.Provider)!;

    // ── Report 1: Closure Report ────────────────────────────────────────
    //
    // All approved ledger rows for one investment, in chronological order,
    // with the partner that paid/received. Used for the printable PDF that
    // accompanies the snapshot.
    public async Task<List<ClosureRow>> GetClosureReportAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        const string sql = @"
            SELECT 'Capital' AS ""LedgerType"",
                   c.""TransactionDate"",
                   COALESCE(p.""Name"", '—') AS ""PartnerName"",
                   c.""Description"",
                   c.""Amount"",
                   c.""ApprovalStatus""
              FROM investpro_capital_contributions c
              LEFT JOIN investpro_partners p ON p.""Id"" = c.""PartnerId""
             WHERE c.""InvestmentId"" = {0} AND c.""Status"" <> 404
            UNION ALL
            SELECT 'Labor',  l.""TransactionDate"", COALESCE(p.""Name"", '—'), l.""Description"", l.""Amount"", l.""ApprovalStatus""
              FROM investpro_labor_contributions l
              LEFT JOIN investpro_partners p ON p.""Id"" = l.""PartnerId""
             WHERE l.""InvestmentId"" = {0} AND l.""Status"" <> 404
            UNION ALL
            SELECT 'Expense', e.""TransactionDate"", COALESCE(p.""Name"", '—'), e.""Description"", e.""Amount"", e.""ApprovalStatus""
              FROM investpro_expenses e
              LEFT JOIN investpro_partners p ON p.""Id"" = e.""PartnerId""
             WHERE e.""InvestmentId"" = {0} AND e.""Status"" <> 404
            UNION ALL
            SELECT 'Revenue', r.""TransactionDate"", COALESCE(p.""Name"", '—'), r.""Description"", r.""Amount"", r.""ApprovalStatus""
              FROM investpro_revenues r
              LEFT JOIN investpro_partners p ON p.""Id"" = r.""PartnerId""
             WHERE r.""InvestmentId"" = {0} AND r.""Status"" <> 404
             ORDER BY 2 ASC;
        ";
        return await db.Database.SqlQueryRaw<ClosureRow>(sql, investmentId).ToListAsync(ct);
    }

    // ── Report 2: Partner Lifetime Statement ───────────────────────────
    //
    // Every investment a partner has participated in, with planned vs
    // actual numbers side by side. Snapshot rows (post-close) bring in
    // the settled profit/loss/payout. Active/Closing rows show planned
    // numbers only; ProfitShareAmount stays NULL.
    public async Task<List<LifetimeRow>> GetLifetimeStatementAsync(Guid partnerId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        const string sql = @"
            SELECT i.""Code""             AS ""InvestmentCode"",
                   i.""Name""             AS ""InvestmentName"",
                   i.""LifecycleStatus"",
                   i.""StartDate"",
                   i.""ClosedAt"",
                   ip.""AgreedCapital""    AS ""CapitalContributed"",
                   ip.""AgreedLaborValue"" AS ""LaborValueContributed"",
                   ip.""ProfitSharePercent"",
                   ip.""LossSharePercent"",
                   spd.""ProfitShareAmount"",
                   spd.""LossShareAmount"",
                   spd.""FinalSettlementAmount""
              FROM investpro_investment_partners ip
              JOIN investpro_investments i ON i.""Id"" = ip.""InvestmentId""
              LEFT JOIN investpro_investment_snapshots s ON s.""InvestmentId"" = i.""Id""
              LEFT JOIN investpro_snapshot_partner_details spd
                     ON spd.""SnapshotId"" = s.""Id"" AND spd.""PartnerId"" = ip.""PartnerId""
             WHERE ip.""PartnerId"" = {0}
               AND ip.""Status"" <> 404
             ORDER BY i.""StartDate"" DESC;
        ";
        return await db.Database.SqlQueryRaw<LifetimeRow>(sql, partnerId).ToListAsync(ct);
    }

    // ── Report 3: Annual Zakat Report ──────────────────────────────────
    //
    // Per partner per year: zakat-eligible base from each closed
    // investment that year. NID + name are pulled from the snapshot
    // detail (frozen at close) so the report stays correct even after
    // the partner row is later edited in the global pool.
    public async Task<List<ZakatRow>> GetZakatReportAsync(int year, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        const string sql = @"
            SELECT spd.""PartnerName"",
                   spd.""PartnerNid"",
                   {0}::int AS ""Year"",
                   s.""InvestmentCode"",
                   s.""ClosedAt"",
                   spd.""CapitalContributed"",
                   spd.""ProfitShareAmount"",
                   spd.""ZakatEligibleAmount""
              FROM investpro_investment_snapshots s
              JOIN investpro_snapshot_partner_details spd ON spd.""SnapshotId"" = s.""Id""
             WHERE EXTRACT(YEAR FROM s.""ClosedAt"") = {0}
               AND s.""Status"" <> 404
               AND spd.""ZakatEligibleAmount"" > 0
             ORDER BY spd.""PartnerName"" ASC, s.""ClosedAt"" ASC;
        ";
        return await db.Database.SqlQueryRaw<ZakatRow>(sql, year).ToListAsync(ct);
    }

    // ── Report 4: Planned-vs-Actual Comparison ─────────────────────────
    //
    // Per partner of a closed investment: planned (agreed capital + labor
    // value + profit %) vs actual (sum of approved capital + labor +
    // settlement). Shows where each partner over- or under-shot the
    // original contract.
    public async Task<List<ComparisonRow>> GetComparisonReportAsync(Guid investmentId, CancellationToken ct = default)
    {
        await using var db = OpenDb();
        const string sql = @"
            SELECT spd.""PartnerName"",
                   spd.""PartnerNid"",
                   ip.""AgreedCapital""       AS ""PlannedCapital"",
                   ip.""AgreedLaborValue""    AS ""PlannedLaborValue"",
                   ip.""ProfitSharePercent"",
                   spd.""CapitalContributed""    AS ""ActualCapitalContributed"",
                   spd.""LaborValueContributed"" AS ""ActualLaborValueContributed"",
                   spd.""ProfitShareAmount"",
                   spd.""LossShareAmount"",
                   spd.""FinalSettlementAmount""
              FROM investpro_investment_partners ip
              JOIN investpro_investment_snapshots s ON s.""InvestmentId"" = ip.""InvestmentId""
              JOIN investpro_snapshot_partner_details spd
                     ON spd.""SnapshotId"" = s.""Id"" AND spd.""PartnerId"" = ip.""PartnerId""
             WHERE ip.""InvestmentId"" = {0}
               AND ip.""Status"" <> 404
             ORDER BY ip.""AgreedCapital"" DESC;
        ";
        return await db.Database.SqlQueryRaw<ComparisonRow>(sql, investmentId).ToListAsync(ct);
    }
}
