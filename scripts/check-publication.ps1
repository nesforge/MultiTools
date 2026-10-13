param([switch]$Staged)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    if ($Staged) { $files = @(git -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR) }
    else { $files = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique) }
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'No se pudo obtener una lista de archivos para revisar.' }
    $rules = [ordered]@{
        PrivateKey = '-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY(?: BLOCK)?-----'
        Token = '(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{35,}|AKIA[0-9A-Z]{16}|sk-[A-Za-z0-9]{30,})'
        Credential = '(?i)(?:api[_-]?key|secret|password|token)\s*[:=]\s*["''][A-Za-z0-9_+/=-]{16,}["'']'
        PrivatePath = '(?i)[A-Z]:[\\/]Users[\\/][^\\/\s"'']+'
    }
    $problems = @()
    foreach ($file in $files) {
        git check-ignore --no-index -q -- $file
        if ($LASTEXITCODE -eq 0) { $problems += "${file}: archivo ignorado pero incluido en Git"; continue }
        if ($LASTEXITCODE -ne 1) { throw 'Falló la comprobación de exclusiones de Git.' }
        if ($file -match '(?i)(^|/)(\.git|\.env(?:\..*)?|\.aws|\.codex|\.agents|bin|obj|dist|TestResults)(/|$)' -and $file -ne '.env.example' -or
            $file -match '(?i)\.(exe|zip|pem|key|pfx|p12|log|bak|tmp)$' -or $file -match '(?i)Results(?:-.*)?\.txt$') {
            $problems += "${file}: material privado o generado"; continue
        }
        if ($file -match '(?i)\.(png|ico)$') { continue }
        if ($Staged) {
            $size = git cat-file -s ":$file"
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer el índice de Git.' }
        } else { $size = (Get-Item -LiteralPath $file).Length }
        if ([long]$size -gt 2MB) { $problems += "${file}: texto demasiado grande para la revisión automática"; continue }
        if ($Staged) {
            $lines = @(git show ":$file")
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer el contenido preparado para el commit.' }
        } else { $lines = @(Get-Content -LiteralPath $file) }
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($rule in $rules.Keys) {
                if ($lines[$i] -cmatch $rules[$rule]) { $problems += "${file}:$($i + 1): $rule (valor oculto)" }
            }
        }
    }
    if ($problems.Count) { $problems | Write-Output; throw 'La revisión encontró archivos que requieren inspección.' }
    # git check-ignore returns 1 for the normal case (the file is not ignored).
    # Clear that expected native exit code so callers can rely on this checker's result.
    $global:LASTEXITCODE = 0
    Write-Output "$($files.Count) archivos revisados: sin coincidencias en los patrones configurados. Revisar también el diff y los binarios."
} finally { Pop-Location }
