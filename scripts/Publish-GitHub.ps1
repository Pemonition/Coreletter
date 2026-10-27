param([string]$RepositoryName = 'Coreletter')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Execute gh auth login -h github.com no seu terminal e tente novamente.' }
$taskLogin = gh api user --jq .login
if ($LASTEXITCODE -ne 0 -or $taskLogin -ne 'Pemonition') { throw 'A conta autenticada deve ser Pemonition. Confira o login.' }
if ($RepositoryName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Nome de repositorio invalido.' }
if (-not (Test-Path -LiteralPath '.git')) { throw 'O repositorio Git local precisa estar inicializado e revisado.' }
git diff --exit-code
if ($LASTEXITCODE -ne 0) { throw 'Revise e registre as alteracoes antes da publicacao.' }
if (git status --porcelain) { throw 'Ha arquivos pendentes; revise-os antes de publicar.' }
$taskExistingRemote = git remote get-url origin 2>$null
if ($taskExistingRemote) { throw 'origin ja existe. Confira o remoto e use git push manualmente; este roteiro nao o substitui.' }
gh repo create "$taskLogin/$RepositoryName" --public --source . --remote origin --push --description 'TCC Squaders: comunidade de links de tecnologia e IA, ASP.NET Core, Angular e PostgreSQL'
if ($LASTEXITCODE -ne 0) { throw 'Publicacao nao concluida. Confira o estado remoto antes de repetir.' }
gh repo view --json url --jq .url
Write-Host 'Repositorio publicado. Convites e atribuicoes sao uma etapa separada apos confirmar os integrantes.'
