param([switch]$CompareBaseline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-refresh-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
try {
    $variants = @('current')
    if ($CompareBaseline) { $variants = @('baseline', 'current') }
    foreach ($variant in $variants) {
        $source = Join-Path $projectRoot 'DesktopFeatures.cs'
        if ($variant -eq 'baseline') {
            $original = & git -C $projectRoot show HEAD:DesktopFeatures.cs
            if ($LASTEXITCODE -ne 0) { throw 'Could not read committed desktop-refresh baseline' }
            $source = Join-Path $testRoot 'DesktopFeatures.cs'
            [IO.File]::WriteAllText($source, ($original -join [Environment]::NewLine), [Text.Encoding]::UTF8)
        }
        $output = Join-Path $testRoot ('DesktopRefresh-' + $variant + '.exe')
        & $compiler /nologo /target:exe /platform:x64 /r:System.dll /r:System.Drawing.dll /codepage:65001 ('/out:' + $output) $source (Join-Path $PSScriptRoot 'TaskbarRegression.cs')
        if ($LASTEXITCODE -ne 0) { throw ('Desktop-refresh measurement compilation failed: ' + $variant) }
        & $output --measure | ForEach-Object { Write-Host ($variant + ': ' + $_) }
        if ($LASTEXITCODE -ne 0) { throw ('Desktop-refresh measurement failed: ' + $variant) }
        if ($variant -eq 'current') {
            & $output
            if ($LASTEXITCODE -ne 0) { throw 'Desktop-refresh regression failed' }
        }
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-refresh-[0-9a-f]{32}$') { throw 'Unexpected desktop-refresh cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
