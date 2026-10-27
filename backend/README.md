# Backend RadarPlural

.NET 10, ASP.NET Core, EF Core e PostgreSQL. Contratos em API.md. Não faz scraping nem chamadas Gemini. Senhas usam PasswordHasher padrão ASP.NET Core; sessão cookie HttpOnly/SameSite Strict, autorização por proprietário e perfil. Rodar local, via proxy Angular preservando Host (changeOrigin:false).

## Executar

No PowerShell, a partir da raiz do repositório, com PostgreSQL criado:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Default='Host=localhost;Port=5433;Database=radarplural;Username=radarplural;Password=VALOR_LOCAL_DO_COMPOSE'
dotnet run --project .\backend\RadarPlural.Api
```

Banco vazio é criado por EnsureCreated exclusivamente em Development. Para evolução, substituir por migrações versionadas; não executar EnsureCreated em base existente de produção. Endereço localhost:5080. Nunca expor este servidor de desenvolvimento à internet.

## Administrador local

1. Cadastre sua conta na interface.
2. Pare a API; defina `$env:BootstrapAdminEmail='seu-email-cadastrado'` e inicie novamente em Development.
3. Faça logout/login para atualizar o papel no cookie. Remova a variável quando terminar. Nenhuma senha ou administrador padrão é distribuído.

## Testes

```powershell
dotnet test .\backend\RadarPlural.slnx
```

Os testes atuais verificam regras puras: links, limite de orçamento, contribuição, rejeição de parâmetros inválidos, quota, concorrência e repetição idempotente. Não afirmamos ciclo TDD completo retroativamente: guardar evidência RED/GREEN nas próximas tarefas.

## Limites e próximos passos

- UsageBudget é domínio demonstrativo em memória, NÃO conectado a assinaturas, endpoints ou IA. Não é ledger financeiro de produção; faltam persistência transacional, períodos, reservas/liquidação/estorno, idempotência com verificação de payload e testes PostgreSQL. RN01/RN03 são cálculos iniciais, não validação de rentabilidade real.
- Não houve teste de integração PostgreSQL até o serviço local estar disponível. Antes de aceitar login/feed, executar cenário completo em banco real e teste de permissões entre duas contas.
- Rate limit em memória por IP60/min para login/cadastro e operações autenticadas. Não substitui proteção distribuída, auditoria e política de recuperação de conta em produção.
- Cadastro aberto apenas para demonstração local; sem confirmação de e-mail ou recuperação de senha. Sem dados pessoais reais no demo.
- Link é armazenado como referência. Servidor não baixa a URL. Frontend deve abrir com noopener/noreferrer. Busca de metadados futura exige proteção SSRF.
- Lista de salvos e denúncias administrativas limitada a100 nesta base; feed público paginado até50. Sem algoritmo de recomendação ou conteúdo HTML remoto.
- Denúncias não ocultam automaticamente. Apenas admin pode ocultar. Conta cadastrada não pode tornar-se admin pela API.
- Segurança de produção exige HTTPS, migrations, retenção e privacidade, logs e limites de payload, verificação de dependências e testes de autorização e CSRF.
