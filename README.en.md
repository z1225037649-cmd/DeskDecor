# DeskDecor

Windows desktop shortcut and folder overlay. It runs as a portable executable and stores its item list and positions beside the executable in `DeskDecor.settings.xml`.

## Build

Run `build.ps1` in a normal user PowerShell session. The build uses the Windows .NET Framework C# compiler, targets x64, and embeds the official ExplorerTAP 2026.2 DLL from `vendor/TranslucentTB-2026.2-x64`. The script checks the DLL hash before compilation and sets the output EXE to Medium integrity.

`package.ps1` also refreshes the ready-to-run `dist/DeskDecor-portable-x64/DeskDecor.exe`, preserving existing settings and personal files in that folder. The shared ZIP excludes those personal files. Exit this portable copy from its tray menu before packaging. Files extracted inside a restricted development folder can inherit Low integrity and fail to register a tray icon. For local verification, use the generated portable folder or extract the ZIP into an ordinary user folder, such as a new folder on the Desktop.

Packaging sets the output folder and both final ZIP files to Medium integrity and removes the ZIP download markers. A Low-integrity ZIP can cause the Windows compressed-folder interface to add `ZoneId=3` to extracted executables even when the ZIP has no `Zone.Identifier` stream. Clearing only the root EXE's marker does not fix that case.

## Upgrading from a shared package

Exit the running version through its tray menu, unpack the new `DeskDecor-portable-x64.zip` into a new permanent folder and run its `DeskDecor.exe`. No network connection is required. A fresh folder imports only the immediate files, shortcuts and folders on the current user's desktop and the public desktop. Folders become entries; their contents are not scanned.

Starting with 1.2.0, automatic discovery of older installations, upgrade handoff, content migration and old-directory cleanup have been removed. The app does not use running processes, the last installation record, startup paths or previous migration records to locate configurations elsewhere. Leftover `.migration` records are not executed.

Later launches load `DeskDecor.settings.xml` beside the current executable, falling back only to its adjacent `.bak` when needed. To keep groups, exit the app and manually copy those two configuration files. Entries keep their original target paths; files in the old directory are not moved or deleted. Enable startup again at the new location if needed.

Increase `AssemblyInfo.cs` before testing, building and packaging each new release. Share packages exclude all personal settings and migrated files.

## Use

Version 1.2.1 combines redundant redraws within each interaction, loads offscreen entry icons when they enter view, and releases icon caches for removed entries. Background refresh reuses a valid desktop host while retaining recovery after Explorer or wallpaper-host changes.

If Windows displays an Open File security warning, verify that the ZIP came from this project, open its Properties, select Unblock and Apply, then extract it into a new folder. An already extracted copy needs its `DeskDecor.exe` unblocked separately. The program is currently unsigned; download or transfer software can add an Internet marker again, so successful local packaging checks cannot guarantee that every recipient will see no warnings.

- Double-click `DeskDecor.exe` to show the desktop items.
- Right-click the tray icon to add a shortcut/file or folder, manage items, toggle the overlay, or exit.
- Double-click an item to open its target. Items are arranged inside independent transparent group frames.
- Drag any empty area inside the frame to move the whole group. Double-click an item to open it.
- Drag the lower-right grip to resize the group; changing its width reflows items inside the frame.
- Drag an item past the Windows drag threshold to start sorting. A translucent copy follows the pointer and a line marks the insertion point. Other items stay still until you release the left button; then the item is inserted once and intervening items shift. Drop in trailing empty space to append. Drop outside the frame, press Escape, or right-click to cancel. Losing mouse capture also cancels without saving.
- Items have no card or panel background. Icon size is adjustable from 16 to 32 logical pixels and scales with monitor DPI, with a subtle black contour following the alpha silhouette and a soft shadow. White labels and header text use a fine black outline and shadow for readability on both light and dark wallpaper. Outlined icon bitmaps are cached and drag previews fade together with their outline.
- The frame has subtle rounded edges, with a brighter border and a small corner indicator only while hovered. Labels use Microsoft YaHei UI with single-line ellipsis; hovering an item shows its full name and path. Windows shell icons are cached so real folder artwork is shown without repeated disk reads during dragging.
- Layout uses consistent padding and centered columns, retaining each group's saved item order and frame position. Desktop metadata (`desktop.ini`, `Thumbs.db`) is omitted from the overlay without deleting the source files or existing saved entries.
- Use "自动排列" in the management window to lay out all items in a compact grid within the group.
- Use the tray menu or the management window button to show or hide the overlay manually.
- Create and switch between independent groups; search, rename, move, or remove entries in settings, with icon-size and opacity controls.
- Layout scales to the group monitor's DPI and restores off-screen groups after a display change. Settings use verified atomic saves with recovery from the last valid backup.
- Click the frame's upper-right "收起" button to hide its contents, leaving a small draggable header. Click "展开" to restore the contents. Expanded size, position, order, and collapsed state are saved. The hidden area passes mouse input through to the desktop.
- Double-click the upper-left group title, right-click it and select "修改分组名称", or use "修改分组名称" in settings. Edit directly over the title without a dialog; Enter or clicking elsewhere saves, and Escape cancels. An empty name restores "快捷入口". Chinese IME candidate input is respected. Choosing rename from settings closes settings and starts editing at the desktop title. Custom names are saved beside the EXE and persist when collapsed or restarted.
- Drag one or more files, shortcuts, or folders from Explorer into the frame to add entries. The border highlights and the header says "松开添加" while a drop is accepted. Dropping on a collapsed header adds entries and expands it. Existing entries are skipped; original files are never moved or copied.
- Right-click an icon and choose "移除图标（保留原文件）" to remove only its saved entry. An empty group stays empty across restarts.
- Toggle "开机自启动" in the management window or tray menu. It defaults off and uses only the current user's `Software\Microsoft\Windows\CurrentVersion\Run\DeskDecor` value with the quoted current EXE path. Turning it off removes that value. Startup checks remove a registration that points to a different location; a two-second check while running also removes it if the original EXE path disappears. A tray notification and settings text explain that autostart was disabled because the location changed. It stays off until explicitly enabled again. When the app is closed, movement is detected the next time the user manually launches it; no background helper is installed.
- The transparent popup is owned by the Windows desktop and positioned immediately above it, so regular application windows cover it. It never uses TopMost and does not activate when clicked or dragged.
- Toggle taskbar transparency and native desktop icon visibility from the tray menu or settings window. Taskbar changes are restored when the app exits, including abnormal process exit via ExplorerTAP's process watcher. Taskbar effects are applied per toggle or taskbar recreation event; background refresh does not reapply them or broadcast theme changes.
- Keep the EXE in a writable folder so it can save `DeskDecor.settings.xml` alongside itself.

On first run, existing entries from the user and public Desktop folders are shown. Items are references to their original paths; files are not copied or moved. The initialized empty-list marker prevents removed entries from being imported again.

Live verification covers a single-monitor 125%→150%→125% scale round trip, input through the original group-title editor, group gestures, system toggles, wallpaper switching and Explorer restart recovery. Physical mixed-DPI monitors and display hot-plugging still need hardware verification. Startup registration and cleanup were tested without logging out or restarting Windows. See the [verification status](tests/verification-status-2026-11-21.md) for evidence and coverage limits.

The EXE uses the Windows .NET Framework installed with Windows. It is a portable x64 app with an embedded native XAML engine; first use extracts the hash-checked engine and its notices into `DeskDecor.runtime/ExplorerTAP-2026.2-x64/` beside the EXE. Keep this folder while it runs. No installation or administrator rights are required. The XAML engine stays loaded in Explorer until Explorer exits; it restores the original taskbar appearance when DeskDecor stops.

## Taskbar compatibility

Windows 11 XAML taskbars use the embedded TranslucentTB ExplorerTAP API v4. `BackgroundFill` is set to a SolidColor brush with zero alpha and the background border is hidden. The original fills are restored when disabled. Connection runs on a background MTA thread, so the settings window stays responsive. The legacy AccentPolicy path is used only for older taskbars. Neither path changes Windows transparency or accent-color registry settings.

The overlay remains a top-level layered popup. The visible window containing `SHELLDLL_DefView` is its owner (`GWLP_HWNDPARENT`), not its child-window parent. In the tested Wallpaper Engine layout this owner is `Progman`, containing both the desktop icon view and a child WorkerW with `WPELiveWallpaper`. This avoids composing a layered cross-process child inside Explorer. Hidden top-level WorkerW notification windows are excluded. The popup is placed immediately above its desktop owner and below ordinary apps; a stable refresh does not rewrite the z-order. Native caption/frame styles are cleared, and layered bitmap positions use virtual-screen coordinates.

The inline title input is independent of the desktop surface, so keyboard activation cannot raise the desktop's owned-window chain over applications. Layer repair also lowers an accidentally raised desktop owner and explicitly clears a stale TopMost state. `SWP_NOOWNERZORDER` prevents surface placement from promoting the owner.

Reference: [TranslucentTB TaskbarAttributeWorker](https://github.com/TranslucentTB/TranslucentTB/blob/release/TranslucentTB/taskbar/taskbarattributeworker.cpp) uses a separate TaskbarService/XAML Diagnostics implementation for XAML taskbars; an AccentPolicy call returning success is not proof of visible transparency.

Run `powershell -ExecutionPolicy Bypass -File tests\run.ps1` for the taskbar and desktop-refresh regression checks. They compile production DesktopFeatures against fake Windows/XAML boundaries, verifying routing, restore, refresh idempotency, and rejection of hidden WorkerW hosts. They do not change Explorer or system preferences. Actual engine connections and failures are recorded in `DeskDecor.debug.log`. Normal startup no longer writes a full-screen PNG or enumerates windows for diagnostic dumps.

The same runner checks delayed reorder commits, batch-import normalization and duplicate handling, preservation of original files when removing entries, collapsed geometry/XML persistence, and startup enable/disable against a fake registry boundary. Startup tests do not enable real autostart.

`tests/SurfaceInspector.cs` is an optional developer-only DWM thumbnail viewer. It previews the actual compositor surface without moving or activating the overlay. A visible preview confirms rendered content, but is not a full-desktop screenshot or proof of interaction through Wallpaper Engine.

See THIRD-PARTY-NOTICES.md and LICENSE.md for the included engine and GPL v3 source/distribution terms.
