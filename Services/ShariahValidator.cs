using FlexCms.InvestPro.Data;

namespace FlexCms.InvestPro.Services;

public record ShariahValidationResult(bool IsValid, string? Error);

/// <summary>
/// Validates investment + per-partner contracts against Shariah-compliance
/// rules before activation. Catches the common mistakes that would otherwise
/// only surface at close time (when distribution math finally runs against
/// real numbers).
/// </summary>
public static class ShariahValidator
{
    public static ShariahValidationResult Validate(Investment inv)
    {
        if (inv.PartnerContracts is null || inv.PartnerContracts.Count == 0)
            return new(false, "At least one partner contract is required before activating.");

        decimal profitSum = 0m;
        decimal lossSum = 0m;
        decimal totalCapital = 0m;

        foreach (var c in inv.PartnerContracts)
        {
            if (c.ProfitSharePercent < 0m || c.ProfitSharePercent > 100m)
                return new(false, $"Partner contract has invalid profit % ({c.ProfitSharePercent}).");
            if (c.LossSharePercent < 0m || c.LossSharePercent > 100m)
                return new(false, $"Partner contract has invalid loss % ({c.LossSharePercent}).");
            if (c.AgreedCapital < 0m)
                return new(false, "Capital cannot be negative.");
            if (c.AgreedLaborValue < 0m)
                return new(false, "Labor value cannot be negative.");

            if (c.ContractType == ContractType.LaborOnly && c.AgreedCapital > 0m)
                return new(false, "Labor-only partner cannot have capital contribution.");

            if (c.ContractType == ContractType.LaborOnly && c.LossSharePercent != 0m)
                return new(false, "Labor-only partner (Mudarib) cannot share in monetary loss. Time loss only — set Loss % to 0.");

            profitSum += c.ProfitSharePercent;
            lossSum   += c.LossSharePercent;
            totalCapital += c.AgreedCapital;
        }

        if (Math.Abs(profitSum - 100m) > 0.01m)
            return new(false, $"Profit shares must sum to 100% (current sum: {profitSum:0.##}%).");
        if (Math.Abs(lossSum - 100m) > 0.01m)
            return new(false, $"Loss shares must sum to 100% (current sum: {lossSum:0.##}%).");

        if (totalCapital == 0m && inv.PartnerContracts.All(c => c.ContractType == ContractType.LaborOnly))
            return new(false, "An investment with no capital cannot be activated.");

        return new(true, null);
    }
}
