# 系列游戏原型开发

本目录是本轮系列游戏原型开发的总目录。

- 本机路径：`F:\workspace\rogue-prototype-lab`
- 计划中的 Codex 项目显示名：**系列游戏原型开发**
- 设置日期：2026-10-09
- 当前状态：第一批 6 个项目已有独立需求与设计 v0.1，013 套圈改造摊另完成完整游戏设计 v0.2；原工作簿及历史结论保留，内容由 Git/GitHub 管理。Codex 项目绑定仍待确认。
- 工作范围：目录设置、资料归档、仓库备份与游戏设计文档；尚未实现或实测游戏原型。
- 仓库：[angelash/rogue-prototype-lab](https://github.com/angelash/rogue-prototype-lab)（公开）；主分支 `main`，远程名 `origin`。
- 日常上传、校验和恢复步骤见 [仓库与备份管理](docs/version-control.md)。

## 先读这些文档

最新入口：[第一批项目需求与设计总览](docs/design/first-batch/README.md)，包含回转寿司工坊、套圈改造摊、回收保洁队、贪吃蛇孵化场、磁铁拾荒者、收割机自己铺路。

本轮专项补充：[套圈改造摊完整游戏设计 v0.2](docs/design/first-batch/proto-013-ring-toss/full-game/README.md)，覆盖世界与人物、三章十二摊、玩法和数值、音画交互、保存与制作验收。新增方案均标为助手建议；文档完善不表示已经实现或决定引擎。

| 文档 | 用途 |
| --- | --- |
| [最终修订方案入口](docs/design/README.md) | 核心结论、版本边界和阅读顺序 |
| [设计原则](docs/design/01-design-principles.md) | 四层系统与目标、十二条原则 |
| [31 个主候选](docs/design/02-candidate-overview.md) | 25 个修订旧候选＋6 个新增方向 |
| [首批原型验证计划](docs/design/03-prototype-validation-plan.md) | 助手建议的 32 → 13 → 51 验证顺序、最小范围与停止条件 |
| [版本调整](docs/design/04-version-changes.md) | 旧 120 案筛选统计与撤回的评价 |
| [研发方法](docs/design/05-development-method.md) | 如何用试验产生可复用认识 |
| [决策与建议](docs/decisions/decision-log.md) | 用户要求与助手建议的区别 |
| [来源索引](sources/chatgpt/source-index.md) | 对话消息、文章及原工作簿 |
| [本地设置状态](docs/setup-status.md) | 实际完成状态与项目绑定步骤 |
| [仓库与备份管理](docs/version-control.md) | 忽略规则、上传、工作簿校验及恢复步骤 |

全部候选仍是纸面假设，尚未实测。本轮把优先推荐与用户追加项整理为第一批；名单解释、用户明确决定与助手建议见总览。实际开工顺序未指定，31 案不代表同时开发。平台、商业模式和预算未指定。

## 目录结构

| 目录 | 用途 |
| --- | --- |
| [docs/](docs/README.md) | 开发可用的方案、决策和试玩记录 |
| docs/design/ | 本次核实后的设计归档 |
| docs/decisions/ | 用户决策与助手建议 |
| docs/playtests/ | 后续原型试玩与验证记录 |
| [sources/](sources/README.md) | 原始资料、来源和版本信息 |
| sources/chatgpt/ | 《解析小丑牌设计》的对话与工作簿来源 |
| [prototypes/](prototypes/README.md) | 第一批各项目的独立入口，目前为文档准备阶段 |
| [assets/](assets/README.md) | 后续素材；shared/ 用于共用素材 |

本地工作簿 [熟悉与意外_120个游戏方案.xlsx](sources/chatgpt/熟悉与意外_120个游戏方案.xlsx) 保留原文件名与内容。已核对文件大小、包结构、四个工作表名称和 SHA-256，本轮只读核对了六个候选及原型优先级相关单元格，定位见 [筛选登记](sources/chatgpt/first-batch-selection.md)。未与云端逐字节比对，也未全面复审全部旧 120 案。
