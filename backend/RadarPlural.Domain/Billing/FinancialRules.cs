namespace RadarPlural.Domain.Billing;

// RN01 (rentabilidade). Código movido sem mudança de lógica; responsabilidade da tarefa de planos.
public static class FinancialRules
{
    public static decimal AiBudget(decimal price, decimal otherVariableCosts, decimal contributionRate)
    {
        if (price < 0 || otherVariableCosts < 0 || contributionRate < 0 || contributionRate > 1) throw new ArgumentOutOfRangeException(nameof(price));
        var available = price * (1 - contributionRate) - otherVariableCosts;
        if (available < 0) throw new ArgumentException("Plano não cobre custos e margem.");
        return decimal.Floor(available * 100) / 100;
    }
    public static (decimal Contribution, decimal Result) Period(decimal revenue, decimal otherVariableCosts, decimal aiCosts, decimal fixedCosts)
    {
        if (revenue < 0 || otherVariableCosts < 0 || aiCosts < 0 || fixedCosts < 0) throw new ArgumentOutOfRangeException(nameof(revenue));
        var contribution = revenue - otherVariableCosts - aiCosts;
        return (contribution, contribution - fixedCosts);
    }
}
