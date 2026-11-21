# 入口与分组回归记录 — 2026-11-21

## 结论

**后续状态：** 下述 8/9 为历史结果。兼容字段已修复并复跑至 9/9，另已修复 AppContext 重复保存导致的备份覆盖，生产事件链回归通过；实际首组/空组删除与最后组保护也已完成。最新结论见 `verification-status-2026-11-21.md` 和 `live-completion-2026-11-21.md`。

独立脚本实际编译、运行完成：**8/9 项通过，1 项失败，退出码 1**。失败项为低优先级的兼容字段一致性问题：删除首分组后，`AppSettings.Group` 和未归一化 XML 的 `<Group>` 仍保留被删除分组。入口归属、当前分组、设置列表、布局及正常保存重载均通过，未观察到入口或原文件丢失。生产源码未修改。

## 测试方式与边界

- 运行命令：在项目根目录执行 `& tests/run-entry-group.ps1`。
- 使用 .NET Framework64 v4.0.30319 的 C# 编译器；所有根目录 C# 源码复制到 GUID 独立的系统临时目录后编译，入口为 `EntryGroupRegression.Main`，未启动 `Program.Main` 或构造 `AppContext`。
- `OverlayForm` 使用未经行为修改的源码，直接测试真实 `MoveItemToGroup`、`RemoveActiveGroup`、`SetActiveGroup` 和布局方法；整个测试中不创建 overlay HWND。
- 设置列表使用真实 WinForms `ListView`、原生隐藏 HWND、真实选择集合和命中测试。通过反射调用 `OnMouseUp`、`OnClick` 触发原有回调，通过真实 `ToolStripItem.PerformClick` 触发目标菜单命令。设置窗体始终隐藏；没有 Windows UI 自动化或输入注入。
- 为禁止任何窗口显示，仅在临时 `SettingsForm.cs` 副本中替换两个显示边界：`menu.Show(_list, e.Location)` 改为保留未显示菜单；`dialog.ShowDialog(this)` 改为检查真实输入框／保存／取消控件并返回指定结果。脚本要求每个替换点恰好出现一次，否则停止。移动、名称校验、`Changed()` 和事件回调保持原代码。
- 名称测试直接进入真实 `RenameSelectedItem`。测试模拟的是对话框返回结果，**不覆盖实际按钮输入、模态消息循环或可见渲染**。
- 按 `AppContext.cs:149`、`:169` 及 overlay 事件订阅逻辑连接隐藏窗体：设置更改触发布局刷新及保存；overlay 的 `ItemMoved` 保存、`ItemsChanged` 刷新列表；分组切换刷新选择器并保存。未运行真实 AppContext 生命周期、托盘或桌面功能，因此不宣称完整应用端到端验证。
- 合成文件仅存在于本任务临时目录；保存／重载也仅使用临时配置。对各原文件核对 SHA-256、最后写入时间、名称和路径，同时检查文件夹／嵌套文件仍存在、没有额外重命名副本。没有访问真实桌面文件或操作正式便携实例。

## 实际结果

| 检查 | 结果 | 核验内容 |
| --- | --- | --- |
| Overlay 单入口跨组移动 | 通过 | 大小写分组 ID、非活动目标组重新布局、`ItemMoved → ItemsChanged` 各一次；同组／无效目标／null 不产生事件 |
| 设置列表单入口跨组移动 | 通过 | 右击未选中行替换旧选择；菜单排除当前分组；源列表移除、目标列表出现；设置事件一次 |
| 设置列表批量移动 | 通过 | 右击已选中行保留多选；仅移动指定文件与文件夹；未选中入口保留；全局顺序、标签不变；设置事件一次 |
| 搜索结果批量移动 | 通过 | 保留查询；隐藏的源入口不受影响；切组后显示匹配目标入口 |
| 显示名称保存 | 通过 | 初始化为当前显示名、修剪空白、接受含 `/` 的显示文字、列表／搜索立即更新、XML 重载保留；原始路径与文件名不变 |
| 名称取消与无效选择 | 通过 | Cancel、空字符串、纯空白均不变更或通知；零选／多选不构造对话框 |
| 删除中间分组 | 通过 | 全部入口迁往第一个保留分组；选择器和 overlay 同步；列表刷新；目标布局无重叠且完整容纳入口 |
| 删除首组／空组／最后一组保护 | 通过 | 首组入口全部迁移；空组可删除；最后一组不可删且不产生入口事件；正常保存重载保留全部入口 |
| 删除首组后的兼容 Group 字段 | **失败** | 内存 `Settings.Group` 指向已删组；原始 XML `<Group>` 仍序列化该组；`SettingsStore.Load` 归一化后可恢复别名 |

原文件校验还检查入口对象身份、全局排列顺序、`DesktopItem.Name`、`Path` 及每个 `GroupId` 均归属现存分组。全部主要操作路径都通过这些检查。

## 缺陷复现与最小建议

**[P3] 删除首分组时没有同步兼容字段 `AppSettings.Group`。**

1. 建立 `Source`、`Target`、`Third` 三组，`Settings.Group` 指向 `Source`，当前分组为 `Source`，其中有入口。
2. 经设置页“删除分组”事件调用 `OverlayForm.RemoveActiveGroup()`。
3. 观察 `Groups` 已移除 `Source`，入口和当前分组正确迁往 `Target`，但 `Settings.Group` 仍为 `Source`。
4. 执行真实 `SettingsStore.Save`，直接用 XmlSerializer 读取原始配置，不调用 `Normalize`：`<Group>` 仍为 `Source`，而 `<Groups>` 只剩 `Target`、`Third`。
5. 正常 `SettingsStore.Load` 会在 `DesktopItem.cs:84` 将 `Group` 改回 `Groups[0]`，所以仅做正常重载测试会漏掉此不一致。

位置：`OverlayForm.cs:300` 删除集合成员后，只更新了 `_selectedGroupId` 和 `ActiveGroupId`；`DesktopItem.cs:190` 验证的是重新反序列化后的副本，不会归一化内存原对象或重写已序列化内容。

最小修复建议：在 `Settings.Groups.Remove(removed);` 后、发出保存事件前增加 `Settings.Group = Settings.Groups[0];`。前置的“不能删除最后一组”保护使该索引有效，并符合 `Normalize()` 的现有约定。应由统一任务修改生产源码后重新运行本脚本，预期 9/9。

影响范围：本次证实的是内存／持久化模型的不一致；没有观察到正常入口丢失、活动分组错误或重启后删除组重新出现在分组列表。不应将其报告为已确认的数据丢失问题。

## 执行证据与清理

首轮 7/8；将首组删除的功能检查与兼容字段检查分开后，最终输出如下：

```text
PASS: overlay single-entry move, exact event order, inactive destination layout and no-op guards
PASS: settings context move replaces selection on an unselected row and refreshes both groups
PASS: settings batch selection moves exactly selected entries once and retains order/labels
PASS: filtered settings batch move preserves query and excludes hidden source entries
PASS: display-name save trims text, updates search and persists without renaming originals
PASS: display-name cancel/blank and zero/multiple selection never mutate or notify
PASS: delete active middle group migrates entries to first remaining group and syncs controls
PASS: delete first/empty groups retains every entry; last-group deletion is blocked
FAIL: deleted first group must not remain in the legacy Group field or saved XML :: RemoveActiveGroup removed Source from Groups but retained Source in Settings.Group and <Group>; entry membership, active selection and normalized reload still pass
RESULT: 8/9 checks passed; presentation suppressed; overlay HWND never created
```

脚本正常以失败状态结束，未把已知缺陷伪装为通过。其 `finally` 在两轮均清理整个独立目录；随后 `Test-Path` 两项均为 False：

- `C:\Users\user\AppData\Local\Temp\DeskDecor-entry-group-c59756c2b34c4a5a9c58f064ba8cbde5`
- `C:\Users\user\AppData\Local\Temp\DeskDecor-entry-group-ee08df79129d4de685ab44aa1e2e0338`

最终运行源码 SHA-256：

```text
SettingsForm.cs  65FA2B1946DEFB63129480913ED4B6FBAFCC341D191C58F6CA86BF4EB0B6E361
OverlayForm.cs   1A51C515335CAB22B9AB83D877EEEDB3C47D5B079CFB6D3A1879C23D87C22524
AppContext.cs    84DD6042A6B4722FE035E429AF1C884EAC484C739C2548600A7A3E3753783BA4
DesktopItem.cs   A06553D74C913E62E0AB08D208BE6F85C8F067B88A04F20225DC17E90071462D
DesktopEntries.cs 47EC098DE25164C1BABEB06D9350D4398100E49E90AB73B64DE3DC06890BF771
```

仅新增本任务约定的三个文件，未调用共享 `tests/run.ps1` 或 `build.ps1`，未提交、推送、重置或操作任何运行中的 DeskDecor 实例。

## 汇总推送前复查

在生产 SettingsForm 合入已实机验证的菜单生命周期补丁后，统一任务重新执行本脚本，仍为 8/9。移动与显示名称相关检查保持通过，唯一失败仍是删除首分组时的兼容字段。删除实现、设置事件和按钮将继续留在本地，不进入本次候选发布；本脚本及本记录也暂不提交。此处结论不覆盖真实删除按钮的完整实机操作。
