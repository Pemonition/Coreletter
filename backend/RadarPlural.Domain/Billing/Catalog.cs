namespace RadarPlural.Domain.Billing;

// Catálogo de planos e hipóteses de custo. NÚMEROS HIPOTÉTICOS para o TCC: o grupo confirma na Ficha (RN01–RN04).
// A API se recusa a iniciar se algum plano violar a margem mínima (RN02): a regra protege a receita.
public static class PlanCatalog
{
    public static readonly CostAssumptions Assumptions = new(
        CostPerComparison: 0.08m,
        DeliveryCostPerPaid: 0.50m,
        MinimumMargin: 0.25m,
        MaxFreeCostPerUser: 0.30m,
        FixedMonthlyCost: 800m); // teto de infraestrutura de deploy/k8s/custo.json

    public static readonly Plan Free = new("free", "Grátis", 0m, 3);
    public static readonly Plan Plus = new("plus", "Plus", 19.90m, 60);

    public static readonly IReadOnlyList<Plan> All = [Free, Plus];

    public static Plan Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Free;
}

public enum ReserveResult { Reserved, AlreadyReserved, QuotaExceeded }

// Porta (hexagonal, aula de 11/11): reserva de uso persistida. Adaptador em RadarPlural.Infrastructure (PgUsageLedger).
// RN03: reserva ANTES de gastar com IA; liquida no sucesso; libera na falha. Mesma chave = mesma reserva (idempotência).
public interface IUsageLedger
{
    Task<ReserveResult> ReserveAsync(Guid memberId, Plan plan, Guid key, DateTimeOffset now, CancellationToken ct);
    Task SettleAsync(Guid key, CancellationToken ct);
    Task ReleaseAsync(Guid key, CancellationToken ct);
    Task<int> UsedAsync(Guid memberId, DateTimeOffset now, CancellationToken ct);
}
