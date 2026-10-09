---
name: unity-pc-prototype
description: 为本仓 Unity PC 单机原型拆分任务、实现已授权行为并执行定向 QA；适用于套圈改造摊开发，不用于 Web、MMO 或服务部署。
---

# Unity PC 原型开发

这是本仓原创工作流适配，方法来源及任务模板见 [开发流程](../../../docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md)。

## 读取与路由

先读仓库 `AGENTS.md`、目标原型现有文件及工作区变更，确认这次要求是设计、只读审阅还是实现；沿用户当前授权完成必要工作，不因阶段切换额外要求确认。

- 环境、工程组织和模拟边界：读 [08 Unity 技术方案](../../../docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)。工具路径、引擎版本和已安装模块以该文档的实读证据及当前机器为准，不自行升级或照抄其他项目设置。
- 规则或数值：按任务读 [02 玩法](../../../docs/design/first-batch/proto-013-ring-toss/full-game/02-gameplay-systems.md)、[04 参数](../../../docs/design/first-batch/proto-013-ring-toss/full-game/04-economy-and-balance.md) 与 [06 存档合同](../../../docs/design/first-batch/proto-013-ring-toss/full-game/06-technical-save-and-accessibility.md)。不在技能中另建参数副本。
- 素材变化：交给 [素材技能](../ring-toss-asset-pipeline/SKILL.md)，按 09 路由 Blender 三维低模旧物、二维人物/背景、UI 和音频，只读取这次涉及的资产规格。固定二维规则与三维表现分别核验，不因模型导入改变判定。
- Steam 构建或发行准备：读 [11 Steam 计划](../../../docs/design/first-batch/proto-013-ring-toss/full-game/11-steam-release-plan.md)。以真实接口和授权执行，不假定已接入 SDK、取得 App ID 或具备发布权限。
- 开工现状与缺口：读 [12 准备度](../../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md) 和 [准备检查入口](../../../scripts/README.md)。先复核实际环境，区分文件存在、工具探针、Unity 编译和 Player 实测；独立 .NET 检查不能代替 Unity 兼容验证。
- 关卡、剧情或验证数据：按任务读 [13 内容与验证合同](../../../docs/design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md)。正式配置、预览、回放与可达证据共用 Core；统计合法解锁/预装覆盖，不用抽样冒充全量，不静默删掉合法难例。

## 开发方式

采用 [10](../../../docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md) 的任务包：目标、现状、输入与输出、状态所有者、范围、验收、证据和接续点。小任务直接完成；跨系统工作拆成可玩的纵切，不先搭通用框架。

用户已明确后续开发由助手处理。程序、内容、正式素材、测试和构建由助手完成；真实身份/签署/付款、尚未取得的设备/玩家和平台权限按实际条件记录。首个 Player 必须验证真实窗口输入与画面，自动场景驱动只能辅助复现，不能代替所有 UI 路径。

投掷、命中、机关消耗、现金事务和局流程使用同一权威规则层。纯判断与数学不依赖画面帧率、角色动画、Unity 场景对象或 Steam 回调；输入和表现通过薄适配接入。修复真实行为缺陷时，先通过生产路径复现，再增加能区分正确与错误结果的回归检查。仅改文案或装饰时，采用链接、布局或视觉核验，不镜像实现堆测试。

根据已存在工程选择可运行的验证方式。规则变更做定向逻辑测试；场景、输入和资源接入做 Unity 集成核验；候选交付做实际桌面构建检查。命令、测试程序集及构建脚本必须已存在且对应已核对版本，不能把建议中的接口写成运行成功。协调者统一执行昂贵验证，审阅者复用同一结果。

集成上游或并行修改时，核对规则、配置、事件和存档的语义结果，检查相同逻辑是否出现两份。不因文本合并干净或测试退出码为零就跳过实际路径核对。

## 交付

报告改变的行为、权威参数与配置版本、实际运行的验证及结果、未执行原因和明确的接续点。更新与本次实现直接相关的事实记录，不把设计建议改写成用户决定或实测结论。执行提交、推送、安装或发布时沿现有授权判断范围；技能本身不增加这些授权，也不覆盖用户的新指令。
