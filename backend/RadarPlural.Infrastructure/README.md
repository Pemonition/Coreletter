# RadarPlural.Infrastructure: adaptadores PostgreSQL

**Camada:** infraestrutura. Implementa as portas do `Pipeline` e grava o que o coletor produz.
**Dono proposto:** Hugo.

## O que tem aqui
| Arquivo | Adaptador |
|---|---|
| `PgStoryQueue.cs` | Fila de etapas na tabela `Stories`: `FOR UPDATE SKIP LOCKED`, lease e versão (várias réplicas sem duplicar trabalho) |
| `PgUsageLedger.cs` | RN03: reserva de quota atômica, idempotente |
| `PgAuditStore.cs` | `PgStoryReader` (história + notícias) e `PgAuditStore` (comparações em jsonb, com versão) |
| `SimulatedAuditor.cs` | Auditor simulado (B05): concordância, divergência numérica e única fonte, sem IA |
| `FeedItemStore.cs` | `FeedItemStore` (grava notícias, `ON CONFLICT DO NOTHING`) e `StoryStore` (retriagem e agrupamento numa transação) |

O esquema é da API (migrações em `RadarPlural.Api/Migrations`); este projeto só lê e escreve.

## Como testar
Testes de integração com PostgreSQL real (banco temporário criado e apagado):
```powershell
$env:INTEGRATION_DATABASE_URL = "Host=localhost;Port=5433;Database=postgres;Username=radarplural;Password=<a do .env>"
dotnet test backend/RadarPlural.slnx --filter "FullyQualifiedName~PgStoryQueueTests"
```
