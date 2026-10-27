# 0001 — Demonstração pública no Render (plano gratuito)

Data: 27/10/2026. Estado: proposta na PR `feat/deploy-render`, aguardando revisão.

## Contexto
A equipe precisa de uma URL pública para ensaio e apresentação, sem custo e sem cartão. A base só rodava localmente: porta fixa em `localhost`, esquema criado com `EnsureCreated` apenas em Development e chaves de proteção do cookie no disco do processo.

## Decisão
- **Hospedagem:** Render, plano gratuito, por Blueprint (`render.yaml`): um serviço web Docker e um PostgreSQL gratuito, ligados por `fromDatabase` (nenhuma senha no repositório).
- **Uma imagem para front e API:** o build Angular vai para `wwwroot` e a API o serve. Mantém mesma origem, cookie `SameSite=Strict`, checagem de `Origin` e dispensa CORS. Coerente com "monólito e banco único" (ARQUITETURA).
- **Migrações versionadas** (`Migrations/Inicial`) aplicadas na inicialização em todos os ambientes, no lugar de `EnsureCreated`.
- **Chaves do Data Protection no PostgreSQL** (`DataProtectionKeys`), para reinícios e deploys não invalidarem sessões.
- **Proxy:** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` para o rate limit enxergar o IP do cliente e não um IP único do proxy.
- **Conexão:** `DatabaseUrl.ToNpgsql` aceita URL `postgresql://` dos provedores (testes em `DatabaseUrlTests`).

## Alternativas consideradas
| Opção | Por que não agora |
|---|---|
| Vercel/Netlify para o front + API separada | Origens diferentes exigiriam cookie `SameSite=None`, CORS com credenciais e proteção CSRF adicional |
| Neon (Postgres gratuito permanente) | Exige mais uma conta e colar a connection string manualmente; fica como plano para quando o banco do Render expirar |
| Railway, Fly.io, Cloud Run | Créditos de teste ou cartão obrigatório |
| MongoDB (Atlas gratuito) | Ver seção abaixo |

## Sobre MongoDB para "sincronizar notícias"
Não adotar agora. Os dados atuais são relacionais e dependem de garantias que o PostgreSQL já dá: e-mail único, chaves estrangeiras com exclusão em cascata, `ON CONFLICT DO NOTHING` para salvar de forma idempotente e, no futuro, a reserva transacional de quota (B07). Um segundo banco contraria "monólito e banco único", dobra configuração, backup e testes e cria sincronização entre bancos.

Ingestão de notícias cabe no PostgreSQL: tabela de itens com índice único na URL canônica ou no GUID do feed (deduplicação por `ON CONFLICT`), coluna `jsonb` para o payload bruto do feed, busca textual com `tsvector` em português. Rever se surgir volume ou formato que o PostgreSQL comprovadamente não atenda (medido, não presumido). A ingestão continua dependendo da política aprovada de fontes, SSRF e limites (AGENTS.md).

## Consequências e limites
- Banco gratuito do Render expira em 30 dias; planejar troca antes da entrega final.
- Hibernação após inatividade: abrir a URL antes da apresentação.
- Bancos locais criados com `EnsureCreated` precisam ser recriados uma vez (dados fictícios).
- `BootstrapAdminEmail` promove uma conta existente com aquele e-mail; cadastrar a conta antes de definir a variável, para ninguém registrar o e-mail primeiro.
- Comportamento do IP encaminhado pelo proxy do Render não foi medido; confirmar o rate limit por cliente após o deploy.
