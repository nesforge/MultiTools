$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $taskRoot ('TestResults/publication-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'scripts/check-publication.ps1') -Destination (Join-Path $fixture 'scripts')
Push-Location $fixture
try {
    git init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el Git temporal.' }
    '.env' | Set-Content -LiteralPath .gitignore
    'Datos ficticios de prueba' | Set-Content -LiteralPath README.md
    & ./scripts/check-publication.ps1 | Out-Null
    # Deliberately synthetic; assembled so the test source does not look like a real token.
    (('gh' + 'p_') + ('A' * 36)) | Set-Content -LiteralPath fixture.txt
    $blocked = $false
    try { & ./scripts/check-publication.ps1 | Out-Null } catch { $blocked = $true }
    if (-not $blocked) { throw 'El patrón de token no fue detectado.' }
    git add -- fixture.txt
    if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación del token ficticio en el índice.' }
    'Solo datos ficticios' | Set-Content -LiteralPath fixture.txt
    & ./scripts/check-publication.ps1 | Out-Null
    $blocked = $false
    try { & ./scripts/check-publication.ps1 -Staged | Out-Null } catch { $blocked = $true }
    if (-not $blocked) { throw 'Se revisó el working tree en lugar del índice.' }
    git add -- fixture.txt
    if ($LASTEXITCODE -ne 0) { throw 'Falló la actualización del fixture.' }
    & ./scripts/check-publication.ps1 -Staged | Out-Null
    'EXAMPLE=demo' | Set-Content -LiteralPath .env
    git add -f -- .env
    if ($LASTEXITCODE -ne 0) { throw 'Falló el fixture rastreado.' }
    $blocked = $false
    try { & ./scripts/check-publication.ps1 | Out-Null } catch { $blocked = $true }
    if (-not $blocked) { throw 'Un archivo ignorado pero rastreado no fue bloqueado.' }
    Write-Output 'PASS: control de publicación verifica contenido real del índice, detecta token ficticio y bloquea .env rastreado.'
} finally { Pop-Location }
