# Validação local — 23/11/2026

Base: `d3f4725` da main. Trabalho em `fix/validacao-local-20261123`; não houve merge nem deploy. Comparação segue simulada e preços seguem hipóteses acadêmicas. As referências de validação desta página são desta execução, não herdadas da CI anterior.

## Mudanças e motivo

- `@angular/build`: 22.2.3 → **22.2.5**, com Vite **8.3.5**, corrigindo os dois alertas da auditoria (Vite alto e dependência indireta em Angular build baixa). Mantidos Angular/CLI 22.2.3. `@angular/compiler-cli` 22.2.3 ficou explícito para impedir que a resolução automática escolhesse 22.3.0, incompatível com o compiler fixado. Não foram usados `--force`, `--legacy-peer-deps` ou overrides.
- Aviso primário consultado: [GHSA-587h-7wmj-h5pm](https://github.com/advisories/GHSA-587h-7wmj-h5pm). Os alertas atingiam ferramentas do servidor de desenvolvimento; auditoria sem alertas não constitui certificação de segurança do produto.
- `CompareStory`: uma chave já reservada, sem cache atual para a história solicitada, não autoriza nova chamada ao auditor. Antes, a chave podia ser reutilizada em outra história sem consumir quota. Mantido o código/HTTP de indisponibilidade existente (503), sem nova rota ou DTO. Repetir uma comparação ainda em cache continua gratuito. Em uma tentativa sem cache que devolva 503 por chave já usada, é necessária nova chave; os retries do frontend já geram uma nova.
- Removido da raiz um arquivo acidental cujo nome contém `:` e `"`, inválidos no Windows. Seu conteúdo era uma cópia colorida de um diff de `Test-Api.ps1`; não era código executado. Esse arquivo impedia checkout limpo no Windows.
- Novos `scripts/Seed-Demo.sql` e `scripts/Test-Comparisons.ps1`: fixtures fictícias e verificação HTTP de quota/cache/permissões. Nenhuma coleta externa é necessária para esses cenários.

## Resultados executados

| Verificação | Resultado |
| --- | --- |
| `npm audit --prefix frontend --json` online | Zero vulnerabilidades reportadas após a atualização |
| `npm run build --prefix frontend` | Build de produção aprovado |
| `npm test --prefix frontend` | Build de desenvolvimento aprovado; esse script não contém testes comportamentais |
| `dotnet test backend/RadarPlural.slnx --configuration Release` com `INTEGRATION_DATABASE_URL` | **258 aprovados, zero falhas e zero pulados** |
| Regressão de idempotência | Teste `RepeatedKeyWithoutReusableResultDoesNotRunAnotherComparison` falhou antes da correção, pelo resultado gratuito indevido; depois passou |
| `scripts/Test-Api.ps1` | Cadastro, publicação, salvar, isolamento, formatos, filtros, paginação, Origin, administrador e retirada de denúncia de item oculto aprovados |
| `scripts/Test-Comparisons.ps1` | Anônimo 401; não admin 403; comparação simulada; cache sem nova cobrança; chave reaproveitada em outra história 503; três usos permitidos; quarto 403; história inexistente 404; comparação pronta pública e consumo privado |
| `node --test video/gerador/lib.test.mjs` | **14 aprovados** |
| Navegador local | Cadastro, publicação e salvar conferidos; na versão corrigida, sessão, comparação/cache, consumo 1→2→3 e bloqueio da quarta comparação conferidos |
| Layout | Inspeção visual em **1280×900** e **390×844**; largura do documento 1265/375, respectivamente, sem overflow horizontal |

Os testes usaram **PostgreSQL 17 local isolado**, porta 55441, criado com os binários já instalados. Não foi alterado o serviço PostgreSQL existente. Docker continuou indisponível. Não foram validados containers, deploy Render, feeds reais, MP4, dispositivos físicos ou todos os critérios de acessibilidade nesta rodada.

Evidências locais, ignoradas pelo Git: `artifacts/validacao-20261123/` (logs HTTP, RED/GREEN, auditoria, screenshots `desktop.jpg`, `mobile.jpg`, `quota.jpg` e processos de demonstração); TRX em `backend/RadarPlural.Tests/TestResults/`. As capturas são de fixtures, não de notícias ou contas reais.

## Reproduzir com banco descartável

Use outra porta/base se estes nomes já estiverem em uso. Não use senha ou banco pessoal. `Seed-Demo.sql` recusa bases sem prefixo `coreletter_demo`; o teste de comparação exige fixtures 1–4 ainda sem comparações, portanto use base nova para cada rodada. Ele preserva contas e resultados para inspeção, sem limpar dados compartilhados.

Exemplo de preparação com os binários PostgreSQL existentes; execute na raiz do projeto:

```powershell
$taskPgBin = 'C:/Program Files/PostgreSQL/17/bin'
$taskRoot = Join-Path (Get-Location) 'artifacts/novo-ensaio'
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
& "$taskPgBin/initdb.exe" -D "$taskRoot/pgdata" -U coreletter_review -A trust --encoding=UTF8 --locale=C
# Pare se initdb falhar; não reutilize um diretório com dados de outro projeto.
& "$taskPgBin/pg_ctl.exe" -D "$taskRoot/pgdata" -l "$taskRoot/postgres.log" -o '-p 55442 -h 127.0.0.1' -w start
& "$taskPgBin/createdb.exe" -h 127.0.0.1 -p 55442 -U coreletter_review coreletter_demo_ensaio
$env:INTEGRATION_DATABASE_URL = 'Host=127.0.0.1;Port=55442;Database=postgres;Username=coreletter_review'
dotnet test backend/RadarPlural.slnx --configuration Release
$env:ConnectionStrings__Default = 'Host=127.0.0.1;Port=55442;Database=coreletter_demo_ensaio;Username=coreletter_review'
dotnet run --project backend/RadarPlural.Api --configuration Release --no-launch-profile -- --migrate-only
& "$taskPgBin/psql.exe" -h 127.0.0.1 -p 55442 -U coreletter_review -d coreletter_demo_ensaio -v ON_ERROR_STOP=1 -f scripts/Seed-Demo.sql
```

A autenticação `trust` destina-se exclusivamente a essa instância fictícia ligada ao loopback; não é configuração de hospedagem. Para executar a API de teste num terminal:

```powershell
$env:ConnectionStrings__Default = 'Host=127.0.0.1;Port=55442;Database=coreletter_demo_ensaio;Username=coreletter_review'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:PORT = '5096'
dotnet run --project backend/RadarPlural.Api --configuration Release --no-launch-profile
```

Em outro terminal:

```powershell
./scripts/Test-Comparisons.ps1 -BaseUrl http://localhost:5096
./scripts/Test-Api.ps1 -BaseUrl http://localhost:5096
```

O fluxo de administrador requer conta fictícia promovida via `BootstrapAdminEmail`, conforme README/CI, e parâmetros `-AdminEmail` / `-AdminPassword`. Sem esses parâmetros o teste pula a parte administrativa. Nesta execução ela foi executada; não confundir a execução sem argumentos com evidência de moderação.

Ao terminar, encerre a API pelo terminal e pare somente a instância criada neste exemplo:

```powershell
& "$taskPgBin/pg_ctl.exe" -D "$taskRoot/pgdata" -m fast -w stop
```

## Roteiro de demonstração sem internet

1. Build Angular e publicação da API em pasta isolada. Copiar `frontend/dist/radarplural/browser/*` para `wwwroot` dessa publicação **antes de iniciar** a API; frontend e API usam a mesma origem.
2. Criar base fictícia nova, aplicar migrações e carregar `Seed-Demo.sql`. As fontes `example.invalid` não devem ser abertas como notícias reais.
3. Executar os dois testes HTTP. Eles deixam comparações prontas nas histórias 1–3. Reservar histórias **5–8**, ainda sem comparação, para demonstrar consumo no navegador.
4. No navegador, criar conta fictícia, publicar um link de exemplo, salvar e abrir a coleção. Em “Histórias e fontes”, abrir uma comparação pronta: não consome quota.
5. Comparar histórias 5, 6 e 7: mostrar quota 1, 2 e 3. Tentar história 8: mostrar erro de quota. Mostrar concordância, divergência numérica, evidências e indicação “sem IA”.
6. Para repetir o consumo, preparar base nova: o cache é público e compartilhado, então apenas criar outra conta não remove comparações prontas.

Não é necessário ativar coleta, faturamento, API de IA ou alterar produção. O host corrigido deste ensaio ficou em `http://localhost:5095`, com base `coreletter_demo_final`; processos e dados estão em `artifacts/validacao-20261123`. Os hosts 5093/5094 foram auxiliares da validação anterior à correção e não devem ser usados como versão final.
