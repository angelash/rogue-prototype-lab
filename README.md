# 系列游戏原型开发

本目录是本轮系列游戏原型开发的总目录。

- 本机路径：`F:\workspace\rogue-prototype-lab`
- 计划中的 Codex 项目显示名：**系列游戏原型开发**
- 设置日期：2026-10-09
- 当前状态：《解析小丑牌设计》2026-10-09 最终修订结论已整理为中文 Markdown；本地工作簿已归档，现有内容由 Git/GitHub 管理。Codex 项目绑定仍待确认。
- 工作范围：目录设置、资料归档与用户另行授权的仓库备份；没有实施游戏原型。
- 仓库：[angelash/rogue-prototype-lab](https://github.com/angelash/rogue-prototype-lab)（公开）；主分支 `main`，远程名 `origin`。
- 日常上传、校验和恢复步骤见 [仓库与备份管理](docs/version-control.md)。

## 先读这些文档

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

全部候选仍是纸面假设，尚未实测。用户尚未选定具体立项，31 案不代表同时开发。平台、商业模式和预算未指定。

## 目录结构

| 目录 | 用途 |
| --- | --- |
| [docs/](docs/README.md) | 开发可用的方案、决策和试玩记录 |
| docs/design/ | 本次核实后的设计归档 |
| docs/decisions/ | 用户决策与助手建议 |
| docs/playtests/ | 后续原型试玩与验证记录 |
| [sources/](sources/README.md) | 原始资料、来源和版本信息 |
| sources/chatgpt/ | 《解析小丑牌设计》的对话与工作簿来源 |
| [prototypes/](prototypes/README.md) | 各游戏原型独立目录，目前未创建原型 |
| [assets/](assets/README.md) | 后续素材；shared/ 用于共用素材 |

本地工作簿 [熟悉与意外_120个游戏方案.xlsx](sources/chatgpt/熟悉与意外_120个游戏方案.xlsx) 已保留原文件名与内容并归档。已核对文件大小、XLSX 包结构、四个工作表名称和 SHA-256，未与云端逐字节比对，也未重新复审单元格内容。正文归档完整覆盖已提供的核实纲要；全部旧 120 案逐项理由与旧字段仍需查工作簿。
