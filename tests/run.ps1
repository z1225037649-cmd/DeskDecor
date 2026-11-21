$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testOutput = Join-Path $PSScriptRoot 'TaskbarRegression.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /r:System.dll /r:System.Drawing.dll /codepage:65001 ("/out:" + $testOutput) `
    (Join-Path $projectRoot 'DesktopFeatures.cs') (Join-Path $PSScriptRoot 'TaskbarRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression test compilation failed' }
& $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
$reorderOutput = Join-Path $PSScriptRoot 'ReorderRegression.exe'
& $compiler /nologo /target:exe /r:System.dll /r:System.Drawing.dll /codepage:65001 ("/out:" + $reorderOutput) `
    (Join-Path $projectRoot 'DesktopItem.cs') (Join-Path $projectRoot 'ItemReorderGesture.cs') (Join-Path $PSScriptRoot 'ReorderRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Reorder regression compilation failed' }
& $reorderOutput
if ($LASTEXITCODE -ne 0) { throw 'Reorder regression tests failed' }
$featuresOutput = Join-Path $PSScriptRoot 'EntryFeaturesRegression.exe'
& $compiler /nologo /target:exe /r:System.dll /codepage:65001 ("/out:" + $featuresOutput) `
    (Join-Path $projectRoot 'DesktopItem.cs') (Join-Path $projectRoot 'DesktopEntries.cs') `
    (Join-Path $projectRoot 'StartupRegistration.cs') (Join-Path $PSScriptRoot 'EntryFeaturesRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Entry features regression compilation failed' }
& $featuresOutput
if ($LASTEXITCODE -ne 0) { throw 'Entry features regression tests failed' }
$inlineOutput = Join-Path $PSScriptRoot 'InlineTitleRegression.exe'
& $compiler /nologo /target:exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /codepage:65001 ("/out:" + $inlineOutput) `
    (Join-Path $projectRoot 'InlineTitleEditor.cs') (Join-Path $projectRoot 'Native.cs') (Join-Path $PSScriptRoot 'InlineTitleRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Inline title regression compilation failed' }
& $inlineOutput
if ($LASTEXITCODE -ne 0) { throw 'Inline title regression tests failed' }
$settingsOutput = Join-Path $PSScriptRoot 'SettingsStoreRegression.exe'
& $compiler /nologo /target:exe /r:System.dll /r:System.Xml.dll /codepage:65001 ("/out:" + $settingsOutput) `
    (Join-Path $projectRoot 'DesktopItem.cs') (Join-Path $PSScriptRoot 'SettingsStoreRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Settings store regression test compilation failed' }
& $settingsOutput
if ($LASTEXITCODE -ne 0) { throw 'Settings store regression tests failed' }
$layoutOutput = Join-Path $PSScriptRoot 'DesktopLayoutRecoveryRegression.exe'
& $compiler /nologo /target:exe /r:System.dll /r:System.Drawing.dll /codepage:65001 ("/out:" + $layoutOutput) `
    (Join-Path $projectRoot 'DesktopItem.cs') (Join-Path $projectRoot 'DesktopLayoutRecovery.cs') (Join-Path $projectRoot 'MonitorDpi.cs') `
    (Join-Path $PSScriptRoot 'DesktopLayoutRecoveryRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Desktop layout recovery regression compilation failed' }
& $layoutOutput
if ($LASTEXITCODE -ne 0) { throw 'Desktop layout recovery regression tests failed' }
$overlayOutput = Join-Path $PSScriptRoot 'OverlayLayoutRegression.exe'
$overlaySources = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /main:OverlayLayoutRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ("/out:" + $overlayOutput) `
    $overlaySources (Join-Path $PSScriptRoot 'OverlayLayoutRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Overlay layout regression compilation failed' }
& $overlayOutput
if ($LASTEXITCODE -ne 0) { throw 'Overlay layout regression tests failed' }
$settingsFormOutput = Join-Path $PSScriptRoot 'SettingsFormRegression.exe'
& $compiler /nologo /target:exe /main:SettingsFormRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ("/out:" + $settingsFormOutput) `
    $overlaySources (Join-Path $PSScriptRoot 'SettingsFormRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Settings form regression compilation failed' }
& $settingsFormOutput
if ($LASTEXITCODE -ne 0) { throw 'Settings form regression tests failed' }
& (Join-Path $PSScriptRoot 'run-group-search.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Group-search regression tests failed' }
& (Join-Path $PSScriptRoot 'run-menu-lifetime.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Menu lifetime regression tests failed' }
