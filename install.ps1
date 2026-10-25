param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist\DilaRacing.SimHub.dll'
if (!(Test-Path -LiteralPath $source)) { throw 'Build the plugin first with build.ps1.' }
if (!(Test-Path -LiteralPath (Join-Path $SimHubPath 'SimHubWPF.exe'))) { throw 'SimHubWPF.exe was not found in the target folder.' }
if (Get-Process -Name SimHubWPF -ErrorAction SilentlyContinue) { throw 'Exit SimHub before installing. No files have been changed.' }
$target = Join-Path $SimHubPath 'DilaRacing.SimHub.dll'
if (Test-Path -LiteralPath $target) {
    $backupFolder = Join-Path $PSScriptRoot 'artifacts\plugin-backups'
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupFolder ('DilaRacing.SimHub-' + [Guid]::NewGuid().ToString('N') + '.dll'))
}
Copy-Item -LiteralPath $source -Destination $target -Force
$expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'Installed DLL hash does not match the build.' }
Write-Output "Installed and verified: $target"
Write-Output 'Start SimHub, enable Dila Racing, then follow README.md to import the button profiles.'
