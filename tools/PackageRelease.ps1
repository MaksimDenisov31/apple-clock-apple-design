$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    & cmd /c Build.bat
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
    & cmd /c BuildInstaller.bat
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    $output = Join-Path $repo 'installer-output'
    $zip = Join-Path $output 'AppleClock-Portable-1.0.9.zip'
    Compress-Archive -LiteralPath @('AppleClock.exe','AppleClock.exe.config','README.md','docs','assets') -DestinationPath $zip -Force
    $lines = foreach ($name in @('AppleClock-Setup-1.0.9.exe','AppleClock-Portable-1.0.9.zip')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }
    [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$lines, [Text.Encoding]::ASCII)
    $lines
} finally { Pop-Location }
