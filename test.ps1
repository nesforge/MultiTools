param([switch]$SmokeOnly, [switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.dotnet-home/packages'
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'restore.ps1')
    if ($CoreOnly) {
        dotnet run --project tests/ClickMoves.Tests/ClickMoves.Tests.csproj -c Release --no-restore -- --core-only
    } elseif ($SmokeOnly) {
        dotnet run --project tests/ClickMoves.Tests/ClickMoves.Tests.csproj -c Release --no-restore -- --smoke-only
    } else {
        dotnet run --project tests/ClickMoves.Tests/ClickMoves.Tests.csproj -c Release --no-restore
    }
    if ($LASTEXITCODE -ne 0) { throw 'Hay pruebas fallidas; consulta TestResults.txt o SmokeResults.txt.' }
} finally { Pop-Location }
