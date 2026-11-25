param([switch]$CompareBaseline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-rendering-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $currentSources = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    $variants = @('current')
    if ($CompareBaseline) { $variants = @('baseline', 'current') }
    $results = @{}
    foreach ($variant in $variants) {
        $sources = $currentSources
        if ($variant -eq 'baseline') {
            $original = & git -C $projectRoot show HEAD:OverlayForm.cs
            if ($LASTEXITCODE -ne 0) { throw 'Could not read the committed rendering baseline' }
            $baseline = Join-Path $testRoot 'OverlayForm.cs'
            [IO.File]::WriteAllText($baseline, ($original -join [Environment]::NewLine), [Text.Encoding]::UTF8)
            $sources = @($currentSources | Where-Object { [IO.Path]::GetFileName($_) -ne 'OverlayForm.cs' }) + $baseline
        }
        $output = Join-Path $testRoot ('RenderingPerformance-' + $variant + '.exe')
        & $compiler /nologo /target:exe /platform:x64 /optimize+ /main:RenderingPerformanceRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ('/out:' + $output) $sources (Join-Path $PSScriptRoot 'RenderingPerformanceRegression.cs')
        if ($LASTEXITCODE -ne 0) { throw ('Rendering test compilation failed: ' + $variant) }
        $results[$variant] = @(& $output)
        if ($LASTEXITCODE -ne 0) { throw ('Rendering test failed: ' + $variant) }
        $results[$variant] | Where-Object { $_ -match '^(PASS|BENCHMARK)' } | ForEach-Object { Write-Host ($variant + ': ' + $_) }
    }
    if ($CompareBaseline) {
        $before = @($results['baseline'] | Where-Object { $_ -like 'PIXELS *' })
        $after = @($results['current'] | Where-Object { $_ -like 'PIXELS *' })
        $differences = @(Compare-Object $before $after)
        if ($differences.Count -ne 0) {
            $differences | Format-Table | Out-Host
            throw 'Rendering pixels differ from the committed baseline'
        }
        Write-Host ('PASS: all ' + $before.Count + ' rendered frames exactly match the committed baseline')
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-rendering-[0-9a-f]{32}$') { throw 'Unexpected rendering test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
