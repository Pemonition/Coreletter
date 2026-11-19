using RadarPlural.Domain.Billing;

namespace RadarPlural.Tests;

// Regras de rentabilidade (Regulamento, seção 5). Números do exemplo da apresentação:
// Plus R$ 19,90 com 60 comparações; custo por comparação R$ 0,08; entrega R$ 0,50; margem mínima 25%.
public class ProfitabilityTests
{
    private static readonly CostAssumptions C = new(CostPerComparison: 0.08m, DeliveryCostPerPaid: 0.50m, MinimumMargin: 0.25m, MaxFreeCostPerUser: 0.30m, FixedMonthlyCost: 800m);
    private static readonly Plan Free = new("free", "Grátis", 0m, 3);
    private static readonly Plan Plus = new("plus", "Plus", 19.90m, 60);

    // RN02 — custo máximo e margem
    [Fact]
    public void PaidPlanCostIsQuotaTimesComparisonPlusDelivery() => Assert.Equal(60 * 0.08m + 0.50m, Profitability.MaxVariableCost(Plus, C)); // 5,30

    [Fact]
    public void FreePlanHasNoDeliveryCost() => Assert.Equal(3 * 0.08m, Profitability.MaxVariableCost(Free, C)); // 0,24

    [Fact]
    public void PlusMarginIsAbout73Percent() => Assert.Equal(0.7337m, Math.Round(Profitability.Margin(Plus, C)!.Value, 4));

    [Fact]
    public void FreePlanHasNoMargin() => Assert.Null(Profitability.Margin(Free, C));

    [Fact]
    public void MinimumPriceKeepsExactlyTheMinimumMarginRoundedUpToCents() => Assert.Equal(7.07m, Profitability.MinimumPrice(Plus, C)); // 5,30 / 0,75 = 7,0666…

    [Theory]
    [InlineData(19.90, PlanCheck.Ok)]   // folgado
    [InlineData(7.07, PlanCheck.Ok)]    // exatamente no limite (arredondado para cima)
    [InlineData(7.06, PlanCheck.MarginBelowMinimum)] // um centavo abaixo
    [InlineData(5.30, PlanCheck.MarginBelowMinimum)] // preço = custo: lucro zero
    [InlineData(4.00, PlanCheck.MarginBelowMinimum)] // prejuízo
    public void PaidPlanMustKeepMinimumMargin(double price, PlanCheck expected) =>
        Assert.Equal(expected, Profitability.Check(Plus with { MonthlyPrice = (decimal)price }, C));

    [Fact]
    public void RaisingQuotaWithoutRaisingPriceIsBlocked() =>
        Assert.Equal(PlanCheck.MarginBelowMinimum, Profitability.Check(Plus with { MonthlyComparisons = 300 }, C)); // custo 24,50 > preço

    [Theory]
    [InlineData(3, PlanCheck.Ok)]                   // 0,24 ≤ 0,30
    [InlineData(4, PlanCheck.FreeCostAboveLimit)]   // 0,32 > 0,30
    public void FreePlanCostIsCapped(int quota, PlanCheck expected) => Assert.Equal(expected, Profitability.Check(Free with { MonthlyComparisons = quota }, C));

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(10, -1)]
    public void NegativePriceOrQuotaIsInvalid(double price, int quota) =>
        Assert.Equal(PlanCheck.Invalid, Profitability.Check(new Plan("x", "X", (decimal)price, quota), C));

    // RN04 — ponto de equilíbrio
    [Fact]
    public void BreakEvenIsFixedCostOverContributionRoundedUp() => Assert.Equal(55, Profitability.BreakEvenSubscribers(Plus, C)); // 800 / 14,60 = 54,8

    [Fact]
    public void BreakEvenWithExactDivisionDoesNotAddOne() =>
        Assert.Equal(40, Profitability.BreakEvenSubscribers(Plus, C with { FixedMonthlyCost = 14.60m * 40 }));

    [Fact]
    public void BreakEvenIsImpossibleWhenEachSubscriberLosesMoney() =>
        Assert.Throws<InvalidOperationException>(() => Profitability.BreakEvenSubscribers(Plus with { MonthlyPrice = 5.30m }, C));

    // RN01 — quota mensal
    [Theory]
    [InlineData(0, UsageDecision.Allowed)]
    [InlineData(59, UsageDecision.Allowed)]       // última permitida
    [InlineData(60, UsageDecision.QuotaExceeded)] // quota cheia
    [InlineData(61, UsageDecision.QuotaExceeded)]
    public void QuotaIsEnforcedAtTheBoundary(int used, UsageDecision expected) => Assert.Equal(expected, UsageRules.Decide(Plus, used));

    [Fact]
    public void PlanWithZeroQuotaNeverAllows() => Assert.Equal(UsageDecision.QuotaExceeded, UsageRules.Decide(Free with { MonthlyComparisons = 0 }, 0));

    [Theory]
    [InlineData("2026-11-30T23:59:59-03:00", "2026-12")] // já é dezembro em UTC: o mês é contado em UTC, igual para todos
    [InlineData("2026-12-01T02:59:59+00:00", "2026-12")]
    [InlineData("2026-01-01T00:00:00+00:00", "2026-01")]
    public void MonthKeyIsUtc(string when, string expected) => Assert.Equal(expected, UsageRules.MonthKey(DateTimeOffset.Parse(when)));
}

public class PlanCatalogTests
{
    [Fact]
    public void EveryOfferedPlanRespectsTheProfitabilityRules() =>
        Assert.All(PlanCatalog.All, p => Assert.Equal(PlanCheck.Ok, Profitability.Check(p, PlanCatalog.Assumptions)));

    [Fact]
    public void UnknownPlanFallsBackToFree() => Assert.Equal("free", PlanCatalog.Find("ouro").Id);
}
