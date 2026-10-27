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

Abra http://localhost:4200. API em http://localhost:5080/api/health. Banco em localhost:5433, acessível somente na interface local. Cadastre uma conta fictícia para demonstrar. O esquema inicial usa EnsureCreated em Development; migrações versionadas são uma tarefa futura antes de evoluir o banco com dados mantidos. Não use esta configuração como deploy de produção.

Para validar a base:

```powershell
.\scripts\Check.ps1
```

Isso executa os testes do back-end e o build Angular; testes com PostgreSQL e fluxo no navegador devem ser registrados separadamente quando executados. Para parar o banco sem apagar dados: `docker compose stop`. Não use exclusão de volumes se quiser manter os dados.

Com a API e um banco descartável em execução, `pwsh -File scripts/Test-Api.ps1` verifica cadastro, publicação, permissões e isolamento de salvos. Ele cria contas fictícias e remove a publicação de teste. A CI executa esse cenário usando PostgreSQL descartável.

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
