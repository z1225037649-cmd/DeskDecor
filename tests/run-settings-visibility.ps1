$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-settings-visibility-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
try {
    $sources = @()
    foreach ($source in Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File) {
        $text = [IO.File]::ReadAllText($source.FullName)
        if ($source.Name -eq 'AppContext.cs') {
            # Retain the actual ShowSettings, Show/Hide, WinForms events and saves.
            # Replace only external startup/system/tray integration.
            $pairs = @(
                @('_startup = new StartupRegistration(Application.ExecutablePath);', '_startup = new StartupRegistration(Application.ExecutablePath, new SettingsVisibilityRegression.StartupStore());'),
                @('_features.SetTaskbarTransparent(settings.TaskbarTransparent);', 'if (settings.TaskbarTransparent || settings.DesktopIconsHidden) throw new InvalidOperationException("System features must stay disabled in the visibility fixture");'),
                @('_features.SetDesktopIconsHidden(settings.DesktopIconsHidden);', '// Real desktop-icon changes are disabled in this fixture.'),
                @('_tray.Visible = true;', '_tray.Visible = false;')
            )
            foreach ($pair in $pairs) {
                if (($text.Split([string[]]@($pair[0]), [StringSplitOptions]::None).Count - 1) -ne 1) {
                    throw ('Application fixture boundary changed: ' + $pair[0])
                }
                $text = $text.Replace($pair[0], $pair[1])
            }
        }
        if ($source.Name -eq 'OverlayForm.cs') {
            # The private desktop has no Explorer host. Keep actual window and
            # bitmap rendering, but never attach it to the user's Explorer.
            $pattern = 'DesktopFeatures\.AttachOverlayToDesktop\(Handle, (Bounds|SystemInformation\.VirtualScreen)\);'
            if ([regex]::Matches($text, $pattern).Count -ne 3) { throw 'Desktop attachment fixture boundary changed' }
            $text = [regex]::Replace($text, $pattern, '{ /* Isolated desktop: no Explorer attachment. */ }')
        }
        $copy = Join-Path $testRoot $source.Name
        [IO.File]::WriteAllText($copy, $text, [Text.Encoding]::UTF8)
        $sources += $copy
    }
    $output = Join-Path $testRoot 'SettingsVisibilityRegression.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:SettingsVisibilityRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /codepage:65001 ("/out:" + $output) $sources (Join-Path $PSScriptRoot 'SettingsVisibilityRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Settings visibility regression compilation failed' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Settings visibility regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-settings-visibility-[0-9a-f]{32}$') {
        throw 'Unexpected visibility-test cleanup path'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
