param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$taskSuffix = [guid]::NewGuid().ToString('N')
$taskPassword = [guid]::NewGuid().ToString('N')
$taskSessionA = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$taskSessionB = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Request([string]$Method, [string]$Path, $Session, $Body = $null) {
    $taskArgs = @{Uri="$BaseUrl/api$Path";Method=$Method;WebSession=$Session;SkipHttpErrorCheck=$true}
    if ($null -ne $Body) { $taskArgs.Body = $Body | ConvertTo-Json; $taskArgs.ContentType = 'application/json' }
    Invoke-WebRequest @taskArgs
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
} finally {
    Expect (Request DELETE "/posts/$($taskPost.id)" $taskSessionA) 204 'Autor exclui e remove dependentes'
}
Write-Host 'Cenario de API concluido. Contas ficticias permanecem no banco de teste; use banco descartavel.'
