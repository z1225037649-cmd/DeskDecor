# TranslucentTB ExplorerTAP

DeskDecor embeds the unmodified, digitally signed x64 ExplorerTAP.dll from the
official TranslucentTB 2026.2 portable release. It uses the XAML taskbar appearance
service (API v4) to set BackgroundFill to a transparent SolidColorBrush and to
restore the original background/border when disabled or when DeskDecor exits.
No TranslucentTB.exe process is launched and no system theme registry settings
are changed.

- Project: https://github.com/TranslucentTB/TranslucentTB
- Binary: https://github.com/TranslucentTB/TranslucentTB/releases/tag/2026.2
- Source commit: d4636e439865df0a1a1419db408e055740ce5c74
- Source archive: vendor/TranslucentTB-2026.2-source.zip
- License: GNU General Public License v3 (see LICENSE.md)
- Copyright: TranslucentTB contributors; original notices are preserved in the source archive.
- Official package SHA-256: 0DBE8E0255C20E131CDE536DCD0AE490D45989A7D360E26D0150DFF1922AC420
- ExplorerTAP.dll SHA-256: C48F1A06168ACD7839269DDE0D15CCF5D09C702512B94DD6BA9A05DF1B153DC2

This integrated DeskDecor build and its source are provided under GPL v3.
The source bundle includes the DeskDecor build scripts and the matching upstream
source archive; no C++ compiler is needed to rebuild DeskDecor from the bundled
official DLL. ExplorerTAP's original C++ project is in that upstream archive.

ExplorerTAP uses Windows XAML Diagnostics inside Explorer. The DLL remains loaded
there until Explorer exits. The background changes themselves are restored when
the toggle is disabled or DeskDecor exits. The extracted versioned runtime folder
must remain available while the program runs.
