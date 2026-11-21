# 分组选择与搜索回归验证 — 2026-11-21

## 验证结果

通过。运行 `tests/run-group-search.ps1` 后，独立编译并执行了 `GroupSearchRegression`，三项断言组全部通过：

- 搜索当前分组中的自定义显示别名、无别名条目的文件名和完整路径；大小写不敏感，首尾空白会被忽略。
- 空白查询显示当前组全部条目；无匹配查询显示空列表；切换分组会保留查询并只显示目标组匹配项。
- SettingsForm 选择分组会同步共享的活动组并触发一次 OverlayForm 变更；OverlayForm 发起的分组变更会刷新 SettingsForm 的选择器和列表。刷新分组标题、重复刷新和重复选择不会递归通知。

同时确认筛选和分组选择不修改入口数据、不触发 `SettingsChanged`，测试结束前两个真实窗体都保持隐藏且未创建窗口句柄。

## 测试方法与边界

测试直接实例化真实 `SettingsForm`、`OverlayForm` 及其控件。事件连接按 `AppContext.ShowSettings` 中的 `GroupChanged → OverlayForm.SetActiveGroup` 和 `ActiveGroupChanged → SettingsForm.RefreshItems/RefreshGroups` 生产连接执行。未实例化完整 `AppContext`，以避免其显示桌面覆盖层、创建托盘图标及启动定时器；因此本次覆盖控件和生产事件链行为，不覆盖配置保存或可见 UI 渲染。

脚本只读编译项目根目录的 C# 源码，不调用共享 `tests/run.ps1` 或 `build.ps1`。可执行文件和 PowerShell transcript 日志位于每次运行生成的 `%TEMP%\DeskDecor-GroupSearch-<GUID>` 目录，脚本在成功或失败退出时清理该目录。本次运行退出码为 0，临时目录已清理。

未发现生产缺陷；生产源码未修改。测试新增文件为 `tests/GroupSearchRegression.cs`、`tests/run-group-search.ps1` 和本记录。
