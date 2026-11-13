# Backend Coreletter (nome técnico RadarPlural)

.NET 10, ASP.NET Core (minimal APIs), EF Core e PostgreSQL, em arquitetura hexagonal. Contrato HTTP em `API.md`; decisão de arquitetura em `docs/decisoes/0003-arquitetura-hexagonal.md`.

## Estrutura

```
RadarPlural.Domain/          núcleo: entidades, regras, portas (interfaces). Não depende de nada.
  Members/ Posts/ News/      entidades e regras de cada assunto
  Billing/                   RN01: FinancialRules, UsageBudget (ainda não ligados a endpoints)
  Ports/                     IPostRepository, IMemberRepository, IReportRepository, INewsRepository, IPasswordHasher
RadarPlural.Application/     casos de uso: AuthService, PostService, NewsService, ModerationService; DTOs; Result<T>
RadarPlural.Infrastructure/  adapters de saída: AppDb (EF Core), repositórios, migrações, hash de senha, health check
RadarPlural.Api/             adapter de entrada: Endpoints/ (um arquivo por recurso), Http/ (sessão, erros, middlewares)
RadarPlural.Ingestion/       download seguro e parser RSS/Atom (usado pelo Worker)
RadarPlural.Worker/          coletor de notícias (console; GitHub Actions ou CronJob no Kubernetes)
RadarPlural.Tests/           unitários: regras puras e casos de uso com adapters em memória
RadarPlural.IntegrationTests/ API real + PostgreSQL real (Testcontainers)
```

Para adicionar uma funcionalidade: entidade e porta no Domain → caso de uso na Application (com teste usando adapter em memória) → repositório na Infrastructure → endpoint na Api (com teste de integração quando houver regra de permissão ou de banco).

## Executar

No PowerShell, a partir da raiz, com o banco do `compose.yaml` no ar (`.\scripts\Start-Database.ps1`), use `.\scripts\Start-Api.ps1` ou:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Default='Host=localhost;Port=5433;Database=radarplural;Username=radarplural;Password=VALOR_DO_.env'
dotnet run --project .\backend\RadarPlural.Api
```

- API em http://localhost:5080. Documentação interativa em http://localhost:5080/swagger (OpenAPI em `/openapi/v1.json`), só em Development ou com `OpenApi__Enabled=true`.
- As migrações são aplicadas na inicialização (`Database__MigrateOnStartup`, padrão `true`). `dotnet RadarPlural.Api.dll --migrate` aplica e encerra (usado no Kubernetes).
- Saúde: `/api/health` (processo vivo, sem banco) e `/api/health/ready` (consegue falar com o banco).
- Fora de Development o log sai em JSON, uma linha por evento.

Nova migração depois de alterar entidade:

```powershell
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project backend/RadarPlural.Infrastructure --startup-project backend/RadarPlural.Api
```

O teste `MigrationsCoverTheModel` falha se alguém mudar entidade e esquecer a migração.

## Administrador local

1. Cadastre sua conta na interface.
2. Pare a API; defina `$env:BootstrapAdminEmail='seu-email-cadastrado'` e inicie novamente.
3. Faça logout/login para atualizar o papel no cookie. Nenhuma senha ou administrador padrão é distribuído.

## Testes

```powershell
dotnet test .\backend\RadarPlural.slnx
```

- `RadarPlural.Tests`: regras puras (links, RN01, quota, concorrência), ingestão RSS e casos de uso com adapters em memória. Não precisa de banco.
- `RadarPlural.IntegrationTests`: sobe a API real (`WebApplicationFactory<Program>`) contra PostgreSQL 17 descartável via **Testcontainers** (precisa do Docker Desktop em execução). Sem Docker, defina `INTEGRATION_DATABASE_URL` apontando para um PostgreSQL de teste com permissão `CREATEDB`; cada execução cria e apaga um banco próprio.
- `scripts/Test-Api.ps1` continua como teste de fumaça contra uma API já rodando (usado pela CI no container).

Cenários da B08 (#16) ainda sem teste automatizado: fluxo do administrador (listar, ocultar, item sumir do feed), publicar podcast/música pela API e filtros. A estrutura está pronta: veja `ApiFactory.SignedUpAsync` e os testes em `AuthAndIsolationTests`.

## Limites e próximos passos

- UsageBudget é domínio demonstrativo em memória, NÃO conectado a assinaturas, endpoints ou IA. Faltam persistência transacional, períodos, reservas/liquidação/estorno. RN01/RN03 são cálculos iniciais.
- Rate limit em memória por IP (60/min, `RateLimit__PermitLimit`) para login/cadastro e operações autenticadas. Com várias réplicas, cada uma conta separado; proteção distribuída exigiria Redis ou o gateway.
- Cadastro aberto apenas para demonstração; sem confirmação de e-mail ou recuperação de senha.
- Link é armazenado como referência. Servidor não baixa a URL.
- Lista de salvos e denúncias limitada a 100; feed paginado até 50.
- Denúncias não ocultam automaticamente; não há desfazer ocultação (#17).
