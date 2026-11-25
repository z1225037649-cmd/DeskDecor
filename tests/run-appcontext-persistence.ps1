$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-appcontext-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $sources = @()
    foreach ($source in Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File) {
        $text = [IO.File]::ReadAllText($source.FullName).Replace("`r`n", "`n")
        if ($source.Name -eq 'AppContext.cs') {
            # Substitute external startup/system state and native presentation.
            # The production event subscriptions and SaveSettings are unchanged.
            $pairs = @(
                @('_startup = new StartupRegistration(Application.ExecutablePath);', '_startup = new StartupRegistration(Application.ExecutablePath, new AppContextPersistenceRegression.StartupStore());'),
                @('_features.SetTaskbarTransparent(settings.TaskbarTransparent);', 'if (settings.TaskbarTransparent || settings.DesktopIconsHidden) throw new InvalidOperationException("System features must stay disabled in the persistence fixture");'),
                @('_features.SetDesktopIconsHidden(settings.DesktopIconsHidden);', '// Native desktop-icon presentation suppressed in this fixture.'),
                @('_overlay.Show();', '// Native overlay presentation suppressed in this fixture.'),
                @('_tray.Visible = true;', '_tray.Visible = false;')
            )
            foreach ($pair in $pairs) {
                if (($text.Split([string[]]@($pair[0]), [StringSplitOptions]::None).Count - 1) -ne 1) { throw 'Application fixture boundary changed' }
                $text = $text.Replace($pair[0], $pair[1])
            }
            $start = $text.IndexOf('            _settingsForm.Show();')
            $endText = '            if (addFolder) _settingsForm.BeginAddFolder();'
            $end = $text.IndexOf($endText, $start)
            if ($start -lt 0 -or $end -lt $start) { throw 'Settings presentation boundary changed' }
            $text = $text.Substring(0,$start) + '            // Native settings presentation suppressed in this fixture.' + "`n" + $text.Substring($end + $endText.Length)
        }
        $copy = Join-Path $testRoot $source.Name
        [IO.File]::WriteAllText($copy,$text,[Text.Encoding]::UTF8)
        $sources += $copy
    }
    $output = Join-Path $testRoot 'AppContextPersistenceRegression.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:AppContextPersistenceRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ("/out:"+$output) $sources (Join-Path $PSScriptRoot 'AppContextPersistenceRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Application persistence regression compilation failed' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Application persistence regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-appcontext-[0-9a-f]{32}$') { throw 'Unexpected application-test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
