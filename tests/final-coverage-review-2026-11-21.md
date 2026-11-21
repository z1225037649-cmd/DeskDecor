# 最终测试独立审查（2026-11-21）

审查基线为 `5bdb7fa` 及后续工作区改动。删除分组的重复保存和回归脚本的桌面隔离问题均已修复。新增回归在旧实现失败，在修复后通过，保存的入口归属正确，滚动备份与删除前的主配置逐字节一致。本轮复核未发现其他阻断项。

## 审查发现与复核

**[P2，已修复] 一次删除分组连续保存两次，导致滚动备份丢失删除前的版本。** 旧代码在 `RemoveActiveGroup` 发出保存和刷新事件后，删除按钮回调又执行一次保存和刷新。首次保存把删除前的主配置滚动到 `.bak`，第二次用已经删除分组的配置覆盖 `.bak`。如果首次保存保留了恢复备份，紧接着的第二次保存也会覆盖该备份。现在按钮回调只调用 `RemoveActiveGroup()`，保存和刷新分别由现有事件负责一次，最后一组保护仍在发出任何通知前返回。入口分组夹具已同步。[删除按钮回调](../AppContext.cs#L169)、[保存和刷新订阅](../AppContext.cs#L66)、[删除事件](../OverlayForm.cs#L308)、[原子替换](../DesktopItem.cs#L194)、[回归夹具](EntryGroupRegression.cs#L111)

新增回归在临时副本中构造真实 `AppContext`，反射触发设置窗体真实删除按钮的 `OnClick`，保留生产事件订阅、`SaveSettings` 与 `SettingsStore`。它检查删除后只剩目标组、两条入口均已迁移，以及 `.bak` 字节等于点击前的主配置。主任务日志中，旧回调恰好在备份断言失败，修复后用例通过。这个用例充分覆盖普通滚动备份的原缺陷。恢复后首次保存由已有存储回归另行覆盖。[新增用例](AppContextPersistenceRegression.cs#L39)、[主回归入口](run.ps1#L67)

**[P3，已修复] 旧回归脚本可能向真实桌面图标窗口调用 `ShowWindow`。** 旧副本构造时仍调用 `SetDesktopIconsHidden(false)`，在交互桌面找到图标窗口时会调用 `ShowWindow`。现在脚本在临时副本中以两项系统功能均关闭的断言代替任务栏初始化，并跳过桌面图标初始化。启动项仍使用内存存储，组件、托盘和设置窗体仍保持隐藏。删除事件订阅和 `SaveSettings` 继续使用正式代码。主任务单项复跑通过，仍验证入口迁移和滚动备份的字节一致性。[临时替换](run-appcontext-persistence.ps1#L13)、[原生调用](../DesktopFeatures.cs#L127)

## 核对后的覆盖范围

| 场景 | 已取得的证据与边界 |
| --- | --- |
| 删除首组、空组、最后一组保护及移除入口 | 主任务的隔离版实机点击记录与 XML 一致。删除首组后入口迁至保留组，兼容 `<Group>` 指向首个保留组。空组删除成功，尝试删除最后一组后配置字节未变。菜单移除一个入口后保存余下七个入口，主任务的 SHA-256 检查确认八个合成原文件全部保留。`fixture-state.json` 记录八个原文件，`pre-shell.settings.xml` 保留一组七入口。 |
| 原始样式的标题编辑 | 用户手动输入后按 Enter，隔离 XML 中分组和兼容字段均为合成标题 `FinalQA Native Title`，在后续手势快照中仍保留。隐藏窗体自动化另外覆盖 Enter、Escape、IME 和失焦。 |
| 桌面图标、自启动和任务栏 | 主任务实机窗口探针确认图标隐藏并恢复。真实 HKCU Run 项值与加引号的隔离可执行文件路径一致，关闭后移除，路径失效后定时器自动移除。开关清透的主任务记录含 XAML 状态及合成画面观察。自启动证据覆盖写入与清理，未记录重新登录后自动启动。 |
| Explorer 重启 | `explorer-live-result.json` 显示修复前实际重启后覆盖层仍可见，但 owner 归零。`explorer-after-fix.json` 对应修复后的第二轮实际重启，覆盖层可见且未被隐藏，重绑 owner，图标和任务栏重现，日志记录 XAML 重新连接并应用。两次设置摘要均保持不变。 |
| Wallpaper Engine | 主任务记录由原 `scene.pkg` 切换到自带 `shimmering_particles/scene.json` 并恢复。`wallpaper-switch-result.json` 中切换时与恢复后均有壁纸窗口和带 owner 的可见覆盖层，图标及任务栏可见，配置字节不变。场景身份来自主任务的切换记录，结果 JSON 记录的是窗口状态与配置比较。 |
| 多组拖动、缩放、折叠、排序、框外取消 | 用户进行了原生手势操作。`gesture-before`、`pre-header-fix` 和 `gesture-final` 的 XML 显示第二组位置和尺寸变化，首组几何保留。`collapse-observed` 到 `expanded-verified` 的 XML 显示实际折叠、展开的是第二组，首组持续展开。用户确认修复后「收起」文字完整。有效松手仅改变第二组的入口顺序，`drag-cancel-before` 到 `gesture-final` 两组顺序、分配和几何均保留，七个入口全部保留。 |
| 单屏动态缩放 | 主任务实机完成 125% → 150% → 125%。两组 DPI 均从 120 升至 144 后回到 120，七个入口保留，切换后的配置在连续采样中保持稳定。 |
| 当前自动化 | 主任务运行记录显示入口分组 9/9，备份用例加入前的独立快照和解包回归各有 29 项通过。随后验证了新增备份用例在修复前失败、修复后通过。既有用例还包含两个分组的排序隔离、跨组松手取消，以及 100%、125%、150%、200% 的按钮文字测量。几何模拟、离屏恢复及稳定刷新是自动化证据。 |

真实混合 DPI 双屏和显示器热插拔按用户同意保留为硬件待测。正式版恢复由主任务收尾并汇入总表。早期记录保留各自当时的失败、工具限制和待测判断。早期入口分组 8/9、标题原样式未测以及缺少壁纸切换证据的结论，均已有上述后续记录。复核时英文 README 第 35 行仍称原样式标题编辑未验证，待随最终总表更新。[验证总表](verification-status-2026-11-21.md)、[早期实机记录](live-test-2026-11-21.md)、[早期并行记录](parallel-ui-review-2026-11-21.md)、[英文 README](../README.en.md#L35)

## 待提交和打包范围

- 审查时暂存区为空，生产差异集中在 `AppContext.cs`、`OverlayForm.cs`、`SettingsForm.cs` 与中文 `README.md`，另有 `tests/ReorderRegression.cs` 和 `tests/run.ps1`。`git diff --check` 通过，仅有行尾转换提示。
- `tests/run.ps1` 新依赖 `tests/run-entry-group.ps1`、`tests/HeaderDpiRegression.cs` 和 `tests/run-appcontext-persistence.ps1`。两个子 runner 分别依赖 `tests/EntryGroupRegression.cs` 和 `tests/AppContextPersistenceRegression.cs`。这五个文件在复核时均未跟踪，最终快照需明确包含。未跟踪的 `tests/entry-group-review-2026-11-21.md` 记录此前 8/9 的历史结果，属于可选择保留的历史说明。
- `.codex-final-qa` 及抽查的两个结果路径未被 Git 忽略，广泛暂存可能纳入本地测试数据。按文件暂存并核对索引清单。`package.ps1` 以文件类型复制根目录源码和测试脚本，再压缩既有 `dist/DeskDecor-source`，因此应从干净候选快照生成包并核对归档成员，避免工作区新增测试脚本或上次归档目录残留改变实际交付范围。
