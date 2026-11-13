param(
    [Parameter(Mandatory)][string]$Id,
    [Parameter(Mandatory)][ValidateSet('approve','disable')][string]$Action,
    [Parameter(Mandatory)][string]$Reason
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 300) { throw 'Informe um motivo de 1 a 300 caracteres, sem dados pessoais.' }
$configPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'backend/RadarPlural.Api/appsettings.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$matches = @($config.Collection.Sources | Where-Object Id -CEQ $Id)
if ($matches.Count -ne 1) { throw 'Identificador ausente ou duplicado no catálogo.' }
$matches[0].Approved = $Action -eq 'approve'
# Read/review the Git diff and commit this change; API restart is required.
$config | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $configPath -Encoding utf8
Write-Output "Fonte $Id : $Action. Motivo para registrar no commit: $Reason"
Write-Output 'Revise o diff, registre o motivo no commit e reinicie a API. A coleta global não foi alterada.'
