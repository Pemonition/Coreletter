$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Execute gh auth login -h github.com.' }
$taskRepo = gh repo view --json nameWithOwner --jq .nameWithOwner
if ($LASTEXITCODE -ne 0 -or $taskRepo -ne 'Pemonition/Coreletter') { throw 'O remoto deve apontar para Pemonition/Coreletter.' }
foreach ($taskMember in @('ddanjos', 'LindomarSSJ', 'hikmiau')) {
    gh api --method PUT "repos/$taskRepo/collaborators/$taskMember" -f permission=push --silent
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel convidar $taskMember. Confira os convites antes de repetir." }
    Write-Host "Acesso de escrita concedido ou convite enviado: $taskMember"
}
Write-Host 'Cada integrante precisa aceitar o convite. Nao foi concedido acesso administrativo.'
