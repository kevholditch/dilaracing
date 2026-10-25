param([string]$SimHubPath = 'C:\Program Files (x86)\SimHub')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is missing.' }
$output = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @(
    (Join-Path $framework 'WPF\PresentationCore.dll'),
    (Join-Path $framework 'WPF\PresentationFramework.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Drawing.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll')
)
foreach ($name in @('SimHub.Plugins.dll','GameReaderCommon.dll','SimHub.Logging.dll','Newtonsoft.Json.dll','GongSolutions.WPF.DragDrop.dll','log4net.dll')) {
    $path = Join-Path $SimHubPath $name
    if (!(Test-Path -LiteralPath $path)) { throw "Missing SimHub SDK assembly: $path" }
    $references += $path
}
$arguments = @('/nologo','/target:library','/optimize+','/warn:4',('/out:' + (Join-Path $output 'DilaRacing.SimHub.dll')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
$arguments += Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
$testArgs = @('/nologo','/target:exe','/reference:System.Drawing.dll',('/out:' + (Join-Path $output 'DilaRacing.Tests.exe')),
    (Join-Path $root 'src\Core.cs'), (Join-Path $root 'tests\CoreTests.cs'))
$testArgs += Join-Path $root 'src\RevStripCore.cs'
$testArgs += Join-Path $root 'src\ThemePresets.cs'
& $compiler @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $output 'DilaRacing.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Lighting tests failed.' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output -Force
New-Item -ItemType Directory -Path (Join-Path $output 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs\SETUP.md') -Destination (Join-Path $output 'docs\SETUP.md') -Force
Copy-Item -Path (Join-Path $root 'profiles\*.ledsprofile') -Destination $output -Force
Write-Output "Built and tested: $output\DilaRacing.SimHub.dll"
