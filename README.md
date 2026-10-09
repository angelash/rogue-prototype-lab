# 系列游戏原型开发

本目录是本轮系列游戏原型开发的总目录。

- 本机路径：`F:\workspace\rogue-prototype-lab`
- 计划中的 Codex 项目显示名：**系列游戏原型开发**
- 设置日期：2026-10-09
- 当前状态（2026-10-10）：用户已要求其余五项逐个开发与单独提交，当前进度见[逐项开发记录](docs/development/first-batch-progress.md)。032已建立独立Unity首次可玩候选与30项测试，原生窗口操作暂受系统权限提示阻挡；013保留既有三维P0。第一批原需求与设计v0.1、013完整方案v0.6均保留；原工作簿、历史结论、内容/能力缺口及生产验证合同保留，内容由 Git/GitHub 管理。Codex 项目绑定仍待确认。
- 当前完成范围：资料与设计、仓库备份、能力审计、隔离工具探针，以及 013 的首个三维单摊、初始 Blender 模型 Resources 接入与 48/48 EditMode 测试通过（旧 30 项＋新三维 18 项）。Windows x64 Mono 构建已完成，隔离相机渲染图已生成并目检；版本、大小、图与日志见 [本轮开发记录](docs/development/proto-013/2026-10-09-p0.md)。该相机图不含 IMGUI 覆盖层。旧二维窗口 QA 保留为历史；该轮曾观察到锁屏，三维正常键鼠 QA 尚未通过，隔离渲染不代替真实操作验收。用户已明确后续开发全部由助手处理。
- 仓库：[angelash/rogue-prototype-lab](https://github.com/angelash/rogue-prototype-lab)（公开）；主分支 `main`，远程名 `origin`。
- 日常上传、校验和恢复步骤见 [仓库与备份管理](docs/version-control.md)。

## 先读这些文档

后续所有项目默认执行根目录 [制作准备与开发规范](DEVELOPMENT_STANDARD.md)，开始或继续开发前再读目标项目根目录的 `DEVELOPMENT.md`。新项目用 [开发要求模板](DEVELOPMENT_TEMPLATE.md) 建立专属合同；默认规则已写入 [AGENTS.md](AGENTS.md) 和 `.cursor/rules/project-development-standard.mdc`。

第一批的项目开发要求与真实落地快照：

- [013 套圈改造摊](prototypes/proto-013-ring-toss/DEVELOPMENT.md)
- [027 贪吃蛇孵化场](prototypes/proto-027-snake-hatchery/DEVELOPMENT.md)
- [032 回转寿司工坊](prototypes/proto-032-sushi-workshop/DEVELOPMENT.md)
- [051 回收保洁队](prototypes/proto-051-recycling-cleaners/DEVELOPMENT.md)
- [121 磁铁拾荒者](prototypes/proto-121-magnet-scavenger/DEVELOPMENT.md)
- [126 收割机自己铺路](prototypes/proto-126-harvester-paths/DEVELOPMENT.md)

最新入口：[第一批项目需求与设计总览](docs/design/first-batch/README.md)，包含回转寿司工坊、套圈改造摊、回收保洁队、贪吃蛇孵化场、磁铁拾荒者、收割机自己铺路。

方案入口：[套圈改造摊完整方案 v0.6](docs/design/first-batch/proto-013-ring-toss/full-game/README.md)，覆盖完整设计、Unity PC/Steam，以及当前缺口和内容生产验证。用户已选择 Unity、现有安装、PC 单机与 Steam 目标，并于 2026-10-09 授权按计划开始实现、素材接入、测试和 Windows 构建；本机核实为 2022.3.62f3c1。随后明确要求真实三维、斜视场景、玩家近处向前抛圈，替代旧二维占位与固定正交 2.5D；具体镜头、布局、造型与材质仍是助手建议，见 [三维方向合同](docs/development/proto-013/3d-scene-direction.md)。

当前实现入口：[013 项目](prototypes/proto-013-ring-toss/README.md) · [P0 Unity 工程与操作说明](prototypes/proto-013-ring-toss/game/RingTossWorkshop/README.md) · [2026-10-09 首轮开发记录](docs/development/proto-013/2026-10-09-p0.md)。P0 仅含无风单摊、普通圈、地面两排三列六槽、风扇与反弹板，以及兑现/留场和两次调整；目标回款 105、基础八圈。首组已有三维摊景、近处持圈表现和 Blender 模型接入，三个输入分别控制左右方位、仰角与力度；已接七个原创程序音效。完整正式人物/皮肤/三章美术、存档、跨摊流程与 Steam 接入仍未完成。

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

第一批名单来自优先推荐与用户追加项，名单解释、用户明确决定与助手建议见总览。013 已获授权开始开发；其余首批项目已获逐项开发授权，实际阶段见[进度](docs/development/first-batch-progress.md)；玩法价值仍待各自原型和试玩验证，31案不代表同时开发。本次用户要求将已有制作准备与要求归档为后续项目默认规范；其它项目默认从现有 Unity、Windows PC 单机及面向 Steam 的制作基线起步，项目专属平台覆盖、视觉维度、商业模式与预算另行记录，不能把套圈镜头和规则自动照搬。

## 目录结构

| 目录 | 用途 |
| --- | --- |
| [docs/](docs/README.md) | 开发可用的方案、决策和试玩记录 |
| docs/design/ | 本次核实后的设计归档 |
| docs/decisions/ | 用户决策与助手建议 |
| docs/playtests/ | 后续原型试玩与验证记录 |
| [sources/](sources/README.md) | 原始资料、来源和版本信息 |
| sources/chatgpt/ | 《解析小丑牌设计》的对话与工作簿来源 |
| [prototypes/](prototypes/README.md) | 第一批各项目独立入口；013已有P0，其余按逐项进度推进 |
| [assets/](assets/README.md) | 原型素材与来源信息；shared/ 用于共用素材 |

本地工作簿 [熟悉与意外_120个游戏方案.xlsx](sources/chatgpt/熟悉与意外_120个游戏方案.xlsx) 保留原文件名与内容。已核对文件大小、包结构、四个工作表名称和 SHA-256，本轮只读核对了六个候选及原型优先级相关单元格，定位见 [筛选登记](sources/chatgpt/first-batch-selection.md)。未与云端逐字节比对，也未全面复审全部旧 120 案。

本轮只读参考 highschool、world-of-claudecraft 与 live-avatar；原工程的版本、校验与筛选理由见 [工程参考登记](sources/references/local-project-reference-register.md)。world-of-claudecraft 的 14 项音频尚未确认可商用，未复制进游戏，结论见 [音频复用登记](sources/references/woc-audio-reuse-register.md)；live-avatar 仅作 TTS 架构参考，见 [音频能力登记](sources/references/live-avatar-audio-capability-register.md)。仓库内的 [Unity PC 开发技能](.agents/skills/unity-pc-prototype/SKILL.md) 与 [套圈素材技能](.agents/skills/ring-toss-asset-pipeline/SKILL.md) 通过权威文档路由具体工作。
