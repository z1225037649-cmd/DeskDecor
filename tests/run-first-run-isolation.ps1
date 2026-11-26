$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-first-run-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
try {
    $sources = @()
    $instance = [Guid]::NewGuid().ToString('N')
    foreach ($source in Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File) {
        $text = [IO.File]::ReadAllText($source.FullName)
        # Avoid interacting with any real DeskDecor instance on this desktop.
        $text = $text.Replace('DeskDecor.SingleInstance.20261114', 'DeskDecor.FirstRun.Mutex.' + $instance)
        $text = $text.Replace('DeskDecor.ShowSettings.20261114', 'DeskDecor.FirstRun.Show.' + $instance)
        $text = $text.Replace('DeskDecor.InstanceMessageWindow', 'DeskDecor.FirstRun.Window.' + $instance)
        $pairs = @()
        if ($source.Name -eq 'Program.cs') {
            $pairs += ,@('Application.Run(context);', 'FirstRunIsolationRegression.Verify(context, dataPath);')
            # Surface startup failures to the test runner instead of a modal dialog.
            # ASCII-only boundaries also work with Windows PowerShell's ANSI decoding.
            $pairs += ,@('try { File.WriteAllText(logPath, ex.ToString()); } catch { }', 'try { File.WriteAllText(logPath, ex.ToString()); } catch { } throw new InvalidOperationException(ex.ToString()); /*')
            $pairs += ,@('Native.MB_OK | Native.MB_ICONERROR | Native.MB_TOPMOST | Native.MB_SETFOREGROUND);', 'Native.MB_OK | Native.MB_ICONERROR | Native.MB_TOPMOST | Native.MB_SETFOREGROUND); */')
        }
        if ($source.Name -eq 'AppContext.cs') {
            $pairs += ,@('_startup = new StartupRegistration(Application.ExecutablePath);', '_startup = new StartupRegistration(Application.ExecutablePath, new FirstRunIsolationRegression.StartupStore());')
            $pairs += ,@('_features.SetTaskbarTransparent(settings.TaskbarTransparent);', 'if (settings.TaskbarTransparent || settings.DesktopIconsHidden) throw new InvalidOperationException("System features must stay disabled in this fixture");')
            $pairs += ,@('_features.SetDesktopIconsHidden(settings.DesktopIconsHidden);', '// Native desktop-icon presentation suppressed in this fixture.')
            $pairs += ,@('_overlay.Show();', '// Native overlay presentation suppressed in this fixture.')
            $pairs += ,@('_tray.Visible = true;', '_tray.Visible = false;')
        }
        if ($source.Name -eq 'OverlayForm.cs') {
            $pairs += ,@('Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)', 'FirstRunIsolationRegression.DesktopDirectory(false)')
            $pairs += ,@('Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)', 'FirstRunIsolationRegression.DesktopDirectory(true)')
        }
        foreach ($pair in $pairs) {
            if (($text.Split([string[]]@($pair[0]), [StringSplitOptions]::None).Count - 1) -ne 1) { throw ('First-run fixture boundary changed: ' + $pair[0]) }
            $text = $text.Replace($pair[0], $pair[1])
        }
        $copy = Join-Path $testRoot $source.Name
        [IO.File]::WriteAllText($copy, $text, [Text.Encoding]::UTF8)
        $sources += $copy
    }
    $oldRoot = Join-Path $testRoot 'older-copy'
    [void][IO.Directory]::CreateDirectory($oldRoot)
    $oldSource = Join-Path $oldRoot 'Fixture.cs'
    [IO.File]::WriteAllText($oldSource, 'using System.Reflection; [assembly: AssemblyVersion("1.1.1.0")] internal static class Fixture { private static void Main() {} }')
    & $compiler /nologo /target:winexe ('/out:' + (Join-Path $oldRoot 'DeskDecor.exe')) $oldSource
    if ($LASTEXITCODE -ne 0) { throw 'Old-version fixture compilation failed' }
    $output = Join-Path $testRoot 'DeskDecor.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:FirstRunIsolationRegression /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /codepage:65001 ('/out:' + $output) $sources (Join-Path $PSScriptRoot 'FirstRunIsolationRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'First-run regression compilation failed' }
    foreach ($mode in @('fresh', 'saved', 'empty', 'backup')) {
        & $output $mode
        if ($LASTEXITCODE -ne 0) { throw ('First-run regression failed: ' + $mode) }
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-first-run-[0-9a-f]{32}$') { throw 'Unexpected first-run fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
