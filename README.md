# DeskDecor

轻量的 Windows 桌面快捷入口工具。把软件快捷方式、文件和文件夹集中放入透明分组框，支持整组移动、调整大小和拖动排序，以便携 EXE 方式运行。

## 下载与使用

1. 在 [Releases](https://github.com/z1225037649-cmd/DeskDecor/releases) 下载 `DeskDecor-portable-x64.zip`。
2. 完整解压到普通用户可写入的文件夹（例如桌面上新建的 `DeskDecor` 文件夹），再双击 `DeskDecor.exe`。无需安装或管理员权限。
3. 通过系统托盘菜单管理项目、显示或隐藏分组，以及启用可选功能。

首次启动读取当前电脑的桌面项目。发布包不包含开发者的个人快捷方式、配置或日志。

## 功能

- 透明分组框、16–32 逻辑像素可调图标（随 DPI 缩放），文字和图标带细黑色轮廓及阴影。
- 整组拖动、自由缩放；拖动项目后松开鼠标才提交新顺序。
- 支持多个独立分组，可在桌面悬停切换或从管理窗口切换、创建和删除；删除分组时入口会移到另一个分组。
- 从资源管理器拖入文件、文件夹或快捷方式添加入口；右键移除入口保留原文件。
- 收起和展开分组内容，双击标题原地改名。
- 按分组所在显示器的 DPI 缩放布局；更改缩放或断开显示器后，将分组和入口恢复到可见屏幕。
- 管理窗口支持搜索、重命名入口、移动入口到其他分组，并调整图标尺寸和显示透明度。
- 配置通过校验后的原子替换保存，保留上一份有效备份；损坏时尝试恢复，并在无法保存或恢复时提示。
- 桌面图层显示，普通应用窗口可以覆盖组件；已针对 Wallpaper Engine 的桌面布局适配。
- 可选隐藏原生桌面图标和任务栏透明。Windows 11 XAML 任务栏使用 TranslucentTB ExplorerTAP 引擎，兼容性取决于系统及 Explorer 版本。
- 可选当前用户开机自启动，默认关闭。检测到原程序路径失效时取消；程序关闭期间移动文件夹，需要下次手动启动时才能检测。

## 配置与分享

配置在 EXE 旁边的 `DeskDecor.settings.xml` 中，保存项目路径、分组、顺序、显示器布局和外观设置，并保留有效备份。分享时直接发送 Releases 中的压缩包，不要发送自己的配置文件。

入口引用当前电脑的真实路径，并不会复制软件。其他用户应添加自己电脑上的项目。

## 更换新版并保留配置

更新时，将收到的 `DeskDecor-portable-x64.zip` 完整解压到一个新的固定文件夹，然后运行其中的 `DeskDecor.exe`，无需联网。

新版自身没有配置时，会查找当前用户正在运行的旧版、上次成功启动记录和开机自启动位置。只有一份有效旧配置时自动迁移；发现多份时让用户选择旧版程序。新文件夹已有配置时继续使用该配置。不会扫描整块硬盘。

从早期版本首次升级时，请先保持旧版运行，再打开新版；若提示旧版不支持自动退出，从旧版托盘选择“退出”，重新打开新版即可。已支持交接的版本会自动保存并退出。若早期旧版已关闭且没有自启动记录，可先运行一次旧版帮助识别，或退出后把它的 `DeskDecor.settings.xml` 和 `.bak` 一并复制到新目录。

从 1.1.1 开始，旧目录里的个人文件、文件夹及仍被入口引用的内容会先复制到新版的 `迁移文件` 文件夹，校验内容后自动更新入口路径；指向旧程序的入口会改为新版程序。复制出的快捷方式同时更新内部目标和工作目录。新版正常启动后，删除旧目录里的程序、配置、备份、日志、运行库和旧迁移归档，并删除已清空的旧文件夹，不再另存旧版归档。若清理期间又出现文件变更，会在新版迁移目录中另外保存，避免覆盖已编辑的新文件。Windows 仍占用的文件会保留清理记录，在释放占用后后台重试；不会重启资源管理器。源码/Git 目录、系统或个人根目录、新旧目录互相嵌套及链接目录不会整目录清理。迁移失败或新版尚未正常启动时不删除旧文件。换目录后，需要在新位置重新开启开机自启动。

发布下一版前提高 `AssemblyInfo.cs` 的版本号，再运行测试、构建和打包。仅更换版本号更高的程序才会接管旧配置；分享包始终不包含个人配置或迁移备份。

本轮实机验证覆盖单屏 125%→150%→125% 动态缩放、原始样式分组改名、分组手势、系统开关、壁纸切换及 Explorer 重启恢复。真实多屏混合 DPI 和显示器热插拔仍待硬件验证；自启动已验证注册与清理，未注销或重启 Windows。详见[测试状态](tests/verification-status-2026-11-21.md)。

## 构建与许可

Windows x64 下在普通用户 PowerShell 环境依次运行 `tests/run.ps1`、`build.ps1` 和 `package.ps1`。使用 Windows 自带的 .NET Framework 编译器，构建会检查所附官方 ExplorerTAP.dll 的 SHA-256，并修正输出 EXE 的完整性标记。GitHub Actions 同样执行检查并提供便携包。

打包同时更新可直接运行的 `dist/DeskDecor-portable-x64/DeskDecor.exe`，保留该目录已有的个人配置和文件；分享用 ZIP 仍不包含这些个人内容。打包前请先从托盘正常退出这个便携副本。受限开发目录可能让新解压文件继承低完整性标记，导致托盘缺失；本地验证请使用打包生成的便携目录，或将 ZIP 解压到普通用户文件夹。

本集成版本以 [GPL v3](LICENSE.md) 发布，包含未经修改的官方 TranslucentTB 2026.2 x64 ExplorerTAP.dll 和匹配的上游源码归档。详见 [第三方声明](THIRD-PARTY-NOTICES.md)。自动化检查不等于覆盖所有系统及壁纸组合的实际桌面测试。

下面保留详细的英文使用和技术说明，也可查看 [English documentation](README.en.md)。

Windows desktop shortcut and folder overlay. It runs as a portable executable and stores its item list and positions beside the executable in `DeskDecor.settings.xml`.

## Build

Run `build.ps1` in PowerShell. The build uses the Windows .NET Framework C# compiler, targets x64, and embeds the official ExplorerTAP 2026.2 DLL from `vendor/TranslucentTB-2026.2-x64`. The script checks the DLL hash before compilation.

## Use

- Double-click `DeskDecor.exe` to show the desktop items.
- Right-click the tray icon to add a shortcut/file or folder, manage items, toggle the overlay, or exit.
- Double-click an item to open its target. Items are arranged together inside one transparent group frame.
- Drag any empty area inside the frame to move the whole group. Double-click an item to open it.
- Drag the lower-right grip to resize the group; changing its width reflows items inside the frame.
- Drag an item past the Windows drag threshold to start sorting. A translucent copy follows the pointer and a line marks the insertion point. Other items stay still until you release the left button; then the item is inserted once and intervening items shift. Drop in trailing empty space to append. Drop outside the frame, press Escape, or right-click to cancel. Losing mouse capture also cancels without saving.
- Items have no card or panel background. Icon size is adjustable from 16 to 32 logical pixels and scales with monitor DPI, with a subtle black contour following the alpha silhouette and a soft shadow. White labels and header text use a fine black outline and shadow for readability on both light and dark wallpaper. Outlined icon bitmaps are cached and drag previews fade together with their outline.
- The frame has subtle rounded edges, with a brighter border and a small corner indicator only while hovered. Labels use Microsoft YaHei UI with single-line ellipsis; hovering an item shows its full name and path. Windows shell icons are cached so real folder artwork is shown without repeated disk reads during dragging.
- Layout uses consistent padding and centered columns, retaining the saved item order and frame position. Desktop metadata (`desktop.ini`, `Thumbs.db`) is omitted from the overlay without deleting the source files or existing saved entries.
- Use "自动排列" in the management window to lay out all items in a compact grid within the group.
- Use the tray menu or the management window button to show or hide the overlay manually.
- Click the frame's upper-right "收起" button to hide its contents, leaving a small draggable header. Click "展开" to restore the contents. Expanded size, position, order, and collapsed state are saved. The hidden area passes mouse input through to the desktop.
- Double-click the upper-left group title, right-click it and select "修改分组名称", or use "修改分组名称" in settings. Edit directly over the title without a dialog; Enter or clicking elsewhere saves, and Escape cancels. An empty name restores "快捷入口". Chinese IME candidate input is respected. Choosing rename from settings closes settings and starts editing at the desktop title. Custom names are saved beside the EXE and persist when collapsed or restarted.
- Drag one or more files, shortcuts, or folders from Explorer into the frame to add entries. The border highlights and the header says "松开添加" while a drop is accepted. Dropping on a collapsed header adds entries and expands it. Existing entries are skipped; original files are never moved or copied.
- Right-click an icon and choose "移除图标（保留原文件）" to remove only its saved entry. An empty group stays empty across restarts.
- Toggle "开机自启动" in the management window or tray menu. It defaults off and uses only the current user's `Software\Microsoft\Windows\CurrentVersion\Run\DeskDecor` value with the quoted current EXE path. Turning it off removes that value. Startup checks remove a registration that points to a different location; a two-second check while running also removes it if the original EXE path disappears. A tray notification and settings text explain that autostart was disabled because the location changed. It stays off until explicitly enabled again. When the app is closed, movement is detected the next time the user manually launches it; no background helper is installed.
- The transparent popup is owned by the Windows desktop and positioned immediately above it, so regular application windows cover it. It never uses TopMost and does not activate when clicked or dragged.
- Toggle taskbar transparency and native desktop icon visibility from the tray menu or settings window. Taskbar changes are restored when the app exits, including abnormal process exit via ExplorerTAP's process watcher. Taskbar effects are applied per toggle or taskbar recreation event; background refresh does not reapply them or broadcast theme changes.
- Keep the EXE in a writable folder so it can save `DeskDecor.settings.xml` alongside itself.

On first run, existing entries from the user and public Desktop folders are shown. Items are references to their original paths; files are not copied or moved. The initialized empty-list marker prevents removed entries from being imported again.

The EXE uses the Windows .NET Framework installed with Windows. It is a portable x64 app with an embedded native XAML engine; first use extracts the hash-checked engine and its notices into `DeskDecor.runtime/ExplorerTAP-2026.2-x64/` beside the EXE. Keep this folder while it runs. No installation or administrator rights are required. The XAML engine stays loaded in Explorer until Explorer exits; it restores the original taskbar appearance when DeskDecor stops.

## Taskbar compatibility

Windows 11 XAML taskbars use the embedded TranslucentTB ExplorerTAP API v4. `BackgroundFill` is set to a SolidColor brush with zero alpha and the background border is hidden. The original fills are restored when disabled. Connection runs on a background MTA thread, so the settings window stays responsive. The legacy AccentPolicy path is used only for older taskbars. Neither path changes Windows transparency or accent-color registry settings.

The overlay remains a top-level layered popup. The visible window containing `SHELLDLL_DefView` is its owner (`GWLP_HWNDPARENT`), not its child-window parent. In the tested Wallpaper Engine layout this owner is `Progman`, containing both the desktop icon view and a child WorkerW with `WPELiveWallpaper`. This avoids composing a layered cross-process child inside Explorer. Hidden top-level WorkerW notification windows are excluded. The popup is placed immediately above its desktop owner and below ordinary apps; a stable refresh does not rewrite the z-order. Native caption/frame styles are cleared, and layered bitmap positions use virtual-screen coordinates.

The inline title input is independent of the desktop surface, so keyboard activation cannot raise the desktop's owned-window chain over applications. Layer repair also lowers an accidentally raised desktop owner and explicitly clears a stale TopMost state. `SWP_NOOWNERZORDER` prevents surface placement from promoting the owner.

Reference: [TranslucentTB TaskbarAttributeWorker](https://github.com/TranslucentTB/TranslucentTB/blob/release/TranslucentTB/taskbar/taskbarattributeworker.cpp) uses a separate TaskbarService/XAML Diagnostics implementation for XAML taskbars; an AccentPolicy call returning success is not proof of visible transparency.

Run `powershell -ExecutionPolicy Bypass -File tests\run.ps1` for the taskbar and desktop-refresh regression checks. They compile production DesktopFeatures against fake Windows/XAML boundaries, verifying routing, restore, refresh idempotency, and rejection of hidden WorkerW hosts. They do not change Explorer or system preferences. Actual engine connections and failures are recorded in `DeskDecor.debug.log`; `DeskDecor.shell.txt` records desktop window diagnostics.

The same runner checks delayed reorder commits, batch-import normalization and duplicate handling, preservation of original files when removing entries, collapsed geometry/XML persistence, and startup enable/disable against a fake registry boundary. Startup tests do not enable real autostart.

`tests/SurfaceInspector.cs` is an optional developer-only DWM thumbnail viewer. It previews the actual compositor surface without moving or activating the overlay. A visible preview confirms rendered content, but is not a full-desktop screenshot or proof of interaction through Wallpaper Engine.

See THIRD-PARTY-NOTICES.md and LICENSE.md for the included engine and GPL v3 source/distribution terms.
