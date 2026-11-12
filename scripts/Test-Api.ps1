# Cenario de API ponta a ponta (roda na CI depois do dotnet test). Uso:
#   ./scripts/Test-Api.ps1                                   (sem fluxo de administrador)
#   ./scripts/Test-Api.ps1 -AdminEmail x -AdminPassword y    (conta ja promovida via BootstrapAdminEmail)
# Issue #16 (B08): tipos podcast/musica, filtros, paginacao, Origin de outro site e fluxo do administrador.
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$AdminEmail = '',
    [string]$AdminPassword = ''
)
$ErrorActionPreference = 'Stop'
$taskSuffix = [guid]::NewGuid().ToString('N')
$taskPassword = [guid]::NewGuid().ToString('N')
$taskSessionA = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$taskSessionB = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Request([string]$Method, [string]$Path, $Session, $Body = $null, [hashtable]$Headers = @{}) {
    $taskArgs = @{Uri="$BaseUrl/api$Path";Method=$Method;WebSession=$Session;SkipHttpErrorCheck=$true;Headers=$Headers}
    if ($null -ne $Body) { $taskArgs.Body = $Body | ConvertTo-Json; $taskArgs.ContentType = 'application/json' }
    Invoke-WebRequest @taskArgs
}
function Json($Response) { $Response.Content | ConvertFrom-Json }
function Check([bool]$Condition, [string]$Name, [string]$Detail = '') {
    if (-not $Condition) { throw "$Name : falhou. $Detail" }
    Write-Host "OK: $Name"
}
function Expect($Response, [int]$Status, [string]$Name) {
    if ([int]$Response.StatusCode -ne $Status) { throw "$Name : esperado $Status, recebido $($Response.StatusCode). $($Response.Content)" }
    Write-Host "OK: $Name"
}
Expect (Request GET '/auth/me' $taskSessionA) 401 'Anonimo nao recebe identidade'
foreach ($taskPair in @(@{Session=$taskSessionA;Name='Leitor A';Email="a-$taskSuffix@example.invalid"},@{Session=$taskSessionB;Name='Leitor B';Email="b-$taskSuffix@example.invalid"})) {
    Expect (Request POST '/auth/register' $taskPair.Session @{email=$taskPair.Email;password=$taskPassword;displayName=$taskPair.Name}) 200 'Cadastro ficticio e sessao'
}
$taskPostResponse = Request POST '/posts' $taskSessionA @{url="https://example.org/$taskSuffix";title='Publicacao de teste';topic='IA';type='article';comment='Teste automatizado com conteudo ficticio'}
Expect $taskPostResponse 201 'Criar publicacao'
$taskPost = $taskPostResponse.Content | ConvertFrom-Json
$taskExtra = [System.Collections.Generic.List[string]]::new()
try {
    Expect (Request DELETE "/posts/$($taskPost.id)" $taskSessionB) 403 'Outro usuario nao exclui'
    Expect (Request PUT "/posts/$($taskPost.id)/save" $taskSessionB) 204 'Salvar'
    Expect (Request PUT "/posts/$($taskPost.id)/save" $taskSessionB) 204 'Salvar repetido e idempotente'
    $taskSavedA = (Request GET '/posts/saved' $taskSessionA).Content | ConvertFrom-Json
    if (@($taskSavedA.items | Where-Object id -eq $taskPost.id).Count -ne 0) { throw 'Salvamento privado vazou entre contas.' }
    $taskSavedB = (Request GET '/posts/saved' $taskSessionB).Content | ConvertFrom-Json
    if (@($taskSavedB.items | Where-Object id -eq $taskPost.id).Count -ne 1) { throw 'Item salvo ausente ou duplicado.' }
    Expect (Request POST "/posts/$($taskPost.id)/reports" $taskSessionB @{reason='Denuncia ficticia para teste'}) 204 'Denunciar'
    Expect (Request GET '/moderation/reports' $taskSessionB) 403 'Leitor nao acessa moderacao'
    Expect (Request POST '/posts' $taskSessionB @{url='javascript:alert(1)';title='Invalida';topic='IA';type='article'}) 400 'Rejeitar URL invalida'
    Expect (Request POST '/posts' $taskSessionB @{url='https://example.org';title='';topic='IA';type='article'}) 400 'Rejeitar titulo vazio'

    # --- Tipos, filtros e paginacao (#16) ---
    # Tema unico por execucao: o banco de teste pode ter publicacoes de outras rodadas.
    $taskTopic = "T$($taskSuffix.Substring(0, 12))"
    foreach ($taskType in 'podcast', 'music') {
        $r = Request POST '/posts' $taskSessionA @{url="https://example.org/$taskType/$taskSuffix";title="Teste $taskType";topic=$taskTopic;type=$taskType}
        Expect $r 201 "Publicar $taskType"
        $taskExtra.Add((Json $r).id)
    }
    $r = Request POST '/posts' $taskSessionA @{url="https://example.org/artigo/$taskSuffix";title='Teste artigo';topic=$taskTopic;type='article'}
    Expect $r 201 'Publicar artigo no tema do teste'
    $taskExtra.Add((Json $r).id)
    Expect (Request POST '/posts' $taskSessionA @{url='https://example.org/x';title='Tipo invalido';topic='IA';type='video'}) 400 'Rejeitar tipo invalido'
    $f = Json (Request GET "/posts?topic=$taskTopic" $taskSessionB)
    Check ($f.total -eq 3) 'Filtro por tema' "total=$($f.total), esperado 3"
    $f = Json (Request GET "/posts?topic=$($taskTopic.ToLower())" $taskSessionB)
    Check ($f.total -eq 3) 'Filtro por tema ignora maiusculas' "total=$($f.total)"
    $f = Json (Request GET "/posts?topic=$taskTopic&type=podcast" $taskSessionB)
    Check ($f.total -eq 1 -and $f.items[0].type -eq 'podcast') 'Filtro por tema e tipo' "total=$($f.total)"
    $f = Json (Request GET "/posts?topic=$taskTopic&pageSize=2&page=2" $taskSessionB)
    Check (@($f.items).Count -eq 1 -and $f.total -eq 3 -and $f.page -eq 2) 'Paginacao: segunda pagina com o resto' "itens=$(@($f.items).Count)"
    $f = Json (Request GET "/posts?topic=$taskTopic&pageSize=2&page=1" $taskSessionB)
    $f2 = Json (Request GET "/posts?topic=$taskTopic&pageSize=2&page=2" $taskSessionB)
    Check (@(@($f.items.id) + @($f2.items.id) | Select-Object -Unique).Count -eq 3) 'Paginacao sem repetir nem perder item'
    Expect (Request GET '/posts?page=0' $taskSessionB) 400 'Rejeitar pagina 0'

    # --- Protecao de origem (CSRF) (#16) ---
    Expect (Request POST '/posts' $taskSessionB @{url='https://example.org/csrf';title='CSRF';topic='IA';type='article'} @{Origin='https://site-malicioso.example'}) 403 'POST com Origin de outro site e bloqueado'
    Expect (Request PUT "/posts/$($taskPost.id)/save" $taskSessionB $null @{Origin='https://site-malicioso.example'}) 403 'PUT com Origin de outro site e bloqueado'
    $taskOrigin = ([Uri]$BaseUrl).GetLeftPart([UriPartial]::Authority)
    Expect (Request PUT "/posts/$($taskPost.id)/save" $taskSessionB $null @{Origin=$taskOrigin}) 204 'Mesma origem passa'
    Expect (Request GET '/posts?page=1' $taskSessionB $null @{Origin='https://site-malicioso.example'}) 200 'GET de outro site nao e bloqueado (so leitura)'
    # Cuidado: com -WebSession, o Invoke-WebRequest GUARDA os -Headers na sessao. Sem limpar, o Origin falso
    # vaza para as proximas chamadas e qualquer teste "espera 403" passa pelo motivo errado.
    $taskSessionB.Headers.Remove('Origin') | Out-Null
    Expect (Request GET '/auth/me' $taskSessionB) 200 'Sessao limpa depois do teste de origem'

    # --- Administrador (#16) ---
    Expect (Request PUT "/moderation/posts/$($taskPost.id)/hide" $taskSessionB) 403 'Leitor nao oculta'
    if ($AdminEmail) {
        $taskAdmin = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        Expect (Request POST '/auth/login' $taskAdmin @{email=$AdminEmail;password=$AdminPassword}) 200 'Login do administrador'
        Check ((Json (Request GET '/auth/me' $taskAdmin)).isAdmin -eq $true) 'Conta tem papel de administrador' 'Confira BootstrapAdminEmail e reinicie a API depois do cadastro.'
        $reports = Json (Request GET '/moderation/reports' $taskAdmin)
        Check (@($reports | Where-Object postId -eq $taskPost.id).Count -ge 1) 'Administrador ve a denuncia'
        Expect (Request PUT "/moderation/posts/$($taskPost.id)/hide" $taskAdmin) 204 'Administrador oculta'
        $f = Json (Request GET '/posts?pageSize=50' $taskSessionB)
        Check (@($f.items | Where-Object id -eq $taskPost.id).Count -eq 0) 'Publicacao oculta some do feed'
        Expect (Request POST "/posts/$($taskPost.id)/reports" $taskSessionB @{reason='Denuncia de item oculto'}) 404 'Nao denuncia item oculto'
        Expect (Request PUT "/moderation/posts/$([guid]::NewGuid())/hide" $taskAdmin) 404 'Ocultar id inexistente da 404'
    } else {
        Write-Host 'PULADO: fluxo do administrador (informe -AdminEmail e -AdminPassword).'
    }
} finally {
    foreach ($taskId in $taskExtra) { Expect (Request DELETE "/posts/$taskId" $taskSessionA) 204 'Limpar publicacao extra' }
    Expect (Request DELETE "/posts/$($taskPost.id)" $taskSessionA) 204 'Autor exclui e remove dependentes'
}
Write-Host 'Cenario de API concluido. Contas ficticias permanecem no banco de teste; use banco descartavel.'
