$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-menu-lifetime-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $sources = @()
    foreach ($source in Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File) {
        $text = [IO.File]::ReadAllText($source.FullName)
        if ($source.Name -eq 'SettingsForm.cs') {
            $boundary = 'menu.Show(_list, e.Location);'
            if (($text.Split([string[]]@($boundary), [StringSplitOptions]::None).Count - 1) -ne 1) {
                throw 'Expected exactly one context-menu presentation boundary'
            }
            $text = $text.Replace($boundary, 'MenuLifetimeRegression.CaptureMenu(menu);')
        }
        $copy = Join-Path $testRoot $source.Name
        [IO.File]::WriteAllText($copy, $text, [Text.Encoding]::UTF8)
        $sources += $copy
    }
    $output = Join-Path $testRoot 'MenuLifetimeRegression.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:MenuLifetimeRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ("/out:" + $output) $sources (Join-Path $PSScriptRoot 'MenuLifetimeRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Menu lifetime regression compilation failed' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Menu lifetime regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-menu-lifetime-[0-9a-f]{32}$') { throw 'Unexpected test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
