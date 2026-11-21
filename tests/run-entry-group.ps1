$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('DeskDecor-entry-group-' + [Guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($testRoot)
try {
    $sources = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File | Sort-Object Name)
    $copiedSources = @()
    foreach ($source in $sources) {
        $text = [System.IO.File]::ReadAllText($source.FullName)
        if ($source.Name -eq 'SettingsForm.cs') {
            # Keep the real controls/callbacks, replacing only presentation boundaries.
            $replacements = @(
                @('menu.Show(_list, e.Location);', 'EntryGroupRegression.CaptureMenu(menu);'),
                @('dialog.ShowDialog(this)', 'EntryGroupRegression.AnswerRename(dialog)')
            )
            foreach ($pair in $replacements) {
                if (($text.Split([string[]]@($pair[0]), [StringSplitOptions]::None).Length - 1) -ne 1) {
                    throw ('Expected exactly one presentation boundary: ' + $pair[0])
                }
                $text = $text.Replace($pair[0], $pair[1])
            }
        }
        $copy = Join-Path $testRoot $source.Name
        [System.IO.File]::WriteAllText($copy, $text, [System.Text.Encoding]::UTF8)
        $copiedSources += $copy
    }
    foreach ($name in @('SettingsForm.cs', 'OverlayForm.cs', 'AppContext.cs', 'DesktopItem.cs', 'DesktopEntries.cs')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $projectRoot $name) -Algorithm SHA256).Hash
        Write-Output ('SOURCE SHA256 ' + $name + ' ' + $hash)
    }
    $testOutput = Join-Path $testRoot 'EntryGroupRegression.exe'
    & $compiler /nologo /target:exe /main:EntryGroupRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ("/out:" + $testOutput) $copiedSources (Join-Path $PSScriptRoot 'EntryGroupRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Entry/group regression compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Entry/group regressions failed' }
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedTestRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([System.IO.Path]::GetFileName($resolvedTestRoot)).StartsWith('DeskDecor-entry-group-')) {
        throw 'Refusing cleanup outside the task-specific temporary directory'
    }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    Write-Output ('CLEANUP: removed task temporary directory ' + $resolvedTestRoot)
}
