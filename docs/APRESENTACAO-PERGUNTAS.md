# Perguntas prováveis da banca e plano B

Complemento de [APRESENTACAO.md](APRESENTACAO.md). Respostas curtas, cada uma apontando para o código que a sustenta.
Quem responde: quem é dono da parte (ver [EQUIPE.md](EQUIPE.md)); qualquer integrante deve saber a versão curta.

## Verificado em 27/11/2026 (clone limpo da `main`, commit `3ab271d`)
| Verificação | Resultado |
|---|---|
| `dotnet test backend/RadarPlural.slnx` (sem PostgreSQL) | **246 aprovados, 0 falhas, 14 ignorados** (os de integração precisam de `INTEGRATION_DATABASE_URL`) |
| `npm ci` + `npm run build` no `frontend` | Build de produção aprovado (≈225 kB inicial, 62 kB transferidos) |
| Demo na nuvem `https://coreletter.onrender.com` | No ar: `/api/health/ready` 200, 50 histórias, 3 com 2+ fontes (uma com 7), notícias coletadas no próprio dia. **Primeiro acesso levou 23 s** (serviço hibernado) |

## Perguntas e respostas

### Produto e escopo
1. **Que problema vocês resolvem?** A mesma notícia contada de jeitos diferentes por veículos diferentes, num feed sem algoritmo de engajamento. O Coreletter agrupa a cobertura em histórias e mostra fontes lado a lado.
2. **A comparação usa IA de verdade?** Não na demo: o auditor é **simulado e determinístico** (`backend/RadarPlural.Infrastructure/SimulatedAuditor.cs`), atrás de uma porta que permite trocar por um provedor real (Gemini) depois. Escolha deliberada: demo reproduzível e sem custo.
3. **Concordância entre veículos prova que a notícia é verdadeira?** Não. Está declarado como limitação; mostramos evidências e links, o leitor decide.

### Arquitetura
4. **Por que monólito e não microsserviços?** Equipe pequena, prazo curto e a quota precisa de transação atômica num banco só. Monólito modular com projetos separados e dependências verificadas por teste ([decisão 0006](decisoes/0006-monolito-modular.md)).
5. **Como garantem que as camadas não se misturam?** `backend/RadarPlural.Tests/ArchitectureTests.cs`: 4 testes falham se o domínio depender de infraestrutura/web ou se camadas internas referenciarem a infraestrutura.
6. **Como isso escala?** API sem estado; o pipeline reserva trabalho no banco com `FOR UPDATE SKIP LOCKED` (`backend/RadarPlural.Infrastructure/PgStoryQueue.cs`): duas réplicas nunca pegam a mesma história e uma não espera a outra. Manifestos de Kubernetes em `deploy/k8s/` ([decisão 0004](decisoes/0004-escala-desde-o-mvp.md)).
7. **Como o front fala com a API?** Mesma origem: o build Angular é servido pela própria API (`UseStaticFiles`), sem CORS. Uma imagem Docker só, a mesma no Render e no `docker compose`.

### Segurança
8. **Como as senhas são guardadas?** Com hash via `PasswordHasher<Member>` do ASP.NET Core Identity (PBKDF2 com salt); nunca em texto. Senha de 10 a 128 caracteres (`Program.cs`, `/api/auth/register`).
9. **E a sessão?** Cookie `radarplural.session` com `HttpOnly`, `SameSite=Strict` e `Secure` fora do ambiente de desenvolvimento.
10. **E CSRF?** Além do `SameSite=Strict`, um middleware recusa com 403 qualquer requisição que altera dados (não GET/HEAD/OPTIONS) cujo cabeçalho `Origin` não seja o próprio host.
11. **Quem pode moderar?** A permissão vem do usuário autenticado, nunca do corpo da requisição. Rotas de moderação exigem o papel `admin` (`RequireRole("admin")`); o primeiro admin é promovido por `BootstrapAdminEmail`.
12. **O coletor de RSS pode ser usado para atacar a rede interna (SSRF)?** Não: só hosts da lista aprovada, só http/https na porta padrão, sem usuário na URL, e o IP resolvido precisa ser público — bloqueia 10/8, 172.16/12, 192.168/16, loopback, 169.254 (metadados de nuvem), CGNAT e IPv6 equivalentes (`backend/RadarPlural.Ingestion/UrlGuard.cs`). Há limite de tamanho e tempo por feed (`SafeFeedFetcher.cs`).
13. **E abuso da API?** Rate limit de janela fixa, padrão 60 requisições/minuto (`RateLimit:PermitPerMinute`).

### Regra de rentabilidade (exigência do regulamento)
14. **Onde está a regra e como provam?** `backend/RadarPlural.Domain/Billing`, testada em `ProfitabilityTests.cs` (17 testes, casos limite). Números em [RENTABILIDADE.md](RENTABILIDADE.md). Ao vivo: `GET /api/plans`.
15. **Mostre com números.** Plus R$ 19,90 com 60 comparações: custo máximo 60 × 0,08 + 0,50 = **R$ 5,30**; margem **73,4%**; preço mínimo com 25% de margem = **R$ 7,07** — a R$ 7,06 **a API não inicia**. Ponto de equilíbrio: 800 ÷ (19,90 − 5,30) = **55 assinantes**.
16. **E se dois pedidos chegarem ao mesmo tempo?** O custo é reservado **antes** de chamar o provedor, de forma atômica no banco; teste com 50 pedidos simultâneos e quota 10 aprova exatamente 10. Mesma chave não cobra duas vezes.

### Qualidade e processo
17. **Como testaram?** TDD nas regras de negócio e permissões; 260 testes (246 rodam sem banco, 14 de integração com PostgreSQL rodam na CI). Scripts HTTP `scripts/Test-Api.ps1` e `scripts/Test-Comparisons.ps1`.
18. **Como usaram IA no desenvolvimento?** Como assistente com revisão humana: cada PR com testes e evidências, e cada integrante explica o próprio código ([EQUIPE.md](EQUIPE.md)). Nenhuma chave ou dado privado em prompts.

## Plano B (se algo falhar ao vivo)
| Falha | O que fazer |
|---|---|
| Render demorando (hibernado) | Abrir a URL **2 minutos antes** de começar; se não subir em 60 s, ir para a demo local |
| Sem internet | Demo local: `docker compose up --build` → http://localhost:8080 (ver README). Testar na véspera no PC da apresentação |
| Docker não sobe | Seguir o roteiro sem Docker de [VALIDACAO-LOCAL-2026-11-23.md](VALIDACAO-LOCAL-2026-11-23.md) (PostgreSQL local + API + seed) |
| Nada roda | Vídeo "Por dentro do Coreletter" (`video/`) + capturas de tela, e explicar o código no GitHub |

## Checklist da véspera (além do de APRESENTACAO.md)
- [ ] Instalar PowerShell 7 (`winget install --id Microsoft.PowerShell -e`) no PC da apresentação: os scripts `scripts/*.ps1` usam `pwsh`
- [ ] Rodar `docker compose up --build` uma vez no PC da apresentação (baixa as imagens; depois funciona sem internet)
- [ ] Conferir espaço em disco (o build Docker precisa de alguns GB livres)
- [ ] Abrir `/api/plans` e uma história com 2+ fontes para ter as abas prontas
- [ ] Cada integrante responde em voz alta 3 perguntas desta lista sobre a própria parte
