param([string]$OutputPath = (Join-Path $PSScriptRoot 'DailyTodo.exe'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'
$output = $OutputPath

$compilerArgs = @(
    '/nologo', '/target:winexe', '/optimize+', '/codepage:65001'
    "/out:$output"
    "/reference:$wpf\PresentationFramework.dll"
    "/reference:$wpf\PresentationCore.dll"
    "/reference:$wpf\WindowsBase.dll"
    "/reference:$wpf\UIAutomationTypes.dll"
    "/reference:$framework\System.Xaml.dll"
    "/reference:$framework\System.Runtime.Serialization.dll"
    "/reference:$framework\System.Windows.Forms.dll"
    "/reference:$framework\System.Drawing.dll"
    "/win32icon:$PSScriptRoot\DailyTodo.ico"
    "/resource:$PSScriptRoot\DailyTodo.ico,DailyTodo.ico"
    "/resource:$PSScriptRoot\MainWindow.xaml,MainWindow.xaml"
    (Join-Path $PSScriptRoot 'DailyTodo.cs')
    (Join-Path $PSScriptRoot 'TodoCalendar.cs')
    (Join-Path $PSScriptRoot 'ReminderPopup.cs')
    (Join-Path $PSScriptRoot 'RepeatPicker.cs')
    (Join-Path $PSScriptRoot 'PixelCat.cs')
    (Join-Path $PSScriptRoot 'CatCollection.cs')
    (Join-Path $PSScriptRoot 'CatGallery.cs')
)
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Get-Item -LiteralPath $output
