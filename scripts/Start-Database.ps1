$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (-not (Test-Path -LiteralPath '.env')) {
    throw 'Copie .env.example para .env e escolha uma senha local antes de iniciar.'
}
docker compose up -d --wait postgres
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar PostgreSQL. Confira se Docker Desktop esta em execucao.' }
