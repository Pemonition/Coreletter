# Renderiza o video no Windows usando o FFmpeg que ja vem com o HyperFrames desktop (ideia da PR #31).
# Uso, na pasta video:   pwsh -File Render-Local.ps1
# Nao instala nada e nao muda o PATH permanente: so usa o FFmpeg do app durante esta execucao.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$encoderFolder = Join-Path $env:LOCALAPPDATA 'Programs/HyperFrames/resources/app.asar.unpacked/vendor/ffmpeg/win32-x64'
$previousPath = $env:PATH
try {
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $encoderFolder 'ffmpeg.exe'))) {
        $env:PATH = "$encoderFolder;$env:PATH"
    }
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue) -or -not (Get-Command ffprobe -ErrorAction SilentlyContinue)) {
        throw 'FFmpeg e FFprobe precisam estar disponiveis (HyperFrames desktop ou PATH); nada foi instalado.'
    }
    npm run check
    if ($LASTEXITCODE -ne 0) { throw 'hyperframes check encontrou problemas: corrija antes de renderizar.' }
    npm run render
    if ($LASTEXITCODE -ne 0) { throw 'Renderizacao nao concluida.' }
    Write-Host 'Pronto: renders/como-funciona.mp4'
} finally { $env:PATH = $previousPath }
