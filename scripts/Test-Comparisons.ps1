# Execute depois de Seed-Demo.sql numa base local descartável e nova.
param([string]$BaseUrl = 'http://localhost:5093')
$ErrorActionPreference = 'Stop'
$taskUri = [Uri]$BaseUrl
if (-not $taskUri.IsLoopback) { throw 'Este cenário usa somente uma API local com banco descartável.' }
$taskSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Request([string]$Method, [string]$Path, $Body = $null, [hashtable]$Headers = @{}) {
    # Invoke-WebRequest pode guardar headers na sessão; cada chamada define sua própria chave.
    $taskSession.Headers.Remove('Idempotency-Key') | Out-Null
    $taskArgs = @{ Uri = "$BaseUrl/api$Path"; Method = $Method; WebSession = $taskSession; SkipHttpErrorCheck = $true; Headers = $Headers }
    if ($null -ne $Body) { $taskArgs.Body = $Body | ConvertTo-Json; $taskArgs.ContentType = 'application/json' }
    Invoke-WebRequest @taskArgs
}
function Expect($Response, [int]$Status, [string]$Name) {
    if ([int]$Response.StatusCode -ne $Status) { throw "$Name : esperado $Status, recebido $($Response.StatusCode)." }
    Write-Host "OK: $Name"
}
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "$Name : falhou." }
    Write-Host "OK: $Name"
}
function StoryId([int]$Number) { '11111111-1111-4111-8111-' + $Number.ToString('000000000000') }
foreach ($taskNumber in 1..4) {
    Expect (Request GET "/stories/$(StoryId $taskNumber)/comparison") 404 'Fixture sem comparação anterior (base nova)'
}
Expect (Request POST '/comparisons' @{ storyId = (StoryId 1) }) 401 'Anônimo não solicita comparação'
$taskAccount = @{ email = "quota-$([Guid]::NewGuid().ToString('N'))@example.invalid"; password = [Guid]::NewGuid().ToString('N'); displayName = 'Teste de quota fictício' }
$taskRegister = Request POST '/auth/register' $taskAccount
Expect $taskRegister 200 'Cadastro da conta de teste'
$taskMember = $taskRegister.Content | ConvertFrom-Json
Expect (Request PUT "/admin/members/$($taskMember.id)/plan" @{ planId = 'plus' }) 403 'Usuário comum não altera plano'
$taskKey = [Guid]::NewGuid().ToString()
$taskFirst = Request POST '/comparisons' @{ storyId = (StoryId 1) } @{ 'Idempotency-Key' = $taskKey }
Expect $taskFirst 200 'Comparação simulada'
$taskResult = $taskFirst.Content | ConvertFrom-Json
Check ($taskResult.comparison.simulated -eq $true -and @($taskResult.comparison.sources).Count -eq 2) 'Duas fontes e modo simulado explícito'
Check ($taskResult.usage.units -eq 1) 'Primeira comparação consome uma unidade'
Expect (Request POST '/comparisons' @{ storyId = (StoryId 1) } @{ 'Idempotency-Key' = $taskKey }) 200 'Repetição da mesma solicitação'
$taskUsage = (Request GET '/me/usage').Content | ConvertFrom-Json
Check ($taskUsage.used -eq 1) 'Repetição/cache não desconta novamente'
Expect (Request POST '/comparisons' @{ storyId = (StoryId 4) } @{ 'Idempotency-Key' = $taskKey }) 503 'Chave de outra história não autoriza execução gratuita'
foreach ($taskNumber in 2..3) {
    Expect (Request POST '/comparisons' @{ storyId = (StoryId $taskNumber) } @{ 'Idempotency-Key' = [Guid]::NewGuid().ToString() }) 200 'Comparação dentro da quota'
}
$taskUsage = (Request GET '/me/usage').Content | ConvertFrom-Json
Check ($taskUsage.used -eq 3 -and $taskUsage.quota -eq 3) 'Plano gratuito chega a três de três'
Expect (Request POST '/comparisons' @{ storyId = (StoryId 4) } @{ 'Idempotency-Key' = [Guid]::NewGuid().ToString() }) 403 'Quarta comparação bloqueada'
Expect (Request POST '/comparisons' @{ storyId = [Guid]::NewGuid().ToString() }) 404 'História inexistente'
Expect (Request POST '/auth/logout') 204 'Logout'
Expect (Request GET "/stories/$(StoryId 1)/comparison") 200 'Comparação pronta é pública'
Expect (Request GET '/me/usage') 401 'Consumo permanece privado'
Write-Host 'Cenário de comparação concluído; fixtures e conta fictícia permanecem no banco descartável.'
