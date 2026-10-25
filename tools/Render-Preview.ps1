param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$argsList = @('/nologo', '/target:exe', ('/out:' + (Join-Path $root 'dist\DilaRacing.Preview.exe')),
    '/reference:System.Drawing.dll',
    ('/reference:' + (Join-Path $framework 'System.Xaml.dll')),
    ('/reference:' + (Join-Path $root 'dist\DilaRacing.SimHub.dll')))
foreach ($name in @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll')) {
    $argsList += '/reference:' + (Join-Path $framework ('WPF\' + $name))
}
foreach ($name in @('SimHub.Plugins.dll','GameReaderCommon.dll','GongSolutions.WPF.DragDrop.dll','WoteverCommon.dll','Newtonsoft.Json.dll')) {
    $argsList += '/reference:' + (Join-Path $SimHubPath $name)
}
$argsList += Join-Path $PSScriptRoot 'Preview.cs'
& (Join-Path $framework 'csc.exe') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Preview compilation failed.' }
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force | Out-Null
& (Join-Path $root 'dist\DilaRacing.Preview.exe') $SimHubPath (Join-Path $root 'artifacts\settings-preview.png')
if ($LASTEXITCODE -ne 0) { throw 'Preview failed.' }
