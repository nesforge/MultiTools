param([ValidateSet('win-x64','win-arm64','win-x86')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.dotnet-home/packages'
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'restore.ps1') -Runtime $Runtime
    dotnet publish src/ClickMoves/ClickMoves.csproj -c Release -r $Runtime --self-contained true --no-restore -warnaserror "-p:PublishDir=../../dist/$Runtime/"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el ejecutable.' }
    $taskExe = Get-Item -LiteralPath (Join-Path $PSScriptRoot "dist/$Runtime/MiniTools.exe")
    if ((Get-ChildItem -LiteralPath $taskExe.DirectoryName -File).Count -ne 1) { throw 'La carpeta de publicación debe contener solo MiniTools.exe.' }
    if ($Runtime -eq 'win-x64' -and $taskExe.Length -gt 60000000) { throw 'El .exe supera el presupuesto de 60 MB; revisa el empaquetado y actualiza la medición antes de distribuir.' }
    Write-Output ("Tamaño: {0:N0} bytes ({1:N2} MB)" -f $taskExe.Length, ($taskExe.Length / 1000000))
} finally { Pop-Location }
Write-Output "Ejecutable: $PSScriptRoot\dist\$Runtime\MiniTools.exe"
