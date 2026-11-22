# 本地 ZIP 解压安全标记验证（2026-11-22）

## 根因

原 `dist/DeskDecor-portable-x64.zip` 没有 `Zone.Identifier` 数据流，但带继承的 Low Mandatory Level。用户通过资源管理器解压后，EXE 出现 `ZoneId=3`，其 `ReferrerUrl` 指向该本地 ZIP。之前仅检查 ZIP 下载标记、清除构建 EXE 标记的验证不足。

## 对照验证

通过 Windows `Shell.Application` 压缩文件夹接口，将内容相同的两份 ZIP 解压到各自全新的 Medium integrity 目录，没有运行解压出的程序：

| ZIP | ZIP 的下载标记 | 解压 EXE 的下载标记 | 解压 EXE 的完整性 |
| --- | --- | --- | --- |
| 原始 Low integrity ZIP | 无 | `ZoneId=3` | Medium |
| 同内容 Medium integrity ZIP | 无 | 无 | Medium |

两个解压 EXE 的 SHA-256 均为 `BF823548EA8D4667BBD3618F9CC82E9953AD7D59326D7EF1EED0A2674AF64D7B`，与构建产物一致。因此本轮可复现的警告来自低完整性归档在 Windows 解压过程中的标记处理。

## 修复

`package.ps1` 在创建暂存目录前将 `dist` 设为可继承的 Medium integrity；发布两个最终 ZIP 时检查链接目标、解除阻止并显式设置 Medium integrity。继续修正本地便携目录及 EXE 的完整性，保留已有个人文件。

已实际运行修复后的打包脚本并验证：两个最终 ZIP 与直接运行的便携 EXE 均为 Medium，且没有 `Zone.Identifier`。便携包恰好含六个分发文件，逐项 SHA-256 与当前产物一致；再次通过 Windows 压缩文件夹接口解压到新目录，EXE 为 Medium、没有下载标记，内容与便携 EXE 一致。PowerShell 语法检查、`git diff --check` 与独立只读脚本审查通过。

桌面上的同名 ZIP 已更新为此包；确认桌面已有 EXE 与构建产物哈希相同后，清除了它的下载标记，并将桌面便携目录、ZIP 和 EXE 设置为 Medium。没有启动应用程序。

## 覆盖限制

资源管理器自动化输入未生效，本轮使用 Windows 自带的压缩文件夹 COM 接口验证实际解压行为。未直接点击启动对话框，也未验证其他电脑。外部下载或传输软件仍可能重新添加互联网标记，程序没有可信数字签名；此修复针对本地打包生成的低完整性 ZIP。
