$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & (Join-Path $PSScriptRoot 'check-publication.ps1')
    $files = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'No se pudo obtener el inventario público.' }
    $destination = Join-Path $taskRoot ('dist/review-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($file in $files) {
        $source = Get-Item -LiteralPath $file
        if (($source.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'No se exportan enlaces simbólicos.' }
        $target = [IO.Path]::GetFullPath((Join-Path $destination $file))
        if (-not $target.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta fuera de la carpeta de revisión.' }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath $source.FullName -Destination $target
    }
    # .NET ZipFile includes dotfiles; Compress-Archive can omit hidden files.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Join-Path $taskRoot ('dist/MiniTools-GitHub-review-' + [Guid]::NewGuid().ToString('N') + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($destination, $archive)
    $files | Set-Content -LiteralPath (Join-Path $destination 'PUBLIC-FILES.txt') -Encoding utf8
    Write-Output "Carpeta de revisión: $destination"
    Write-Output "Archivo: $archive"
    Write-Output "Archivos: $($files.Count); SHA256: $((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash)"
} finally { Pop-Location }
