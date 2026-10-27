$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$taskRepo = gh repo view --json nameWithOwner --jq .nameWithOwner
if ($LASTEXITCODE -ne 0 -or $taskRepo -ne 'Pemonition/Coreletter') { throw 'Publique primeiro em Pemonition/Coreletter.' }
$taskContent = Get-Content -LiteralPath 'docs/BACKLOG.md' -Raw -Encoding UTF8
$taskMatches = [regex]::Matches($taskContent, '(?ms)^### (B\d{2})[^\r\n]*\r?\n.*?(?=^### |\z)')
$taskOwners = @{B01='Pemonition';B02='LindomarSSJ';B03='hikmiau';B04='Pemonition';B05='ddanjos';B06='LindomarSSJ';B07='ddanjos';B08='hikmiau';B09='ddanjos';B10='Pemonition'}
$taskExistingJson = gh issue list --repo $taskRepo --state all --limit 100 --json title
if ($LASTEXITCODE -ne 0) { throw 'Falha ao conferir issues existentes.' }
$taskTitles = @($taskExistingJson | ConvertFrom-Json | ForEach-Object { $_.title })
foreach ($taskMatch in $taskMatches) {
    $taskId = $taskMatch.Groups[1].Value
    $taskTitle = ($taskMatch.Value -split '\r?\n')[0] -replace '^### ', ''
    if ($taskTitles -contains $taskTitle) { Write-Host "Ja existe: $taskId"; continue }
    $taskBody = $taskMatch.Value + "`n`nResponsavel proposto: @" + $taskOwners[$taskId] + ". Revisao: tech lead ou senior; para Henrique, pareamento com Davidy. Ver docs/EQUIPE.md e a tabela de dependencias em docs/BACKLOG.md. Somente assumir a tarefa quando os pre-requisitos estiverem cumpridos."
    $taskBodyFile = Join-Path ([System.IO.Path]::GetTempPath()) ('coreletter-issue-' + [guid]::NewGuid() + '.md')
    try {
        Set-Content -LiteralPath $taskBodyFile -Value $taskBody -Encoding UTF8
        # A atribuicao nominal consta no corpo; o assignee pode ser aplicado apos aceitar o convite.
        gh issue create --repo $taskRepo --title $taskTitle --body-file $taskBodyFile
        if ($LASTEXITCODE -ne 0) { throw "Falha criando $taskId; confira o GitHub antes de repetir." }
    } finally { Remove-Item -LiteralPath $taskBodyFile -ErrorAction SilentlyContinue }
}
