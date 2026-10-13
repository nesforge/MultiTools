param([ValidateSet('win-x64','win-arm64','win-x86')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.dotnet-home/packages'
Push-Location $PSScriptRoot
try {
    dotnet restore src/ClickMoves/ClickMoves.csproj "-p:RuntimeIdentifier=$Runtime" --locked-mode -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración bloqueada del programa.' }
    if ($Runtime -eq 'win-x64') {
        dotnet restore tests/ClickMoves.Tests/ClickMoves.Tests.csproj --locked-mode -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración bloqueada de las pruebas.' }
    }
} finally { Pop-Location }
