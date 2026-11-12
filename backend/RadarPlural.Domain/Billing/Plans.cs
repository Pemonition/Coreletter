namespace RadarPlural.Domain.Billing;

// Plano de assinatura (#13: Free e Plus). Preço em reais por mês; quota = comparações com IA por mês.
public sealed record Plan(string Id, string Name, decimal MonthlyPrice, int MonthlyComparisons);

// Hipóteses de custo do negócio (ajustar quando houver preço real do provedor de IA). Ficam num lugar só.
public sealed record CostAssumptions(
    decimal CostPerComparison,      // custo de IA por comparação (tokens), R$
    decimal DeliveryCostPerPaid,    // custo de entrega por assinante pago por mês (e-mail etc.), R$
    decimal MinimumMargin,          // margem mínima sobre o preço (0,25 = 25%)
    decimal MaxFreeCostPerUser,     // quanto aceitamos gastar por usuário grátis por mês (custo de aquisição), R$
    decimal FixedMonthlyCost);      // custos fixos do mês (teto de infraestrutura etc.), R$

public enum PlanCheck { Ok, MarginBelowMinimum, FreeCostAboveLimit, Invalid }

// RN02 (margem mínima) e RN04 (ponto de equilíbrio). Regras puras: sem banco, sem web, testadas com casos limite.
public static class Profitability
{
    // Pior caso de custo variável de um cliente no mês: ele usa a quota inteira.
    public static decimal MaxVariableCost(Plan plan, CostAssumptions c) =>
        plan.MonthlyComparisons * c.CostPerComparison + (plan.MonthlyPrice > 0 ? c.DeliveryCostPerPaid : 0m);

    // Margem = lucro / preço. Plano grátis não tem margem (é custo de aquisição).
    public static decimal? Margin(Plan plan, CostAssumptions c) =>
        plan.MonthlyPrice > 0 ? (plan.MonthlyPrice - MaxVariableCost(plan, c)) / plan.MonthlyPrice : null;

    // Menor preço que mantém a margem mínima, arredondado PARA CIMA no centavo (arredondar para baixo furaria a regra).
    public static decimal MinimumPrice(Plan plan, CostAssumptions c)
    {
        if (c.MinimumMargin is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(c), "Margem mínima deve estar entre 0 e 1.");
        var exact = MaxVariableCost(plan with { MonthlyPrice = 1m }, c) / (1 - c.MinimumMargin);
        return Math.Ceiling(exact * 100m) / 100m;
    }

    // Um plano só pode ser oferecido se não der prejuízo no pior caso.
    public static PlanCheck Check(Plan plan, CostAssumptions c)
    {
        if (plan.MonthlyPrice < 0 || plan.MonthlyComparisons < 0) return PlanCheck.Invalid;
        if (plan.MonthlyPrice == 0)
            return MaxVariableCost(plan, c) <= c.MaxFreeCostPerUser ? PlanCheck.Ok : PlanCheck.FreeCostAboveLimit;
        return Margin(plan, c) >= c.MinimumMargin ? PlanCheck.Ok : PlanCheck.MarginBelowMinimum;
    }

    // Quantos assinantes pagam os custos fixos do mês: custo fixo / contribuição de cada um, arredondado para cima.
    public static int BreakEvenSubscribers(Plan paid, CostAssumptions c)
    {
        var contribution = paid.MonthlyPrice - MaxVariableCost(paid, c);
        if (contribution <= 0) throw new InvalidOperationException("Cada assinante dá prejuízo: não existe ponto de equilíbrio.");
        return (int)Math.Ceiling(c.FixedMonthlyCost / contribution);
    }
}

public enum UsageDecision { Allowed, QuotaExceeded }

// RN01 (quota mensal). A reserva atômica no banco (RN03) usa esta mesma regra: só reserva se usado < quota.
public static class UsageRules
{
    public static UsageDecision Decide(Plan plan, int usedThisMonth) =>
        usedThisMonth < plan.MonthlyComparisons ? UsageDecision.Allowed : UsageDecision.QuotaExceeded;

    // O mês é contado em UTC: igual para todos os usuários, sem depender do fuso de quem pede.
    public static string MonthKey(DateTimeOffset when) => when.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
