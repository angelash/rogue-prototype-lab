# 开发准备检查脚本

版本：v0.4；日期：2026-10-09。这些脚本可实际运行，用于现状检查；不创建 Unity 工程、不安装软件、不查询账户或启动编辑器。

- [check-development-readiness.ps1](check-development-readiness.ps1)：核对已配置的 Unity/Mono、Blender 文件、命令路径、计划工程是否存在及基础主机信息。文件存在与编译/导入/运行通过分别报告。可通过 `-UnityEditorPath`、`-BlenderPath` 覆盖机器路径；相对 `-OutputPath` 按仓库根解析，报告可写到受忽略的 `.local/`。
- [check-docs.py](check-docs.py)：Python 标准库检查中文 Markdown 的 UTF-8/非空、本地文件链接及来源登记 SHA-256。默认跳过仓外绝对引用，换电脑无需具备参考仓；`--check-external` 可核对本机所有来源路径。它不验证网页内容、Markdown 锚点、排版、游戏规则或玩法可达性。

在仓库根目录执行：

```powershell
& './scripts/check-development-readiness.ps1' -OutputPath '.local/readiness-inventory.json'
& 'C:\Python310\python.exe' -X utf8 './scripts/check-docs.py' --check-external
```

Python 路径按当前可用运行时选择，脚本无需 pip 安装。Unity 测试、内容编译、故障注入、Blender 正式资产生产、Windows 构建和发行打包脚本尚未建立，按 [缺口与推进计划](../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md) 的阶段补齐，不能把本目录的准备检查当成完整生产工具链。
