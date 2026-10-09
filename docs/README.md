# 开发文档

设计归档从 [design/README.md](design/README.md) 开始。

全项目制作准备、制作要求、验证与备份默认按根 [DEVELOPMENT_STANDARD.md](../DEVELOPMENT_STANDARD.md) 执行；专属开发合同与当前落地在各 `prototypes/<项目>/DEVELOPMENT.md`，入口见 [项目总览](../prototypes/README.md)。新项目按 [模板](../DEVELOPMENT_TEMPLATE.md) 建立合同，通用规则不在每个项目重复复制。

本轮最新入口：[第一批项目需求与设计](design/first-batch/README.md)。六个项目都有独立文档目录，分别保存 requirements.md 和 design.md；prototypes/ 中各项目 README 链接对应正文。

013 后续补充：[套圈改造摊完整方案 v0.6](design/first-batch/proto-013-ring-toss/full-game/README.md)。独立保留十四个专题与总入口，原型 v0.1 保留为历史最小范围；用户随后明确真实三维斜视地摊。已有独立 Unity 工程、原创模型与短音效、48 项测试和 Windows 开发构建，实际范围见 [开发记录](development/proto-013/2026-10-09-p0.md)；游戏相机渲染已目检，三维正常窗口输入及完整正式内容尚未验收。其余五项仍为文档准备阶段。

当前不足与后续产物见 [12 缺口与补齐计划](design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md)；正式配置、覆盖、可达证据、文本和故障数据按 [13 内容与验证合同](design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md) 生产。已有 [准备检查脚本](../scripts/README.md) 和 [实际能力探针登记](../sources/references/development-capability-audit-2026-10-09.md)，工具通过与游戏通过分别记录。

执行入口见 [08 Unity PC](design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)、[09 资产生产](design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md)、[10 流程与技能](design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md)、[11 Steam](design/first-batch/proto-013-ring-toss/full-game/11-steam-release-plan.md)。只读参考快照见 [来源登记](../sources/references/local-project-reference-register.md)。

| 位置 | 用途 |
| --- | --- |
| design/ | 2026-10-09 最终修订结论、31 个候选、验证计划和版本记录 |
| [decisions/decision-log.md](decisions/decision-log.md) | 用户要求、助手建议及待确认事项 |
| playtests/ | 后续原型验证与试玩记录，目前尚无实测数据 |
| [setup-status.md](setup-status.md) | 总目录和 Codex 项目绑定状态 |
| [version-control.md](version-control.md) | Git/GitHub 备份、忽略规则、校验及恢复步骤 |

本版包含“构建系统而非只构建目标”的再次分析，区分开发者创作系统、游戏规则系统、玩家阶段目标与原型验证目标。

来源见 [sources/chatgpt/source-index.md](../sources/chatgpt/source-index.md)。本地同名工作簿已归档到 [sources/chatgpt/熟悉与意外_120个游戏方案.xlsx](../sources/chatgpt/熟悉与意外_120个游戏方案.xlsx)，检查范围见来源登记。
