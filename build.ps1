$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$iconScript = Join-Path $root 'make_icon.ps1'
$icon = Join-Path $root 'DeskDecor.ico'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Windows .NET Framework C# compiler not found' }
$sources = @(Get-ChildItem -LiteralPath $root -Filter '*.cs' | ForEach-Object { $_.FullName })
& $iconScript -OutputPath $icon
$out = Join-Path $root 'DeskDecor.exe'
$engine = Join-Path $root 'vendor\TranslucentTB-2026.2-x64\ExplorerTAP.dll'
if (-not (Test-Path -LiteralPath $engine)) { throw 'Missing official ExplorerTAP 2026.2 x64 DLL' }
if ((Get-FileHash -LiteralPath $engine -Algorithm SHA256).Hash -ne 'C48F1A06168ACD7839269DDE0D15CCF5D09C702512B94DD6BA9A05DF1B153DC2') { throw 'ExplorerTAP checksum mismatch' }
$args = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
  ('/resource:' + $engine + ',DeskDecor.ExplorerTAP.dll'),
  ('/resource:' + (Join-Path $root 'LICENSE.md') + ',DeskDecor.LICENSE.md'),
  ('/resource:' + (Join-Path $root 'THIRD-PARTY-NOTICES.md') + ',DeskDecor.THIRD-PARTY-NOTICES.md'),
  ('/win32manifest:' + (Join-Path $root 'app.manifest')),
  ('/win32icon:' + $icon),
  '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll',
  ('/out:' + $out)) + $sources
& $csc $args
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
# A rebuild can retain the previous EXE's download marker.
Unblock-File -LiteralPath $out
# Executables built in a Low-integrity workspace inherit that label, even when
# later double-clicked in Explorer. Tray registration requires normal desktop access.
$integrityOutput = & (Join-Path $env:WINDIR 'System32\icacls.exe') $out /setintegritylevel M 2>&1
if ($LASTEXITCODE -ne 0) {
    throw ('Could not prepare the executable for desktop use. Run build.ps1 from a normal user PowerShell session. ' + ($integrityOutput -join [Environment]::NewLine))
}
$builtVersion = [Reflection.AssemblyName]::GetAssemblyName($out).Version
if ($builtVersion -le [Version]'0.0.0.0') { throw 'The executable must have an explicit release version' }
Write-Host ('Built: ' + $out + ' (' + $builtVersion.ToString(4) + ')')
