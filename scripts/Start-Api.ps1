$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (-not (Test-Path -LiteralPath '.env')) { throw 'Crie .env a partir de .env.example.' }
$taskPasswordLine = Get-Content -LiteralPath '.env' | Where-Object { $_ -match '^POSTGRES_PASSWORD=' } | Select-Object -First 1
if (-not $taskPasswordLine) { throw 'POSTGRES_PASSWORD ausente no .env.' }
$taskDbPassword = $taskPasswordLine.Substring('POSTGRES_PASSWORD='.Length)
if ($taskDbPassword -notmatch '^[A-Za-z0-9_-]{12,}$') { throw 'Use senha local com 12+ letras, numeros, _ ou - para este roteiro.' }
$env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=radarplural;Username=radarplural;Password=$taskDbPassword"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/RadarPlural.Api --urls http://localhost:5080
if ($LASTEXITCODE -ne 0) { throw 'A API encerrou com erro.' }
