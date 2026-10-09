# 仓库与备份管理

设置日期：2026-10-09（Asia/Shanghai）。

远程仓库：[angelash/rogue-prototype-lab](https://github.com/angelash/rogue-prototype-lab)，由用户创建；当前可见性为公开。远程名为 `origin`，主分支为 `main`，本地主分支跟踪 `origin/main`。

## 本次归档

- 初始快照提交：`993e3f6da67a8a9fb91c44904fb4c5563e18c63b`。原有 17 个文件逐字节保存，包含原来位于 `docs/` 的工作簿；后续整理可从此版本追溯。
- 原始工作簿现存于 [sources/chatgpt/熟悉与意外_120个游戏方案.xlsx](../sources/chatgpt/熟悉与意外_120个游戏方案.xlsx)。保留原文件名与内容，大小为 149077 字节。
- [来源登记](../sources/chatgpt/source-register.md) 保留云端来源、版本日期、本机检查结果及原位置；[checksums.sha256](../sources/chatgpt/checksums.sha256) 保存本地工作簿的 SHA-256。
- `.gitignore` 排除系统与编辑器文件、Office 锁文件、凭据、本机配置、依赖、缓存和构建结果。设计文档、原始资料、Excel、源素材、源代码以及依赖锁文件应当提交。
- `.gitattributes` 将后续更新的文本统一为 LF；工作簿、文档和媒体按二进制存储。当前工作簿可直接用 Git 备份，无需额外软件。
- `assets/shared/` 和 `docs/playtests/` 使用 `.gitkeep` 保留当前空目录，确保克隆后能还原预留结构。

本地 `.git/config` 已设置：仅允许快进拉取、清理失效的远程分支引用、默认推送当前同名分支、直接显示中文路径；配置只作用于此仓库。

2026-10-09 技术方案准备阶段先核对 Unity 缓存与构建的忽略规则，后续已实际建立 013 工程、五件 Blender 制作源/FBX/预览、七个短音效及中文字体。源码、正式 Assets、必要 ProjectSettings、Packages 锁文件、`.meta`、许可与事实记录已随 `d086cfc` 提交并推送；构建和完整工具日志留在忽略目录，证据摘要见 [开发记录](development/proto-013/2026-10-09-p0.md)。

Blender `.blend`、`.fbx`、`.tga/.exr`、音频、图片与字体按二进制保存，自动 `.blend1/.blend2`、Unity IDE 生成工程及缓存/构建排除。Unity 自动 YAML 的空值尾随空格由文件属性允许，保持 `.meta` GUID；大体量资产是否采用 LFS 按实际文件和远程限制评估，不能忽略正式制作源代替备份。

后续各项目默认执行根 [共同开发规范](../DEVELOPMENT_STANDARD.md)，项目根 `DEVELOPMENT.md` 保存落地与证据入口。已有 Git 备份授权持续适用；按任务完成必要定向检查、核对暂存内容、提交推送并确认远程同步，不把本机开发包已生成写成它已经上传。

后续能力审计的探针模型、音视频、原始日志与回归样本仅在忽略的 `.local/readiness-2026-10-09/`，本仓保存其 [规格与证据登记](../sources/references/development-capability-audit-2026-10-09.md)。正式制作时提交源文件和发行来源记录，不能把探针目录整批加入仓库。已有 [准备检查脚本](../scripts/README.md) 可校验文档与登记源字节；它不证明游戏构建或发行通过。

## 日常上传

在 `F:\workspace\rogue-prototype-lab` 中操作。开始编辑前先确认工作区干净，再同步远程：

```powershell
git status --short --branch
git pull --ff-only
```

编辑后先检查新增、修改和删除的文件，再提交并上传：

```powershell
git status --short
git diff
git add --all
git diff --cached --stat
git diff --cached
git commit -m "更新设计资料"
git push
git status --short --branch
```

提交说明应写明实际变更。`git add --all` 也会记录删除，提交前需要核对。确认推送成功，且本地主分支与 `origin/main` 同步，才算本轮远程备份完成。忽略规则内的文件不会上传；如需保存试玩日志或可复用配置，应放到明确的归档目录，并先检查内容。

若快进拉取失败，先检查 `git log --oneline --graph --all`，处理分支分歧后再推送，不用强制推送覆盖远程历史。项目授权以 [AGENTS.md](../AGENTS.md) 和用户的新指令为准。

## 原始资料与恢复

新增原始资料放在 `sources/`，同时记录来源链接、原文件名、版本日期和筛选理由。遇到同名的新版本，先核对内容，并放入有版本日期的子目录；保留旧版本。

核对本地工作簿：

```powershell
Get-FileHash -LiteralPath 'sources/chatgpt/熟悉与意外_120个游戏方案.xlsx' -Algorithm SHA256
```

预期校验值：`bc2f22bd25c6aaf2b186df46497a0441db18b90f2489a92eeb19307ca874ad23`。此值用于识别本次归档的字节内容，尚未与云端文件逐字节比对。

换电脑或恢复整个项目时，克隆到一个不存在的新目录：

```powershell
git clone https://github.com/angelash/rogue-prototype-lab.git F:\workspace\rogue-prototype-lab-restored
```

可用 `git log --oneline --all` 找到历史版本。恢复单个文件前先备份当前文件，再使用 `git restore --source=<提交号> -- <仓库内路径>`；初始快照中的工作簿路径为 `docs/熟悉与意外_120个游戏方案.xlsx`。
