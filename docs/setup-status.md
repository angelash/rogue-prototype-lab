# 本地项目设置状态

初始设置与核验日期：2026-10-09（UTC）。
仓库备份核验日期：2026-10-09（Asia/Shanghai）。

| 项目 | 状态 |
| --- | --- |
| 总目录 | 已创建：F:\workspace\rogue-prototype-lab |
| 同名目录检查 | 创建前不存在，没有覆盖已有目录 |
| 同名 Codex 项目检查 | 创建前及完成时的项目列表均未发现“系列游戏原型开发”或目标路径 |
| 目录约定检查 | 未找到 F:\AGENTS.md、F:\workspace\AGENTS.md、F:\workspace\AGENTS.override.md 或目标目录的已有 AGENTS.md |
| 基础结构 | docs/、sources/、prototypes/、assets/ 及对应子目录已创建 |
| Markdown 归档 | 已完成：设计原则、31 候选、首批验证计划、版本调整、研发方法及来源索引 |
| Codex 项目绑定 | **尚未完成** |
| Codex 项目 ID | 尚无 |
| 本地工作簿 | 已从 docs/ 中发现的同名文件归档到 sources/chatgpt/；149077 字节，已检查包结构、四个工作表名称和 SHA-256 |
| Git 仓库 | 已初始化，主分支 main，远程 origin 指向用户创建的 GitHub 仓库 |
| 忽略规则与文件属性 | 已配置 .gitignore 和 .gitattributes，见仓库与备份管理 |
| 第一批项目文档 | 已整理 6 个独立项目入口和 12 份需求/设计 v0.1，见第一批汇总；未实现或实测 |
| 013 完整方案 | 已补至 v0.3，总入口及十二个专题，覆盖完整设计、Unity PC、资产生产、开发技能与 Steam；只有数学/文档核对，没有可玩构建或试玩 |
| 013 现有软件核查 | Unity 2022.3.62f3c1 与 Windows x64 Mono 模块、Blender 5.2.2 LTS 已核实；未启动 Unity、验证当前许可证或创建工程 |
| 仓库开发技能 | 新增 unity-pc-prototype、ring-toss-asset-pipeline，两项格式校验通过；未执行真实游戏或素材生产任务 |
| 外部工程与资产参考 | highschool、world-of-claudecraft 只读，版本及来源已登记；候选开源素材未导入，未改外部文件 |
| v0.3 文档检查 | 53 份 Markdown 严格 UTF-8/非空检查通过，389 个本地链接有效，两项技能格式通过；工作簿 SHA-256 不变，未代替游戏验证 |

初次归档时，全部新建 Markdown 已检查为非空、可严格 UTF-8 读取；本机目录与文件清单已经核对，当时未成功下载原工作簿。后续仓库备份已发现本地同名文件并记录校验值；未重新复审单元格内容，也未与云端逐字节比对。历史状态由首次 Git 快照保留。

本轮第一批项目整理另外只读核对了六个候选及原型优先级相关单元格，定位见 [筛选登记](../sources/chatgpt/first-batch-selection.md)。原文件内容及 SHA-256 不变；不代表已全面复审旧方案。各项目文档入口见 [第一批汇总](design/first-batch/README.md)。

## 完成项目绑定的用户步骤

1. 在桌面应用的 Projects（项目）视图创建或添加一个本地项目。
2. 将项目命名为“系列游戏原型开发”。
3. 选择已存在的目录 F:\workspace\rogue-prototype-lab；如需补充目录，在项目菜单选择 Edit project（编辑项目），再选择 Add folder（添加文件夹）。
4. 如项目含有多个目录，将该目录设置为 Make primary（主目录）。
5. 检查项目列表显示的名称和路径，再记录真实项目 ID。

官方说明：[Projects and chats](https://learn.chatgpt.com/docs/projects)。

当前工具提供项目查询和任务创建，未提供创建本地项目的操作。新建任务不能视为项目绑定。已读取的 Computer Use 技能规范禁止自动操作 ChatGPT 桌面应用 UI，因此没有执行该绑定入口，也没有修改 Codex 数据库或未公开配置。

## 尚需完成

- 按上述步骤绑定 Codex 本地项目并记录真实 ID。
- 如需确认本地文件与 Library 原文件完全一致，另行取得云端文件字节或校验值后比对；当前本机文件记录见 [来源登记](../sources/chatgpt/source-register.md)。

## 本次范围

初始阶段执行目录设置和中文文字归档。2026-10-09 用户另行提供已创建的 GitHub 仓库，并授权本地 Git 设置、忽略规则和现有内容的提交与推送；现按此授权管理备份。没有实现游戏或安装软件，也没有代用户创建远程仓库。

后续本轮按用户要求整理第一批项目目录、独立需求与设计并提交。名单解释、明确追加项和助手顺序建议分开记录，尚未开始游戏实现。

用户又要求先对套圈改造摊做全面设计补充，并追加 Unity/现有版本/PC 单机/Steam 与资产能力要求，已独立归档 [完整方案 v0.3](design/first-batch/proto-013-ring-toss/full-game/README.md)。原型 v0.1 和原资料保留；新增数值、题材及具体美术路线为助手建议。本轮补技术、资产、开发与发行文档以及两项技能，没有执行游戏实现、软件安装、素材生产或发行。

上传、校验和恢复步骤见 [仓库与备份管理](version-control.md)。
