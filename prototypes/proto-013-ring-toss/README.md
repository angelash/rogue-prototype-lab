# 013 套圈改造摊

第一批项目；原型文档 v0.1 保留，当前方向说明 v0.6，2026-10-09。已进入 Unity 实现与验证，正在按用户明确要求制作首个真实三维单摊场景。

核心问题：稳定投掷能否通过练习改善，兑现或留场是否随回款压力改变。

- [P0 Unity 工程、运行命令与操作说明](game/RingTossWorkshop/README.md)
- [三维场景方向与首组范围](../../docs/development/proto-013/3d-scene-direction.md)：用户明确方向、助手方案与新三维验证合同。
- [2026-10-09 首轮开发结果](../../docs/development/proto-013/2026-10-09-p0.md)：实现范围、实际验证证据与接续点。
- [需求文档](../../docs/design/first-batch/proto-013-ring-toss/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-013-ring-toss/design.md)
- [完整方案入口](../../docs/design/first-batch/proto-013-ring-toss/full-game/README.md)：完整设计、Unity PC、资产生产、Steam 与开发缺口。
- [Unity 技术方案](../../docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)
- [美术与资产生产](../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md)
- [开发流程与技能](../../docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md)
- [准备度与缺口](../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md)
- [内容生产与验证合同](../../docs/design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md)
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号 013。权威设计文档放在 docs/design/，独立 Unity 工程位于 `game/RingTossWorkshop/`。用户已选择 Unity、现有安装、PC 单机与 Steam 目标；核实编辑器为 2022.3.62f3c1，并已授权游戏实现、必要素材接入、测试和 Windows 构建。

用户随后明确要求真实三维、场景斜视角、玩家在镜头前向前抛圈，像地摊。旧二维占位与固定正交 2.5D 建议已被替换；此前二维实现与实际验证记录保留为历史，不能当作新三维验收。

首组助手方案为固定后上方透视相机、地面两排三列奖品、近处投掷手/圈、暖灯夜市、条纹棚、帆布摊地与轻覆盖 UI；无自由漫游。三维真值与视图共用锚点，左右瞄准、仰角和力度分别控制。当前只迭代无风单摊、普通圈、风扇和反弹板；目标回款 105、基础八圈、两次调整等经济/标量建议不变。具体相机、几何、材质与布局参数仍是助手建议，实际完成范围和验证结果见开发记录。

原型已有七个原创程序短音；world-of-claudecraft 的 14 项音频因商用授权未确认而未复制，live-avatar 仅作为 TTS 架构参考。完整正式皮肤/人物与场景、剧情与十二摊、存档、跨摊流程、其他圈/机关和 Steam 接入仍未完成。首组单摊不代表完整游戏已完成。

用户已明确后续开发全部由助手处理。后续内容、素材、测试与构建沿缺口计划和内容验证合同连续推进；基础环境检查见 [scripts](../../scripts/README.md)，P0 的 Prepare/Test/Build 入口和控制方式见工程 README。
