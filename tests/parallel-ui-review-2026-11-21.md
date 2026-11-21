# ParallelUI 真实界面复测 — 2026-11-21

## 最新结论（用户要求修改后继续验证）

本轮真实界面复测发现设置页右键“移动到分组”会触发 `ObjectDisposedException: ContextMenuStrip`，移动没有保存。在独占副本中修复菜单生命周期后，单个与多个入口移动均通过真实界面及 XML 核验；分组确认、搜索、显示名称修改及重启持久化也通过。分组名称输入和保存已在去掉工具窗口隐藏标志的测试编辑器中通过，随后恢复原始样式重启确认名称保留；原始隐藏编辑器的工具可操作性仍未验证。

遵守最初“只可写独占目录和本报告、不要改根目录生产源码”的范围，**生产 SettingsForm.cs 尚未应用修复**。经验证的最小补丁保留在本文末尾；测试副本清理后仍可据此复原修复。

## 首次复测结论（历史）

隔离副本构建成功，设置窗口成功显示。真实交互被 Computer Use 的窗口边界错误阻塞；重新选择并抓取窗口后仍失败，没有成功点击或输入。分组选择确认、搜索、单个/多个入口移动、显示名称和分组名称修改仍未验证，不能标记通过，也不能据此判定应用缺陷。

## 隔离

- 已阅读既有 live-test 记录及 computer-use 的 SKILL.md、guidance.md、api.md、confirmations.md；所有 Windows UI 操作仅使用 node_repl 中的 @oai/sky。
- 独占目录 `.codex-parallel-ui/` 从当前未提交工作区复制全部根目录 C# 源码及必需构建资源，保留当前修复；仅修改副本隔离标识及设置/桌面层/内联编辑器标题，没有修改生产源码或共享测试文件。
- mutex：`DeskDecor.ParallelUI.SingleInstance.20261121`；event：`Local\DeskDecor.ParallelUI.ShowSettings.20261121`；message-window：`DeskDecor.ParallelUI.InstanceMessageWindow`；startup 注册值名：`DeskDecor.ParallelUI.20261121`。
- 设置窗口标题 `DeskDecor ParallelUI`；桌面层 `DeskDecor.ParallelUI.DesktopSurface`；内联编辑器 `DeskDecor ParallelUI InlineTitleEditor`；项目重命名对话框 `ParallelUI 重命名项目`。
- 配置只含 6 个本轮创建的合成 txt 文件，全部位于独占目录 `synthetic/`。Source 组含 Label 1–5，Target 组含 Label 6；初始激活 Target。
- 配置 `TaskbarTransparent=false`、`DesktopIconsHidden=false`、`DesktopInitialized=true`，图标 32、透明度 60。未操作系统特性开关，隔离 startup 注册值未发现。
- 未创建对话或委派代理；未运行共享 tests/run.ps1，未提交、推送或重置。

## 可见证据与工具结果

1. 复制的 build.ps1 成功构建。测试 exe SHA-256：`4FF5227FDCB5D41805288B453BA87F6DA1B9EBC9204A36AE175BDD787119ED8F`。
2. 初次 `sky.launch_app` 返回 `launched app did not expose a targetable window`。按明确测试 exe 路径再次唤醒后，`sky.list_windows` 返回唯一测试窗口：id=2427322，title=`DeskDecor ParallelUI`，app=`process:C:\Users\user\Desktop\mimo\DeskDecor-GitHub\.codex-parallel-ui\DeskDecor.exe`。
3. 使用该返回对象抓取截图和可访问性树。截图清晰显示设置窗口，分组为 `ParallelUI Target`，列表仅有 `ParallelUI Label 6`；搜索框为空；图标 32、透明度 60；任务栏透明、隐藏系统桌面图标、开机自启动均未勾选，状态文字为“任务栏透明状态：已关闭”。
4. 树中搜索框为索引 3，列表项目为索引 8，位置文字为独占 synthetic/ParallelUI-Item-06.txt 完整路径，分组框为索引 11、Value=`ParallelUI Target`；索引 21 为“修改分组名称”。
5. 对最新观测中的分组索引 11 点击，返回：`window bounds changed; call get_window_state before using this window`。
6. 重新 `sky.list_windows`、按测试路径和标题唯一匹配、`sky.get_window`、`sky.get_window_state`。新截图及树仍显示 Target / Label 6，窗口 id 不变。仅使用新观测的分组索引重试一次，仍返回同一错误。
7. 按技能恢复失败即报告的规则停止，未复用坐标、未重开 UI 会话、未继续输入。这是工具输入边界错误；本轮没有 UI 审批超时或自动审批拒绝。

## 各项状态

| 项目 | 本轮结果 | 限制 |
| --- | --- | --- |
| 初始设置显示 | 通过显示检查 | 截图与可访问性树一致 |
| 分组选择确认 | 阻塞，未验证 | 两次打开分组框的点击均失败 |
| 搜索 | 未验证 | 未输入搜索文本 |
| 单个入口移动 | 未验证 | 未操作右键菜单 |
| 多个入口移动 | 未验证 | 未执行多选及移动 |
| 修改显示名称 | 未验证 | 未打开或保存重命名对话框 |
| 修改分组名称 | 未验证 | 未触发或输入内联编辑器 |

既有隐藏控件测试覆盖不替代以上真实界面验证。

## 持久化证据

停止前直接读取独占主 XML：ActiveGroupId=`parallel-ui-target`；组标题仍为 `ParallelUI Source`、`ParallelUI Target`。6 个入口全部保留，Name 分别为 `ParallelUI-Item-01.txt` 至 `ParallelUI-Item-06.txt`，Label 分别为 `ParallelUI Label 1` 至 `ParallelUI Label 6`；1–5 的 GroupId 为 `parallel-ui-source`，6 为 `parallel-ui-target`。没有移动或重命名落盘。TaskbarTransparent、DesktopIconsHidden 仍为 false；DesktopInitialized=true、IconSize=32、Opacity=60。

## 清理

- 普通权限关闭测试 PID 21696 返回“拒绝访问”；随后获准的提权命令先校验该进程的精确可执行路径为独占目录中的 DeskDecor.exe，再成功关闭它。
- 清理后独占路径无测试进程；正式便携实例 PID 16040 仍从 `C:\Users\user\Desktop\mimo\desktop-beautifier\dist\DeskDecor-portable-x64\DeskDecor.exe` 运行，未对其发送输入或终止命令。
- 本轮创建的副本、构建资源、配置、日志、合成文件均已清理；仅保留本报告。


配置初始与停止前 SHA-256 均为：5399961D0315A6269FC63FA58271D1A4984552CBBA1BF8AC3A15D15B894C7107，字节完全一致。

## 构建时生产源码指纹

以下仅记录复制时快照，未写回对应生产文件。

~~~json
[
  {
    "File": "AppContext.cs",
    "SHA256": "84DD6042A6B4722FE035E429AF1C884EAC484C739C2548600A7A3E3753783BA4"
  },
  {
    "File": "DesktopEntries.cs",
    "SHA256": "47EC098DE25164C1BABEB06D9350D4398100E49E90AB73B64DE3DC06890BF771"
  },
  {
    "File": "DesktopFeatures.cs",
    "SHA256": "22AC881A19381307E442155E9B3BD3F9734BBAA2380F640449514D4BF33FF079"
  },
  {
    "File": "DesktopItem.cs",
    "SHA256": "A06553D74C913E62E0AB08D208BE6F85C8F067B88A04F20225DC17E90071462D"
  },
  {
    "File": "DesktopLayoutRecovery.cs",
    "SHA256": "EFA909D7E439370C8891781A79CC6A1F87DF903C144157A3070583D32BAC0FBD"
  },
  {
    "File": "InlineTitleEditor.cs",
    "SHA256": "D292A4966097D31853E3FCD9C82AE8C9820BC02DB2D09F19DCF40F8EA80D90D6"
  },
  {
    "File": "ItemReorderGesture.cs",
    "SHA256": "73B1E55505164869D740F1AA3D07FAC23A1ABEF70A75155ACB9C1153B840CE36"
  },
  {
    "File": "LayeredSurface.cs",
    "SHA256": "E050749DB530D34517B3ACDCD4B85507A24F13F58B2324809F7D06057DB25D4A"
  },
  {
    "File": "MonitorDpi.cs",
    "SHA256": "D5847EA2C1BC9F1734C017B65CC2F64652D2FCEFA61F7896D879280BFDB145D2"
  },
  {
    "File": "Native.cs",
    "SHA256": "B8DC8465BB9787222F563AA1D66889476BCD3FA73B082ED0361BB886479D3AD6"
  },
  {
    "File": "OverlayForm.cs",
    "SHA256": "1A51C515335CAB22B9AB83D877EEEDB3C47D5B079CFB6D3A1879C23D87C22524"
  },
  {
    "File": "Program.cs",
    "SHA256": "6E27D071FEC0D00DD6F1ACFA97CE85A0FD2A806968588285E00F8E1A80D98E2F"
  },
  {
    "File": "SettingsForm.cs",
    "SHA256": "65FA2B1946DEFB63129480913ED4B6FBAFCC341D191C58F6CA86BF4EB0B6E361"
  },
  {
    "File": "ShellDiagnostics.cs",
    "SHA256": "D031559EC4DB0C1223B166C7D2DDA9C5AF30032A96C14A113F21C8CD19217FA4"
  },
  {
    "File": "ShellIconCache.cs",
    "SHA256": "7400DEE74AFEF4A087C53D6C89B64B11CD523D37467EF04719FF0C884FBC89F8"
  },
  {
    "File": "SoftButton.cs",
    "SHA256": "005E408069CC6441D95F2AF37BEB032B0A1ADE908BB51D53A517E1893CBC0B93"
  },
  {
    "File": "StartupRegistration.cs",
    "SHA256": "C9F5786CABEC369F8C25B847F0C4102303D8C57E10243AEA5D8DF074F5FE4BDC"
  },
  {
    "File": "XamlTaskbarEngine.cs",
    "SHA256": "09D61F7FE6EB9BE5967C870EAB9337BC9971FA5292A0CC9035F9C7D02B244B3B"
  }
]

~~~


## 修改后继续验证 — 第二轮

### 工具输入方式调整

先在独占副本暂时将 manifest 从 PerMonitorV2 改为 system DPI，诊断窗口边界错误。键盘 Tab 与带最新 screenshotId 的截图坐标点击均可成功。随后立即恢复根目录原始 app.manifest 并重建；以下搜索、入口重命名、单/多入口移动及 Target 切换均使用原始 PerMonitorV2 模式，不能将 DPI 临时调整认定为已证明的修复。

带 screenshotId 的截图坐标点击可绕过此前索引点击的边界错误。中途出现过 “user input was detected in this window” 和 “unknown screenshotId screenshot-0”；均停止旧输入、重新观测，必要时重新绑定返回的窗口对象后继续，没有猜测窗口句柄。

### 真实缺陷与副本修复

复现路径：Source 列表右击单个入口 → 移动到分组 → Target。

原始代码在弹出菜单 Closed 事件内立即 Dispose。点击 Target 后出现可见 WinForms 未处理异常对话框；XML 中入口仍在 Source。可访问性树原文含：

~~~text
System.ObjectDisposedException: 无法访问已释放的对象。
对象名:“ContextMenuStrip”。
在 System.Windows.Forms.Control.CreateHandle()
在 System.Windows.Forms.ToolStripDropDown.CreateHandle()
在 System.Windows.Forms.Control.get_Handle()
在 System.Windows.Forms.ToolStripDropDown.ReparentToDropDownOwnerWindow()
在 System.Windows.Forms.ToolStripDropDown.SetVisibleCore(Boolean visible)
在 System.Windows.Forms.ToolStripDropDown.OnItemClicked(ToolStripItemClickedEventArgs e)
在 System.Windows.Forms.ToolStrip.HandleItemClick(ToolStripItem dismissingItem)
在 System.Windows.Forms.ToolStripItem.HandleClick(EventArgs e)
在 System.Windows.Forms.ToolStripItem.HandleMouseUp(MouseEventArgs e)
~~~

修复只在独占 SettingsForm.cs 副本实施：将当前菜单保存在窗体字段中；下一次创建菜单时释放上一个菜单，窗体 Dispose 时释放最后一个菜单；移除 Closed 中的即时释放。避免菜单点击消息还在处理时销毁菜单。改动后的复制 build.ps1 构建成功。

### 最新结果

| 检查 | 结果 | 实际可见及持久化证据 |
| --- | --- | --- |
| 确认分组选择 | 通过 | Source 可见 5 项；切至 Target 后按 Enter 关闭下拉框，目标列表可见入口 1、2、3、6；XML ActiveGroupId=parallel-ui-target |
| 搜索 | 通过 | 在原始 PerMonitorV2 副本输入小写 label 2，仅显示 ParallelUI Label 2；清空后恢复 5 项 |
| 修改显示名称 | 通过 | 双击入口 1 打开真实对话框，设置文本后实际点击“保存”；列表与 XML 变为 ParallelUI Renamed Entry；原 Name/Path 保留 |
| 单入口移动（修复前） | 失败，已复现缺陷 | Target 菜单点击抛出上述 ContextMenuStrip 异常；入口 1 未落盘移动 |
| 单入口移动（修复副本） | 通过 | 相同菜单点击成功，Source 从 5 项变 4 项；仅入口 1 的 GroupId 变为 Target，未出现异常 |
| 多入口移动（修复副本） | 通过 | 截图确认同时高亮入口 2、3；右击选中项保持双选，目标菜单点击后 Source 仅剩 4、5；XML 仅新增移动 2、3 |
| 分组名称输入及保存 | 有边界的通过 | 原始隐藏编辑器无法被工具枚举；仅在副本将 ShowInTaskbar=false 改为 true，并清除 WS_EX_TOOLWINDOW 后，返回独立编辑器窗口 id=152898580；输入 ParallelUI Renamed Group 后按 Enter，XML 组标题保存成功 |
| 原始隐藏编辑器真实可操作性 | 仍未验证 | 原始样式时 settings 隐藏，list_windows/list_apps 均未返回可用编辑器；不把样式适配后的结果当作原始样式通过 |
| 恢复样式后的重启持久化 | 通过 | 恢复原始 InlineTitleEditor.cs（仅保留唯一窗口标题）后重建、重启，设置列表显示入口 1、2、3、6，分组框可访问性 Value=ParallelUI Renamed Group |
| 合成原文件完整性 | 通过 | 6 个文件均存在，原名称及路径不变，内容分别与创建时的 Synthetic ParallelUI fixture 1–6 逐字一致；目录恰好 6 个文件 |

入口对话框首次使用 type_text 时焦点被主窗口激活影响，文本没有改变；重新观测后使用真实 UI Automation set_value 设置可见文本框，再点击可见保存按钮。没有将未生效输入记录为成功。

分组编辑器适配只影响可发现性标志，输入、Enter 提交及持久化事件代码保持原样；最终构建已恢复这些标志。没有关闭或操作正式便携实例。

### 最终持久化状态与指纹

~~~text
ActiveGroupId = parallel-ui-target
Source.Title = ParallelUI Source
Target.Title = ParallelUI Renamed Group
Item 1.Label = ParallelUI Renamed Entry
Target items = 1, 2, 3, 6
Source items = 4, 5
All six original Name/Path values preserved
TaskbarTransparent=false; DesktopIconsHidden=false
IconSize=32; Opacity=60
~~~

- 在仅进行移动、显示名称及分组确认之后，重启前后 XML SHA-256 均为 E2F748DCF9EF1A4F3A97265565F3076ECA992BF99E5C12686F23DC1614113B96。
- 保存分组名称、恢复原始编辑器样式后的最终重启前后 XML SHA-256 均为 B1FD4FE85B74FE59DD5A10B7BBF4A2BA4B520CA30BB270DEFF88639A5D871BEE。
- 最终隔离 exe SHA-256：FD5AEB044D24B745F3827DF5F6F677EC10785924A785CE5E5BE0D58D1F1CB5D2。
- 最终 SettingsForm.cs 副本 SHA-256：2562F217FA3853B36CC72DA72197D30A562992500A5F622D7DEA4A56CDCFDB07。
- 生产 SettingsForm.cs SHA-256 仍为 65FA2B1946DEFB63129480913ED4B6FBAFCC341D191C58F6CA86BF4EB0B6E361。尚未合入下面的修复。

### 已验证的最小生产修复补丁（未应用）

仅包含菜单生命周期改动，不包含 ParallelUI 标识或诊断适配。遵守最初任务写入范围，留给有生产源码修改权限的统一任务应用。

~~~diff
--- a/SettingsForm.cs
+++ b/SettingsForm.cs
@@
         private readonly ListView _list;
+        private ContextMenuStrip _listMenu;
@@
-                ContextMenuStrip menu = new ContextMenuStrip();
+                if (_listMenu != null) _listMenu.Dispose();
+                ContextMenuStrip menu = _listMenu = new ContextMenuStrip();
@@
-                menu.Closed += delegate { menu.Dispose(); };
                 menu.Show(_list, e.Location);
@@
-            if (disposing) _settingsHelp.Dispose();
+            if (disposing)
+            {
+                if (_listMenu != null) _listMenu.Dispose();
+                _settingsHelp.Dispose();
+            }
~~~

该改动验证了实际菜单的两次创建/释放周期（单选菜单与多选菜单）及相同目标子菜单点击。未操作“移除选中入口”，未进行真实文件删除。未运行或改动共享回归脚本；本轮构建均在独占目录完成，没有把测试构建写回根目录。

### 第二轮清理结果

- 已按精确路径关闭最终隔离 PID 22504；首次即时复查尚能看到退出中的进程，随后独立复查确认该路径无残留。
- 已核验并删除独占目录 .codex-parallel-ui/（包括源码副本、候选源码、exe、资源、配置、日志与合成文件）。
- 正式便携实例仍运行，PID 16040。
- 修复补丁及验证证据仅保留在本报告，没有修改生产源码、既有其他测试记录或共享脚本。
