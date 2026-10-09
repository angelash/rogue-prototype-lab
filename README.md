# 系列游戏原型开发

本目录是本轮系列游戏原型开发的总目录。

- 本机路径：`F:\workspace\rogue-prototype-lab`
- 计划中的 Codex 项目显示名：**系列游戏原型开发**
- 设置日期：2026-10-09
- 当前状态：第一批 6 个项目已有独立需求与设计 v0.1，013 套圈改造摊完整方案入口已补至 v0.4，新增内容/能力缺口、生产验证合同和可运行准备检查；原工作簿及历史结论保留，内容由 Git/GitHub 管理。Codex 项目绑定仍待确认。
- 当前完成范围：资料与设计、仓库备份、能力审计及隔离工具探针；尚未实现或实测游戏原型。用户已明确后续开发全部由助手处理。
- 仓库：[angelash/rogue-prototype-lab](https://github.com/angelash/rogue-prototype-lab)（公开）；主分支 `main`，远程名 `origin`。
- 日常上传、校验和恢复步骤见 [仓库与备份管理](docs/version-control.md)。

## 先读这些文档

最新入口：[第一批项目需求与设计总览](docs/design/first-batch/README.md)，包含回转寿司工坊、套圈改造摊、回收保洁队、贪吃蛇孵化场、磁铁拾荒者、收割机自己铺路。

本轮专项补充：[套圈改造摊完整方案 v0.4](docs/design/first-batch/proto-013-ring-toss/full-game/README.md)，覆盖完整设计、Unity PC/Steam，以及当前缺口和内容生产验证。用户已选择 Unity、现有安装、PC 单机与 Steam 目标；本机核实为 2022.3.62f3c1。固定正交 2.5D 和 Blender/生图分工仍为助手建议，尚未创建游戏工程或生产正式素材。

后续开发先读 [12 缺口与补齐计划](docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md) 和 [13 内容与验证合同](docs/design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md)。程序、内容、音画、测试与构建由助手处理；16 项缺口按阶段、产物和完成证据登记。已有 [准备检查脚本](scripts/README.md)，Blender 与音视频编码的实际探针见 [能力审计](sources/references/development-capability-audit-2026-10-09.md)。

开发入口：[Unity PC 技术方案](docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md) · [美术与资产生产](docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md) · [开发流程与两项技能](docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md) · [Steam 发行](docs/design/first-batch/proto-013-ring-toss/full-game/11-steam-release-plan.md)。

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

全部候选仍是纸面假设，尚未实测。本轮把优先推荐与用户追加项整理为第一批；名单解释、用户明确决定与助手建议见总览。实际开工顺序未指定，31 案不代表同时开发。013 的引擎、PC 单机与 Steam 目标已指定；其他项目的技术/平台选择、各项目商业模式与预算另行记录。

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

本轮只读参考 highschool 与 world-of-claudecraft；原文件名、版本、校验与筛选理由见 [工程参考登记](sources/references/local-project-reference-register.md)。仓库内的 [Unity PC 开发技能](.agents/skills/unity-pc-prototype/SKILL.md) 与 [套圈素材技能](.agents/skills/ring-toss-asset-pipeline/SKILL.md) 通过权威文档路由具体工作。
