param([switch]$CompareBaseline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-interaction-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
try {
    $variants = @('current')
    if ($CompareBaseline) { $variants = @('baseline', 'current') }
    $results = @{}
    foreach ($variant in $variants) {
        $sources = @()
        foreach ($source in Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File) {
            $text = [IO.File]::ReadAllText($source.FullName)
            if ($source.Name -eq 'OverlayForm.cs') {
                if ($variant -eq 'baseline') {
                    $original = & git -C $projectRoot show HEAD:OverlayForm.cs
                    if ($LASTEXITCODE -ne 0) { throw 'Could not read committed interaction baseline' }
                    $text = $original -join [Environment]::NewLine
                }
                $boundary = 'if (!IsHandleCreated || IsDisposed) return;'
                if (($text.Split([string[]]@($boundary), [StringSplitOptions]::None).Count - 1) -ne 1) { throw 'Rendering boundary changed' }
                $text = $text.Replace($boundary, 'if (RenderingInteractionRegression.Render(this)) return;')
                # Retain real mouse/hover/gesture logic but avoid native coordinate
                # conversion and capture, which would create an Explorer surface.
                $text = [regex]::Replace($text, 'Point (p|screen) = PointToScreen\(e.Location\);\r?\n\s+\1.Offset\(-Bounds.Left, -Bounds.Top\);', 'Point $1 = e.Location;')
                $text = $text.Replace('Capture = true;', '// Native pointer capture suppressed in this fixture.')
            }
            $copy = Join-Path $testRoot $source.Name
            [IO.File]::WriteAllText($copy, $text, [Text.Encoding]::UTF8)
            $sources += $copy
        }
        $output = Join-Path $testRoot ('RenderingInteraction-' + $variant + '.exe')
        & $compiler /nologo /target:exe /platform:x64 /optimize+ /main:RenderingInteractionRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ('/out:' + $output) $sources (Join-Path $PSScriptRoot 'RenderingInteractionRegression.cs')
        if ($LASTEXITCODE -ne 0) { throw ('Interaction fixture compilation failed: ' + $variant) }
        $results[$variant] = @(& $output $variant)
        if ($LASTEXITCODE -ne 0) { throw ('Interaction fixture failed: ' + $variant) }
        $results[$variant] | Where-Object { $_ -match '^(PASS|FRAMES|ICON_LOADS)' } | ForEach-Object { Write-Host ($variant + ': ' + $_) }
    }
    if ($CompareBaseline) {
        $before = @($results['baseline'] | Where-Object { $_ -like 'PIXELS *' })
        $after = @($results['current'] | Where-Object { $_ -like 'PIXELS *' })
        if (@(Compare-Object $before $after).Count) { throw 'Final interaction pixels differ from committed baseline' }
        Write-Host ('PASS: all ' + $before.Count + ' interaction results match the committed baseline pixels')
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-interaction-[0-9a-f]{32}$') { throw 'Unexpected interaction cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
