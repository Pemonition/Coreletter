# RadarPlural.Api: entrada web (WebApi)

**Camada:** entrada/apresentação (arquitetura limpa: *WebApi*). Também serve o Angular compilado (mesma origem).
**Dono proposto:** Davidy (regras de negócio e quota, B05/B07) com Henrique (validação e moderação, B03/B08). O grupo confirma na Ficha.

## O que tem aqui
| Arquivo | Conteúdo |
|---|---|
| `Program.cs` | Endpoints HTTP, autenticação por cookie, limite por IP, sondas `/api/health` e `/api/health/ready`, migração (`--migrate-only`) |
| `Domain.cs` | `AppDb` (EF Core), entidades (Member, Post, FeedItem, Story…), `Rules` e `UsageBudget` |
| `DatabaseUrl.cs` | Converte `postgresql://` (Render) para o formato do Npgsql; compartilhado com o Worker |
| `Migrations/` | Migrações versionadas do banco. **Este projeto é o dono do esquema** |

## Depende de / quem depende dele
Depende de EF Core + Npgsql. Os testes (`RadarPlural.Tests`) referenciam este projeto. O Worker só reaproveita o `DatabaseUrl.cs`.

## Como rodar e testar
```powershell
.\scripts\Start-Api.ps1          # API em http://localhost:5080 (precisa do PostgreSQL do compose)
.\scripts\Test-Api.ps1           # cenário HTTP completo
dotnet test backend/RadarPlural.slnx
```
Contrato HTTP: `backend/API.md`. Mudou um endpoint? Atualize `API.md`, o front e os testes na mesma PR.

## Dívida conhecida
Entidades e regras ainda moram no projeto web (`Domain.cs`). Pela arquitetura limpa (decisão 0004), vão para um projeto `RadarPlural.Domain` depois do merge das PRs abertas. Até lá, regra nova de negócio (ex.: RN01) deve ficar em classe própria e testável, nunca dentro do endpoint.
