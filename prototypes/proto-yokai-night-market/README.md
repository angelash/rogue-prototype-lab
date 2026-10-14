# 妖怪夜市开张

当前 v2 使用 Unity 恢复 Crunch Time 的业务逻辑、场景/UI 和卡牌模板，只换夜市包装。完整工程、全部制作资产与测试用例纳入本仓 codex/yokai-night-market-source 本地分支；构建、缓存与运行结果忽略。

- [开发合同](DEVELOPMENT.md)：当前决定、范围和实际限制。
- [工程与资产版本管理](VERSION_CONTROL.md)：完整源码分支、资产/测试覆盖及历史恢复。
- [Steam发行准备](release/README.md)：2026-10-14新增流程、分工、来源门槛、中英文商店草稿、独立宣发候选与Preview模板。当前研究包未取得商业来源放行，不能直接送审。
- [实现入口](fidelity/README.md)：规则、恢复、素材与启动。
- [全量替换数据](fidelity/data/theme.json)：95 卡、28 委托、420 双语键。
- [内容查阅](fidelity/data/content-guide.md)：当前卡牌参数和委托目标。
- [原图尺寸与导入](fidelity/image-specs.json)：82 张游戏纹理。
- [UI 分层规格](fidelity/data/ui-surfaces.json) / [对应回归用例](fidelity/test-cases/ui-surfaces.md)：设置、左侧面板、阶段底图及功能标记。
- [v2.8 界面重整](fidelity/art-direction-v2.8/README.md) / [实际集成与检查](fidelity/art-direction-v2.8/integration-and-review.md)：38 张 UI 已替换为夜市纸面、旧金边与矿物色，44 张保留；原尺寸、透明轮廓、Sprite 几何、节点与点击区域锁定，当前运行包已更新。
- [实际提示词](fidelity/art/prompts/) / [生成原图](fidelity/art/generated/) / [来源谱系](fidelity/art/generation-register.json)。
- [静态验证](fidelity/evidence/fidelity-verification.json) / [交付证据](fidelity/evidence/delivery.json) / [验收说明](fidelity/04-validation-and-delivery.md)。

本机 Unity 工程 local/UnityProject/，使用 **Unity 2022.3.62f3c1**，打开 Assets/Recovery/Scenes/NightMarket.unity。完整 Windows 包 local/Builds/Release/，入口 YokaiNightMarket.exe。压缩备份和实际 hash 由交付记录登记；移动游戏时需带完整目录。

v2.3 已补入重开/设置保存的 15 项自动流程断言，并完成 13 项实际 Release 键鼠检查，记录进程重启持久化和岗位拦截。早期开发包防火墙阻挡属于历史；本轮复查时该窗口已不在，正式包正常启动。测试源见[流程与原生用例](fidelity/test-cases/flow-and-native.md)。

当前为本机恢复候选，完整原生操作、原速体验、听感与逐像素对照尚须补验。恢复实现、原音频及功能 stencil 派生 PNG 已进入完整本地 Git 分支；公开远程仅保留原创资料，完整分支使用 Git bundle 备份。

旧改编资料保留在 design/、data/、art/、replacement/；[旧 README](history/README-v1.md) / [旧合同](history/DEVELOPMENT-v1.md)。其设施、行动点、客人、章节、局内保存和新 AI 不驱动当前游戏。
