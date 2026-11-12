# RadarPlural.Tests

**Dono:** todos. Cada um mantém os testes da própria parte; a regra de rentabilidade precisa de casos limite (Regulamento, seção 5).

| Arquivo | Cobre |
|---|---|
| `UnitTest1.cs`, `DatabaseUrlTests.cs` | Regras da base e conversão de URL |
| `IngestionTests.cs` | Coletor: SSRF, parser, limites, catálogo |
| `PipelineTests.cs` | Triagem e agrupamento com manchetes reais |
| `StageRunnerTests.cs` | Executor de etapas e novas tentativas |
| `PgStoryQueueTests.cs` | **Integração** com PostgreSQL: concorrência entre réplicas, lease, versão |
| `ArchitectureTests.cs` | Regra de dependência da arquitetura limpa |
| `Infra/CostCeilingTests.cs` | Teto de custo dos manifests do Kubernetes |

Integração: defina `INTEGRATION_DATABASE_URL`; sem ela, esses testes aparecem como **ignorados**. TDD: registre RED e GREEN na PR (AGENTS.md).
