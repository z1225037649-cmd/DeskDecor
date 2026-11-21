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
