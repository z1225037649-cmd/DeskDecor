# 最终自动化回归与打包复核（2026-11-21）

结论：此前完整回归在快照与源码 ZIP 解包后均为 30 PASS、0 FAIL；最终只调整测试隔离脚本后，按要求增量复跑 AppContext 持久化单项，两处均为 1 PASS、0 FAIL。重新构建、打包和 ZIP 内容指纹核验成功。未重新执行稳定的其余 29 项，本报告不将本轮增量结果描述为最新脚本下再次全套 30 项。仅完成自动化，不宣称真实桌面验收。

## 最新增量复核：AppContext 外部系统边界隔离

生产源码不变。与上一轮完整白名单逐项比较，48 个输入中仅 tests/run-appcontext-persistence.ps1 变化，其余 47 个文件 SHA256 完全相同。本轮重新枚举白名单，在开始和结束分别计算 SHA256，48 个文件的集合与指纹均未发生变化（变化数 0）。

最新脚本在临时 AppContext 源码中额外替换构造时的 SetTaskbarTransparent 与 SetDesktopIconsHidden 外部调用，并断言 TaskbarTransparent 与 DesktopIconsHidden 均为 false；保留生产事件订阅、SaveSettings 及删除分组逻辑。当前夹具两处执行均通过，验证备份仍与删除前主配置逐字节相同。此说明描述最新脚本；下方历史全量记录使用的是补强前边界。

- 本轮开始：2026-11-21T21:57:25.0545197+08:00（Asia/Shanghai）。
- 本轮结束指纹核验：2026-11-21T21:59:12.8850180+08:00。
- 独立目录：C:\Users\user\AppData\Local\Temp\DeskDecor-final-regression-e6b07668a51441f099863ba5b8f020ee；白名单复制至 snapshot，源码 ZIP 解压至 unpacked-source。
- 最新输入完整指纹集合：采用下方上一轮 48 项完整输入表，仅将 tests/run-appcontext-persistence.ps1 的旧哈希 1C89C59298C9F557916CC63338E85813EBEF157AA2120213D1804D0C8BA30DC4 替换为本轮下表值；其余 47 项开始/结束值均保持原值。

| 最新变化输入 | 开始 SHA256 = 结束 SHA256 |
| --- | --- |
| tests\run-appcontext-persistence.ps1 | 358C2AD57DC0962F9FA278055C94ED1C11EE9CBF9142CCC92D09E6460FEBEDEF |


本轮实际命令：在 snapshot 下依次执行以下三项，命令进程退出码 0，无编译警告或错误：

```powershell
$qaRoot = 'C:\Users\user\AppData\Local\Temp\DeskDecor-final-regression-e6b07668a51441f099863ba5b8f020ee'
& '.\tests\run-appcontext-persistence.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'persistence.log')
& '.\build.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'build.log')
& '.\package.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'package.log')
```

对实际生成的源码 ZIP 执行 Expand-Archive 后，在 unpacked-source 中执行（命令进程退出码 0）：

```powershell
& '.\tests\run-appcontext-persistence.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'unpacked-persistence.log')
```

两份日志均精确为 1 PASS、0 FAIL，实际输出相同：

```text
PASS: actual AppContext deletion event chain saves once and retains the exact pre-deletion backup
```

使用 ZipFile.OpenRead 检查本轮真实源码 ZIP：48 个文件，路径集合及逐文件 SHA256 与最新输入完全一致；包含最新隔离脚本、全部必要源码/测试及 vendor 依赖。便携 ZIP 仍精确为 DeskDecor.exe、README.md、README.en.md、LICENSE.md、THIRD-PARTY-NOTICES.md、DeskDecor-source.zip 六项。不存在用户配置、快捷方式、日志、测试产物或 .codex-final-qa 内容。本轮未复制/扫描 .codex-final-qa，未运行生产 EXE、修改系统设置或操作运行中的正式/隔离进程，未提交或 push。

| 最新临时产物 | 字节数 | SHA256 |
| --- | ---: | --- |
| DeskDecor.exe | 701952 | F51087C12E7B028D9052E1BEACA27DC5663FA1405D37EB9A22E595E8C32CC9F8 |
| DeskDecor-source.zip | 2983670 | E3500048A70FA12579502B76BBA49BA4B9E4F2A7B79C0B7FF3428CEBFE5F0615 |
| DeskDecor-portable-x64.zip | 3332069 | C1194661E84074C2B8BC0E323D806D85FA0925961A5A7D45DDC9C78C3EDC5E5E |

本轮已于 2026-11-21T21:59:12.9717898+08:00 校验绝对路径、临时父目录和指定 GUID 后，使用 Remove-Item -LiteralPath 删除独立目录；Test-Path 确认不存在。生产工作区仅更新本报告。

## 上一轮完整回归记录（保留历史证据）

以下执行时间、输入表、产物指纹及 30 行原始输出均属于上一轮全量运行；最新隔离脚本和最新包指纹以上方增量记录为准。

上一轮全量结论（隔离边界补强前）：该轮工作树独立副本的自动化回归全部通过（30 行 PASS、0 行 FAIL），入口分组 9/9；build.ps1 与 package.ps1 均成功。实际生成的源码 ZIP 解压后再次运行主 runner 得到 30 行 PASS，并独立构建成功。仅完成自动化与源码/包内容复核，不代表真实桌面、系统缩放切换、Explorer 重启或正式版恢复的实机验收。

## 范围与隔离

- 起始 HEAD：5bdb7fae96c47631d1074592bad71523d576f6cd；测试对象为包含未提交修复的工作树白名单副本，不能仅凭 HEAD 复现，具体指纹见下表。
- 开始时间：2026-11-21T21:43:09.5107388+08:00（Asia/Shanghai）。
- 独立目录：C:\Users\user\AppData\Local\Temp\DeskDecor-final-regression-ae4a81fff5a04e5388159078f5c5177e；snapshot 为只读复制输入，构建产物只写到此独立目录；unpacked-source 为实际源码 ZIP 的解压结果。
- 输入共 48 个文件：根目录 18 个 .cs、3 个 .ps1、清单/图标/说明/许可证，以及 tests 中 14 个 .cs、5 个 .ps1、两个指定 vendor 文件。
- 未复制 .git、.codex-final-qa、生产 EXE、现有 dist、配置、快捷方式或日志。未读取或打包 .codex-final-qa 内容。
- 未操作桌面 UI、系统设置、注册表、Explorer、Wallpaper Engine 或运行中的正式/隔离进程；未提交、push，也未派生任务。仅更新本报告。
- 运行的是回归可执行文件，未启动 DeskDecor.exe。任务栏/启动项使用假原生/注册表边界；窗体回归使用隐藏控件；入口分组 runner 只在自己的临时源码中替换菜单和重命名对话框的展示边界，保留生产事件/变更逻辑。

## 真实命令与结果

在 snapshot 工作目录顺序执行以下命令（PowerShell，ErrorActionPreference=Stop）。三步共同执行的命令进程退出码为 0，各脚本未抛出错误：

```powershell
$qaRoot = 'C:\Users\user\AppData\Local\Temp\DeskDecor-final-regression-ae4a81fff5a04e5388159078f5c5177e'
& '.\tests\run.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'regression.log')
& '.\build.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'build.log')
& '.\package.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'package.log')
```

实际源码 ZIP 经 Expand-Archive 解压至 unpacked-source，在该目录执行（命令进程退出码 0）：

```powershell
& '.\tests\run.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'unpacked-regression.log')
& '.\build.ps1' *>&1 | Tee-Object -FilePath (Join-Path $qaRoot 'unpacked-build.log')
```

| 回归模块 | 快照 PASS 行 | ZIP 解压 PASS 行 |
| --- | ---: | ---: |
| TaskbarRegression | 3 | 3 |
| ReorderRegression | 4 | 4 |
| EntryFeaturesRegression | 2 | 2 |
| InlineTitleRegression | 1 | 1 |
| SettingsStoreRegression | 1 | 1 |
| DesktopLayoutRecoveryRegression | 1 | 1 |
| OverlayLayoutRegression | 1 | 1 |
| SettingsFormRegression | 2 | 2 |
| GroupSearchRegression | 3 | 3 |
| MenuLifetimeRegression | 1 | 1 |
| EntryGroupRegression | 9 | 9 |
| HeaderDpiRegression | 1 | 1 |
| AppContextPersistenceRegression | 1 | 1 |
| 合计 | 30 | 30 |

本轮更新覆盖删除分组重复保存修复：生产 AppContext 的 RemoveGroupRequested 订阅仅调用 _overlay.RemoveActiveGroup()，由已有 ItemMoved -> SaveSettings 完成持久化，ItemsChanged 刷新列表。EntryGroupRegression 夹具已同步此生产连接。

新增 AppContextPersistenceRegression 与 run-appcontext-persistence.ps1 已接入主 runner。本次两轮均实际输出 PASS: actual AppContext deletion event chain saves once and retains the exact pre-deletion backup。测试构造真实 AppContext、调用真实设置窗体删除按钮，保留生产事件订阅及 SaveSettings，验证删除后仅剩目标分组、全部条目迁移，并逐字节比较 .bak 与删除前主配置。runner 仅在自己的临时源码替换启动项外部存储和窗体/托盘展示边界，断言窗体不可见、overlay HWND 未创建。本任务未自行重做“旧代码先失败”实验，因此该历史实验不计入本报告的独立执行证据。
入口分组输出：RESULT: 9/9 checks passed; presentation suppressed; overlay HWND never created。覆盖单项/批量/搜索过滤后的跨组移动、显示名保存/取消/空值/无效选择、删除中间组/首组/空组/最后组保护，并明确断言删除首组后 legacy Settings.Group 和保存的 XML 不再残留被删除组。原始目标文件的内容、路径、时间戳及全局顺序等由该回归断言检查。

HeaderDpiRegression 在 96/120/144/192 DPI（100%/125%/150%/200%）逐一测量“收起”和“展开”，检查单行可完整容纳、位于按钮内且不与箭头重叠；这是离屏测量，未修改 Windows 显示缩放。

新回归已进入 tests/run.ps1：入口分组通过 run-entry-group.ps1 执行，HeaderDpiRegression 由主 runner 直接编译并运行。RefreshDesktopLayout 中 EnsureDesktopAttached 已移到 desktopChanged 条件块外，几何未变也可走幂等修复；此调用关系经源码复核，任务栏假边界回归通过，但本报告未测试真实 Explorer owner 重建。

## 构建与实际 ZIP 核验

- build.ps1 成功生成 x64 DeskDecor.exe，编译器未输出警告/错误；内嵌 ExplorerTAP DLL 校验通过。
- 使用 System.IO.Compression.ZipFile.OpenRead 读取真实源码 ZIP：48 个文件，路径集合与输入白名单完全一致；逐个条目计算 SHA256，与复制前输入指纹全部相同（包括图标及 vendor 文件）。所有必要 .cs/.ps1、app.manifest、DeskDecor.ico、文档/许可证和 vendor 依赖均存在。
- 源码包包含 tests/EntryGroupRegression.cs、tests/run-entry-group.ps1、tests/HeaderDpiRegression.cs、tests/AppContextPersistenceRegression.cs、tests/run-appcontext-persistence.ps1 及已接入它们的 tests/run.ps1；实际解压后回归和构建成功，验证包内容可用。
- 源码包不包含生产配置、settings.xml、.bak/.tmp/.config、.lnk、.log、.git、.codex-final-qa、回归 EXE 或测试报告。vendor 上游源码 ZIP 原样包含，SHA256 与输入一致。
- 便携 ZIP 精确包含 6 项：DeskDecor.exe、README.md、README.en.md、LICENSE.md、THIRD-PARTY-NOTICES.md、DeskDecor-source.zip，路径集合无额外项。
- 本次核验使用全新临时副本，不复用生产 dist；临时产物清理后不留在生产目录。

| 临时产物 | 字节数 | SHA256 |
| --- | ---: | --- |
| DeskDecor.exe | 701952 | 60B5720FBE3B3C59ACFA05F3AAB784309B058B844611AD89F729C8DD31C4B38A |
| DeskDecor-source.zip | 2983560 | 69C6D47A4387EE8E00DAD4E34E07B3A03F7EEC8BD68C443B8E821556E82AFA1D |
| DeskDecor-portable-x64.zip | 3331959 | F47451C22DC88A447E4BDDCEDEAF372C124D4C44D6C4A7F130D9FDD69EDD5EB1 |

以上产物哈希仅标识本次生成结果；编译/压缩时间等会影响后续重建哈希。

## 开始/结束输入 SHA256

复制前与全部验证结束时重新枚举同一白名单并计算 SHA256，文件集合及 48 个文件的指纹完全一致，变化数 0，因此无需因生产源码变更重新建立快照。下表每项同时代表开始值与结束值。

| 输入文件 | 开始 SHA256 = 结束 SHA256 |
| --- | --- |
| app.manifest | 509F12393A046A1468BC96D7E92327B025E5AE14A907FEE39CF1152A04CC5051 |
| AppContext.cs | 2B07188F6CB035FDDFF34155101124ABAE2DC855C6109C4C5339B39B79DB62AA |
| build.ps1 | 2EB466C6120D66BD099200329D1E13356626619E4EA90D0A85EA64CFC3641A52 |
| DeskDecor.ico | 93F3B66ED0DD9833B38E73AEDB5FAFDF0BC31F5229FC28547963155A78F67EB8 |
| DesktopEntries.cs | 47EC098DE25164C1BABEB06D9350D4398100E49E90AB73B64DE3DC06890BF771 |
| DesktopFeatures.cs | 22AC881A19381307E442155E9B3BD3F9734BBAA2380F640449514D4BF33FF079 |
| DesktopItem.cs | A06553D74C913E62E0AB08D208BE6F85C8F067B88A04F20225DC17E90071462D |
| DesktopLayoutRecovery.cs | EFA909D7E439370C8891781A79CC6A1F87DF903C144157A3070583D32BAC0FBD |
| InlineTitleEditor.cs | D292A4966097D31853E3FCD9C82AE8C9820BC02DB2D09F19DCF40F8EA80D90D6 |
| ItemReorderGesture.cs | 73B1E55505164869D740F1AA3D07FAC23A1ABEF70A75155ACB9C1153B840CE36 |
| LayeredSurface.cs | E050749DB530D34517B3ACDCD4B85507A24F13F58B2324809F7D06057DB25D4A |
| LICENSE.md | 5C30B33202453DFA813E7660EACF015487A06D34248FE04932E0E41CEE5A932A |
| make_icon.ps1 | 4C7F9035B2DE8A472D21957697773813FA03076DE1A171AD296D41BFBA10D09F |
| MonitorDpi.cs | D5847EA2C1BC9F1734C017B65CC2F64652D2FCEFA61F7896D879280BFDB145D2 |
| Native.cs | B8DC8465BB9787222F563AA1D66889476BCD3FA73B082ED0361BB886479D3AD6 |
| OverlayForm.cs | 2660A29B534D6F1DF74ABF57D2D140DEF9C399E70318C9034EA3CF4276E9EE87 |
| package.ps1 | 691CF1C3DC6BE960174361A2A5837A7E30742BD7DB359D535482EDC9375333D4 |
| Program.cs | 6E27D071FEC0D00DD6F1ACFA97CE85A0FD2A806968588285E00F8E1A80D98E2F |
| README.en.md | C495BB3D83BD601D13D7004C3552693CFDE5F1BBCC2EA170A98F4DBB58D7EB9F |
| README.md | A893CB0302FD905E9C52270F19C54277DF18191F77DB55FA86541AB9108608F4 |
| SettingsForm.cs | 570C5C9AD39A07FB71D9350DCD95D1B6290E9146D1E56979D54EADBBFE451D74 |
| ShellDiagnostics.cs | D031559EC4DB0C1223B166C7D2DDA9C5AF30032A96C14A113F21C8CD19217FA4 |
| ShellIconCache.cs | 7400DEE74AFEF4A087C53D6C89B64B11CD523D37467EF04719FF0C884FBC89F8 |
| SoftButton.cs | 005E408069CC6441D95F2AF37BEB032B0A1ADE908BB51D53A517E1893CBC0B93 |
| StartupRegistration.cs | C9F5786CABEC369F8C25B847F0C4102303D8C57E10243AEA5D8DF074F5FE4BDC |
| tests\AppContextPersistenceRegression.cs | EE3384FBDD20D027D753303C3654A6EDC71BB0E8E5DE1793B6678C97804EED21 |
| tests\DesktopLayoutRecoveryRegression.cs | 73E4E3B3FFB2E63EAC3A2AF84BA55B915BCAF40EFCC0B9D3532B145D3017E0DA |
| tests\EntryFeaturesRegression.cs | 304AAD0BE977D31E43DD89258BAB3933ADA32C01A899D27D319A654C2AEE0D88 |
| tests\EntryGroupRegression.cs | B9434F5A0E1FF18AE56945D27B75FA03D330A56823DBE5E81D9F6A655A77321A |
| tests\GroupSearchRegression.cs | 53B9F21F066713918505F129447936CC50501E992AB1D7BDD7C217283ACBB98E |
| tests\HeaderDpiRegression.cs | 8D8222DE9E3CE4F6B88B1B9164ADCDCFCB04E5770DA4993906858CAD96F273A4 |
| tests\InlineTitleRegression.cs | 10D61B944750317C67C9B681BE568D2F868B05D71C1268469179428420D31A04 |
| tests\MenuLifetimeRegression.cs | E8986924C3C1EB1EB6C71C08F2BF1D24420152FB453289815660DDD523055B87 |
| tests\OverlayLayoutRegression.cs | 48F45AEF5D7628C0EF69BF92D7DD5F4FFA1F051B86C01160CAECCCBD83F674ED |
| tests\ReorderRegression.cs | BDE000811D44AFFAF4CEEC15B097BE256EF8B1BCFAAA1D623FFB627FB21E6C01 |
| tests\run-appcontext-persistence.ps1 | 1C89C59298C9F557916CC63338E85813EBEF157AA2120213D1804D0C8BA30DC4 |
| tests\run-entry-group.ps1 | C6CE916100336619A1F0BECE055D0C5A711AD32291EF67785F44C41546236ECC |
| tests\run-group-search.ps1 | 658564E312CF18B2892B697033206994FFC2E3FB8789D8C507D932D0BD353872 |
| tests\run-menu-lifetime.ps1 | 30555785EB2046379E5EB48C3286825992AEAA08C9A1E04CDF7B0A10CDCD6CEE |
| tests\run.ps1 | EE6904F7E4DAC3F163A4E56F31FA90FEBE01206CB7418A2FAC3E60BDF2EDD154 |
| tests\SettingsFormRegression.cs | 90D87024E7C41C81D775749CE364FE2CA26E8834AD55FA91F1A4CC777A38F65B |
| tests\SettingsStoreRegression.cs | E2519693DF1A3D0C09DA9D21A2CF0AF454DA4A624C1EDAF1321E52BD25AB8714 |
| tests\SurfaceInspector.cs | D4496D7C86AF02E287758704FD66112579652DD8B97098DC1D36242874CFC019 |
| tests\TaskbarRegression.cs | 99E75F03FAF18D7EA0AFE1D060E87FB4024400836F0F3BAFEC0DA3C132E741CF |
| THIRD-PARTY-NOTICES.md | 8396347CF52E4D7E0EBD8981CBDFA12A52A486A44AA7B6B520286704D4E09EC9 |
| vendor\TranslucentTB-2026.2-source.zip | C89EC1DC77B55F9FDC7C455E1A4A9CD435F730CBB352071EB5D6FEE7AB74B5EF |
| vendor\TranslucentTB-2026.2-x64\ExplorerTAP.dll | C48F1A06168ACD7839269DDE0D15CCF5D09C702512B94DD6BA9A05DF1B153DC2 |
| XamlTaskbarEngine.cs | 09D61F7FE6EB9BE5967C870EAB9337BC9971FA5292A0CC9035F9C7D02B244B3B |

结束指纹核验时间：2026-11-21T21:45:23.1229111+08:00。

## 原始快照回归输出

```text
PASS: enable/refresh/disable produces no repeated accent or frame writes
PASS: XAML uses the dedicated engine once and restores when disabled
PASS: stable desktop refresh avoids WorkerW and window-position churn
PASS: threshold, crossing without changes, final-release insertion and single commit
PASS: cancellation, outside release and original-position no-op
PASS: trailing-space append retains all saved entries
PASS: multi-group reorder retains other-group order, metadata and assignments; cross-group release cancels
PASS: startup path changes clear the old entry once, retain unchanged entries and reject missing EXEs (fake registry only)
PASS: batch import, duplicates, non-destructive removal, collapsed geometry and empty-list persistence
PASS: inline bounds, Enter, Escape, IME composition and focus-loss save (no visible test windows)
PASS: atomic replacement, rolling backup, backup recovery, recovery-backup retention and failure preservation
PASS: off-screen recovery, nearest-monitor selection, relative entry positions, collapsed geometry and DPI scaling
PASS: real overlay layout fits recovered content and stops repeated recovery, including capacity overflow (no visible test windows)
PASS: settings search isolates groups and matches labels and paths without editing entries
PASS: settings group selection notifies the overlay once and refresh does not recurse (no visible test windows)
PASS: search filters current-group labels, names and paths; empty, whitespace and unmatched queries behave correctly
PASS: form and overlay group selection synchronize through the production event flow; RefreshGroups does not recurse
PASS: regression used hidden real forms without visible windows or data edits
PASS: menu dismissal retains commands, next menu replaces/disposes the previous one, and form disposal releases the final menu
SOURCE SHA256 SettingsForm.cs 570C5C9AD39A07FB71D9350DCD95D1B6290E9146D1E56979D54EADBBFE451D74
SOURCE SHA256 OverlayForm.cs 2660A29B534D6F1DF74ABF57D2D140DEF9C399E70318C9034EA3CF4276E9EE87
SOURCE SHA256 AppContext.cs 2B07188F6CB035FDDFF34155101124ABAE2DC855C6109C4C5339B39B79DB62AA
SOURCE SHA256 DesktopItem.cs A06553D74C913E62E0AB08D208BE6F85C8F067B88A04F20225DC17E90071462D
SOURCE SHA256 DesktopEntries.cs 47EC098DE25164C1BABEB06D9350D4398100E49E90AB73B64DE3DC06890BF771
PASS: overlay single-entry move, exact event order, inactive destination layout and no-op guards
PASS: settings context move replaces selection on an unselected row and refreshes both groups
PASS: settings batch selection moves exactly selected entries once and retains order/labels
PASS: filtered settings batch move preserves query and excludes hidden source entries
PASS: display-name save trims text, updates search and persists without renaming originals
PASS: display-name cancel/blank and zero/multiple selection never mutate or notify
PASS: delete active middle group migrates entries to first remaining group and syncs controls
PASS: delete first/empty groups retains every entry; last-group deletion is blocked
PASS: deleted first group must not remain in the legacy Group field or saved XML
RESULT: 9/9 checks passed; presentation suppressed; overlay HWND never created
CLEANUP: removed task temporary directory C:\Users\user\AppData\Local\Temp\DeskDecor-entry-group-5feb8fa38d974306bb9aa1244363d9ae
PASS: collapse and expand labels fit at 100%, 125%, 150% and 200% without overlapping the chevron
PASS: actual AppContext deletion event chain saves once and retains the exact pre-deletion backup
```

## 清理

已于 2026-11-21T21:45:23.2474083+08:00 核验绝对路径、临时父目录及指定 GUID 名称后，使用 Remove-Item -LiteralPath 删除本轮独立目录；Test-Path 确认不存在。两轮原始日志清理前均已核对 PASS=30、FAIL=0。原始快照输出、最新全部输入指纹和临时产物指纹已保存在本报告。本报告以本轮 30 PASS 结果替代此前 29 PASS 快照结论；生产目录未生成或覆盖任何构建产物。
