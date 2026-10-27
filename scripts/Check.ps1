$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet test backend/RadarPlural.slnx
if ($LASTEXITCODE -ne 0) { throw 'Testes do back-end falharam.' }
Push-Location frontend
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw 'Instalacao do front-end falhou.' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Build do front-end falhou.' }
} finally { Pop-Location }
