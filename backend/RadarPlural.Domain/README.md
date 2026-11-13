# RadarPlural.Domain: regras de negócio (domínio)

**Camada:** domínio (arquitetura limpa). Só C#: sem banco, sem web, sem IA. Começou pela regra de rentabilidade; as entidades da API migram para cá depois do merge das PRs abertas (decisão 0004).
**Dono proposto:** Davidy (B07).

| Pasta/arquivo | Conteúdo |
|---|---|
| `Billing/Plans.cs` | `Profitability` (RN02 margem mínima, RN04 ponto de equilíbrio) e `UsageRules` (RN01 quota mensal) |
| `Billing/Catalog.cs` | Planos e hipóteses de custo; porta `IUsageLedger` (RN03, implementada em `RadarPlural.Infrastructure/PgUsageLedger.cs`) |

Regras e números: `docs/RENTABILIDADE.md`. Testes: `RadarPlural.Tests/ProfitabilityTests.cs` e `PgUsageLedgerTests`.
