# Coreletter

Comunidade de tecnologia e IA para compartilhar links de notícias, podcasts e músicas, descobrir conteúdos num feed e salvar referências. Projeto acadêmico da equipe Squaders, com ideia aprovada pelo professor conforme relato do tech lead. Nome provisório.

## Entrega inicial

Base inicial publicada: ASP.NET Core .NET 10, PostgreSQL e Angular. O primeiro incremento cobre autenticação, feed com filtros e paginação, publicação/exclusão, salvamento privado e denúncia/moderação. [CI inicial aprovada](https://github.com/Pemonition/Coreletter/actions/runs/40426458380), incluindo cenário de API com PostgreSQL. Consulte backend/API.md para os contratos e docs/HANDOFF.md para limites e validações.

Comparação premium com IA, scraping controlado e integração financeira persistente são próximas entregas. Compartilhar uma URL não baixa seu conteúdo. Não há transcrição de áudio, cobrança real, postagem em redes sociais ou IA real conectada nesta base.

## Executar no PowerShell

Pré-requisitos: PowerShell 7 (`pwsh`), SDK .NET 10, Node compatível com a versão Angular fixada no frontend/package.json, npm e Docker Desktop em execução. Instale dependências com `dotnet restore backend/RadarPlural.slnx` e `npm ci --prefix frontend`.

Na raiz do projeto, prepare uma senha exclusivamente local:

```powershell
Copy-Item .env.example .env
notepad .env
```

Substitua o valor por pelo menos 12 letras/números, podendo usar `_` ou `-`. Não use senha pessoal. O arquivo `.env` é ignorado pelo Git. Depois:

```powershell
.\scripts\Start-Database.ps1
```

Em um segundo PowerShell na raiz:

```powershell
.\scripts\Start-Api.ps1
```

Em um terceiro PowerShell na raiz:

```powershell
.\scripts\Start-Web.ps1
```

Abra http://localhost:4200. API em http://localhost:5080/api/health. Banco em localhost:5433, acessível somente na interface local. Cadastre uma conta fictícia para demonstrar. O esquema é criado por migrações versionadas do EF Core (`backend/RadarPlural.Api/Migrations`), aplicadas automaticamente quando a API inicia.

Se o seu banco local foi criado antes das migrações (com `EnsureCreated`), a API vai falhar ao iniciar porque as tabelas já existem sem histórico. Como o banco local só tem dados fictícios, recrie-o uma vez com `docker compose down -v` e `.\scripts\Start-Database.ps1`.

Para criar uma nova migração depois de alterar entidades: `dotnet tool restore` e `dotnet ef migrations add NomeDaMudanca --project backend/RadarPlural.Api` (com `ConnectionStrings__Default` definido). Revise o arquivo gerado na PR.

Para validar a base:

```powershell
.\scripts\Check.ps1
```

Isso executa os testes do back-end e o build Angular; testes com PostgreSQL e fluxo no navegador devem ser registrados separadamente quando executados. Para parar o banco sem apagar dados: `docker compose stop`. Não use exclusão de volumes se quiser manter os dados.

Com a API e um banco descartável em execução, `pwsh -File scripts/Test-Api.ps1` verifica cadastro, publicação, permissões e isolamento de salvos. Ele cria contas fictícias e remove a publicação de teste. A CI executa esse cenário usando PostgreSQL descartável.

## Vídeos (HyperFrames)

Roteiro em JSON → composição HTML/GSAP → MP4 com a marca do projeto. Primeiro vídeo: "Como o Coreletter funciona". Guia completo, com o passo a passo no Windows: [video/README.md](video/README.md).
## Demonstração na nuvem (Render, plano gratuito)

A pasta raiz tem `Dockerfile` e `render.yaml`. Uma única imagem compila o Angular e o entrega pela própria API, então front e API ficam na mesma origem. Assim o cookie `SameSite=Strict` e a checagem de `Origin` continuam valendo, sem CORS. O Blueprint cria o PostgreSQL gratuito e o serviço web e liga um ao outro sem nenhuma senha no Git.

1. Entrar em https://render.com com a conta do GitHub do tech lead.
2. **New → Blueprint** → repositório `Coreletter` → branch `main`.
3. Deixar `BootstrapAdminEmail` vazio no primeiro deploy e clicar em **Apply**.
4. Abrir a URL `https://<servico>.onrender.com`, cadastrar a conta de administração da demonstração, preencher `BootstrapAdminEmail` com esse e-mail em **Environment** e salvar (o serviço reinicia e promove a conta).

Limites do plano gratuito, a considerar no ensaio da apresentação:
- O serviço hiberna após cerca de 15 minutos sem acesso; o primeiro acesso depois disso leva perto de 1 minuto.
- O PostgreSQL gratuito do Render **expira 30 dias após a criação**. Antes disso, migre para outro Postgres (por exemplo, Neon gratuito) trocando apenas a variável `ConnectionStrings__Default` no painel; URLs `postgresql://` são aceitas.
- Use somente contas e conteúdos fictícios: a URL é pública.

Decisão registrada em `docs/decisoes/0001-deploy-render.md`.

## Coletor de notícias (RSS/Atom)

O projeto `backend/RadarPlural.Worker` lê os feeds das fontes aprovadas em `backend/RadarPlural.Worker/fontes.json`, grava título, link, data e um trecho curto na tabela `FeedItems` e termina. A API mostra o resultado em `GET /api/news`. A política (fontes, SSRF, limites, direito autoral) está em `docs/decisoes/0002-ingestao.md`.

```powershell
# Ver o que seria coletado, sem banco:
dotnet run --project backend/RadarPlural.Worker -- --dry-run
# Gravar no banco local (a API precisa ter rodado uma vez para aplicar as migrações):
$env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=radarplural;Username=radarplural;Password=<a do .env>"
dotnet run --project backend/RadarPlural.Worker
# Só uma fonte:
dotnet run --project backend/RadarPlural.Worker -- --fonte tecnoblog
```

Rodar de novo não duplica notícias. Uma fonte fora do ar não interrompe as outras.

Depois de coletar, o Worker faz duas etapas sem IA (`docs/decisoes/0003-pipeline-auditoria-video.md`, guia em `docs/PIPELINE.md`):
- **Triagem:** descarta oferta/afiliado (caminhos em `excludeUrlContains` de cada fonte e padrões de título), resumos do dia e notícia com mais de 7 dias, gravando o motivo. Caça-clique só recebe uma marca. A cada execução, as notícias das últimas 72 h são reavaliadas pelas regras atuais.
- **Agrupamento:** junta a mesma notícia de veículos diferentes em uma **história** (`GET /api/stories`). Regra: 3+ palavras relevantes em comum, sendo ao menos 1 nome próprio, em até 48 h. Os ids das histórias se mantêm entre execuções.

O `--dry-run` mostra o que seria descartado e as histórias com 2+ fontes, sem banco.

Na nuvem, o GitHub Actions roda o coletor a cada 3 horas (`.github/workflows/ingestao.yml`). Para gravar no banco do Render, crie o secret **INGESTAO_DATABASE_URL** em *Settings → Secrets and variables → Actions*, com a *External Database URL* do PostgreSQL do Render terminada em `?sslmode=require`. Sem o secret, o workflow roda só `--dry-run`.

Para incluir uma fonte: o site precisa publicar RSS/Atom em https e não bloquear robôs. Use a URL final, depois de redirecionamentos (com ou sem `www`), porque o coletor não segue redirecionamento para fora da lista. Teste com `--dry-run --fonte <id>` e abra PR.

## Escala e Kubernetes

O sistema foi preparado para rodar várias cópias de cada parte (`docs/decisoes/0004-escala-desde-o-mvp.md`): API sem estado, etapas do pipeline reservando trabalho no banco com `FOR UPDATE SKIP LOCKED`, migração em passo único e teto de custo testado. Deploy local no Kubernetes do Docker Desktop: `deploy/k8s/README.md`. Teste de carga: `scripts/carga/`.

Testes de integração com PostgreSQL rodam quando há um servidor descartável:
```powershell
$env:INTEGRATION_DATABASE_URL = "Host=localhost;Port=5433;Database=postgres;Username=radarplural;Password=<a do .env>"
dotnet test backend/RadarPlural.slnx
```
Sem a variável, eles aparecem como ignorados.

## Colaboração

Repositório: https://github.com/Pemonition/Coreletter. Para a equipe iniciar:

```powershell
Set-Location C:\Users\$env:USERNAME\Dev
git clone https://github.com/Pemonition/Coreletter.git
Set-Location Coreletter
```

Se a pasta já existe, use-a e atualize a branch; não clone por cima. Aceite o convite do GitHub para enviar branches. Abra sua tarefa em https://github.com/Pemonition/Coreletter/issues antes de editar. As etapas de dependências, banco e execução estão acima.

Hugo (`Pemonition`) assume tech lead. Veja docs/EQUIPE.md e docs/BACKLOG.md para responsabilidades e tarefas futuras por nível. Davidy (`ddanjos`) é o sênior, Lindomar (`LindomarSSJ`) é o júnior e Henrique (`hikmiau`) é o iniciante. Todos devem contribuir com código próprio, revisão e entendimento; não atribuir automaticamente a humanos a autoria do código gerado por IA.

Uma branch por tarefa e PR revisado antes do merge. Não trabalhar diretamente em main. A revisão deve ser feita por outra pessoa, preferencialmente tech lead/sênior. Repositório público autorizado pelo tech lead; nenhuma licença pública de uso foi escolhida.

## Assistentes

AGENTS.md é a fonte compartilhada de regras. CLAUDE.md aponta para ela. Leia docs/HANDOFF.md ao trocar de ferramenta. Isso preserva decisões por arquivos, mas não transfere autenticação, tokens ou sessão entre provedores. Nenhum provedor pago é ativado automaticamente.

## Limites acadêmicos e de segurança

Use contas e conteúdo de demonstração fictícios, links permitidos e sem segredos. Origem e título fornecido pelo usuário devem ser identificáveis. Comparação futura deve citar evidências e admitir insuficiência, sem prometer verdade certificada. Quotas financeiras precisam de persistência e transação antes de chamadas de IA com custo real.
