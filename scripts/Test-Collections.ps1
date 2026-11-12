param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
if ($BaseUrl -notmatch '^http://localhost:\d+$') { throw 'Use somente a API local de teste.' }
# Requires disposable PostgreSQL and a configured, approved source ID 'fixture'.
# Keep Collection__Enabled=false: this HTTP scenario must never fetch a live feed.
function Check($Condition, $Message) { if (-not $Condition) { throw $Message } }
function Request($Path, $Method = 'GET', $Body = $null, $Session = $null) {
    $args = @{ Uri = "$BaseUrl/api$Path"; Method = $Method; SkipHttpErrorCheck = $true }
    if ($Session) { $args.WebSession = $Session }
    if ($null -ne $Body) { $args.Body = ConvertTo-Json -InputObject $Body -Depth 8; $args.ContentType = 'application/json' }
    $response = Invoke-WebRequest @args
    $content = if ($response.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    [pscustomobject]@{ StatusCode = $response.StatusCode; Content = $content }
}
foreach ($path in @('/collections/profile','/collections/items','/collections/export','/collections/sources')) {
    Check ((Request $path).StatusCode -eq 401) "Endpoint privado exposto: $path"
}
$a = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$b = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
foreach ($session in @($a,$b)) {
    $body = @{ email = "fixture-$([guid]::NewGuid().ToString('N'))@example.invalid"; password = 'fixture-test-only-123'; displayName = 'Collection fixture' }
    Check ((Request '/auth/register' 'POST' $body $session).StatusCode -eq 200) 'Cadastro fixture falhou'
}
$new = Request '/collections/profile' 'GET' $null $a
Check ($new.StatusCode -eq 200 -and ($new.Content -eq '' -or $new.Content -eq 'null')) 'Conta nova possui perfil inesperado'
Check (((Request '/collections/sources' 'GET' $null $a).Content | ConvertFrom-Json).Count -eq 0) 'Coleta global deve estar desativada no teste'
$profile = @{ topic = 'IA'; keywords = @('IA'); excludedKeywords = @(); sourceIds = @('fixture'); intervalHours = 6; maxItems = 3; enabled = $false }
Check ((Request '/collections/profile' 'PUT' $profile $a).StatusCode -eq 200) 'Salvar questionário pausado falhou'
$profile.topic = 'Automação'
Check ((Request '/collections/profile' 'PUT' $profile $a).StatusCode -eq 200) 'Atualizar questionário pausado falhou'
$saved = (Request '/collections/profile' 'GET' $null $a).Content | ConvertFrom-Json
Check ($saved.topic -eq 'Automação' -and $saved.enabled -eq $false) 'PUT pausado ignorou alterações'
$other = Request '/collections/profile' 'GET' $null $b
Check ($other.Content -eq '' -or $other.Content -eq 'null') 'Perfil vazou entre contas'
$profile.enabled = $true
Check ((Request '/collections/profile' 'PUT' $profile $a).StatusCode -eq 400) 'Coleta global desativada aceitou ativação'
$profile.sourceIds = @('https://untrusted.example/feed')
Check ((Request '/collections/profile' 'PUT' $profile $a).StatusCode -eq 400) 'URL arbitrária foi aceita'
$export = (Request '/collections/export' 'GET' $null $a).Content | ConvertFrom-Json
Check ($export.topic -eq 'Automação' -and $export.limitations.Count -gt 0) 'Export sem tema/limites'
Check ((Request '/auth/logout' 'POST' $null $a).StatusCode -eq 204) 'Logout falhou'
Check ((Request '/collections/profile' 'GET' $null $a).StatusCode -eq 401) 'Perfil acessível após logout'
'PASS: autorização, conta nova, questionário pausado, atualização, isolamento, catálogo, desligamento global, export e logout.'
