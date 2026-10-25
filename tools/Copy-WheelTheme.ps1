param(
    [ValidateSet('NeoX', 'FPro')][string]$SourceWheel = 'NeoX',
    [ValidateSet('NeoX', 'FPro')][string]$TargetWheel = 'FPro',
    [string]$SimHubPath = 'C:\Program Files (x86)\SimHub'
)
$ErrorActionPreference = 'Stop'
if ($SourceWheel -eq $TargetWheel) { throw 'Choose two different wheels.' }
if (Get-Process -Name SimHubWPF -ErrorAction SilentlyContinue) {
    throw 'Exit SimHub before copying settings. No files have been changed.'
}
$settingsPath = Join-Path $SimHubPath 'PluginsData\Common\DilaPlugin.Settings.json'
$original = [IO.File]::ReadAllText($settingsPath)
$settings = $original | ConvertFrom-Json
if ($settings.Version -ne 1 -or !$settings.$SourceWheel -or !$settings.$TargetWheel) {
    throw 'Expected existing version 1 settings for both wheels.'
}
$source = $settings.$SourceWheel
$target = $settings.$TargetWheel
# Physical button numbers differ between wheels. Preserve the target's layout
# and TC/ABS assignments; copy only the shared theme/effect preferences.
$fields = @('StartColor', 'EndColor', 'Brightness', 'Mode', 'FadeSeconds',
    'RevFlash', 'PitFlash', 'FlashHz', 'RevFlashHz', 'FallbackRedlineRpm', 'ShowWhenIdle')
foreach ($field in $fields) {
    if ($source.PSObject.Properties.Name -notcontains $field) { throw "Missing source setting: $field" }
    $target | Add-Member -NotePropertyName $field -NotePropertyValue $source.$field -Force
}
$idleMode = $source.IdleMode
if ($null -eq $idleMode) { $idleMode = $source.Mode }
$racingMode = $source.RacingMode
if ($null -eq $racingMode) {
    $racingMode = if ($source.Mode -in @(1, 2)) { 3 } else { $source.Mode }
}
$target | Add-Member -NotePropertyName IdleMode -NotePropertyValue $idleMode -Force
$target | Add-Member -NotePropertyName RacingMode -NotePropertyValue $racingMode -Force
$json = $settings | ConvertTo-Json -Depth 20 -Compress
# Recheck before the write, in case SimHub was started during preparation.
if (Get-Process -Name SimHubWPF -ErrorAction SilentlyContinue) { throw 'SimHub started. No files have been changed.' }
if ([IO.File]::ReadAllText($settingsPath) -cne $original) { throw 'Settings changed during preparation; retry after closing SimHub.' }
$backupFolder = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\settings-backups'
New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
$backup = Join-Path $backupFolder ('DilaPlugin.Settings-' + [Guid]::NewGuid().ToString('N') + '.json')
Copy-Item -LiteralPath $settingsPath -Destination $backup
[IO.File]::WriteAllText($settingsPath, $json, (New-Object Text.UTF8Encoding($false)))
if ([IO.File]::ReadAllText($settingsPath) -cne $json) { throw "Verification failed. Original settings backed up at $backup" }
Write-Output "Copied shared theme/effect preferences from $SourceWheel to $TargetWheel. Preserved target button layout and TC/ABS LED assignments."
Write-Output "Backup: $backup"
