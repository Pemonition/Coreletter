# Conciliar os frontends: Pemonition × feat/tela-historias

Proposta, **não integrada**. Feita em 12/11/2026 com o merge simulado numa pasta descartável e desfeito em seguida; nada foi enviado.

## Situação

As duas linhas saem do mesmo commit, `9a26532` ("docs: registrar CI aprovada e orientar inicio da equipe"), e divergem pouco no frontend:

| Só na Pemonition | Só na feat/tela-historias (pilha #24→#29) |
|---|---|
| Radar privado: `collection-radar.ts/.html` + 6 testes | Aba **Histórias e fontes** com comparação, quota do plano e `aria-current` |
| Destaque espacial CSS, link "pular para o conteúdo", foco visível | Abas que quebram linha no celular (#15) |
| `brand.ts` e `hero.ts` (nome central, hero compacto), contraste AA | Seção no `frontend/README.md` |

## Conflitos reais (merge simulado)

**Frontend: fácil.** São 2 conflitos, e nos dois a resolução é manter ambos os lados:
- `frontend/src/app.html`: os dois acrescentam botões no menu lateral. Resultado: Histórias, Feed, Salvos, Meu radar, Moderação.
- `frontend/src/styles.css`: os dois acrescentam blocos no fim. Manter os dois blocos. As regras de `.sidebar` no celular das duas branches fazem a mesma coisa (quebrar linha); manter uma só.
- `main.ts` se junta sozinho: o `select()` aceita `stories` sem login e o `radar` com login.

**Backend: é o trabalho de verdade.** `Program.cs`, `Domain.cs`, `backend/API.md` e `.github/workflows/ci.yml` conflitam. O radar da Pemonition cria as tabelas com `EnsureCreated`, enquanto a pilha usa **migrações versionadas** (`ItensFeed`, `TriagemHistorias`, `EtapasPipeline`, `Rentabilidade`, `Comparacoes`). `EnsureCreated` não convive com migrações: num banco já migrado, ele não cria as tabelas do radar.

## Ordem proposta

1. A pilha entra na `main` (#11 → #24/#25 → #26 → #27 → #28 → #29). Ela traz as migrações, a CI com PostgreSQL e os testes.
2. Numa branch nova a partir da `main`, trazer a Pemonition com merge (sem rebase, para preservar a autoria) e:
   - transformar as tabelas do radar numa migração (`dotnet ef migrations add RadarPrivado`) e tirar o `EnsureCreated`;
   - juntar os endpoints de coleta no `Program.cs` da pilha. São dois coletores com papéis diferentes: o **público** (Worker + fontes.json → histórias) e o **radar privado** (por usuário, com questionário). Registrar isso numa decisão `docs/decisoes/0005`;
   - resolver os 2 conflitos do frontend como descrito acima.
3. Rodar tudo: `dotnet test` com PostgreSQL, `npm test`, `Test-Api.ps1` e `Test-Collections.ps1`, mais a inspeção no navegador (`docs/HANDOFF.md`, seção do frontend visual).

## Ponto de desenho que a conciliação cria

Com 5 abas, o menu do celular fica com 3 linhas antes do conteúdo. Sugestão para o Lindomar:
- uma **barra inferior** fixa com as 3 abas principais (Histórias, Feed, Radar) e um item "Mais" para Salvos e Moderação;
- ou menu recolhível no topo.

Em ambos os casos, manter o `aria-current` e o foco visível.
