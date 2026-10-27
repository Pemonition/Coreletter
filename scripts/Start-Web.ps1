$ErrorActionPreference = 'Stop'
Set-Location (Join-Path (Split-Path $PSScriptRoot -Parent) 'frontend')
npm start
if ($LASTEXITCODE -ne 0) { throw 'O front-end encerrou com erro.' }
