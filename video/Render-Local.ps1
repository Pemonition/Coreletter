$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
# Use the already installed HyperFrames encoder only for this process.
$encoderFolder = Join-Path $env:LOCALAPPDATA 'Programs/HyperFrames/resources/app.asar.unpacked/vendor/ffmpeg/win32-x64'
$previousPath = $env:PATH
try {
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $encoderFolder 'ffmpeg.exe'))) {
        $env:PATH = "$encoderFolder;$env:PATH"
    }
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue) -or -not (Get-Command ffprobe -ErrorAction SilentlyContinue)) {
        throw 'FFmpeg e FFprobe precisam estar disponíveis; nenhuma instalação automática foi feita.'
    }
    npm run render
    if ($LASTEXITCODE -ne 0) { throw 'Renderização não concluída.' }
} finally { $env:PATH = $previousPath }
