# 妖怪夜市开张

当前 v2 使用 Unity 恢复 Crunch Time 的业务逻辑、场景/UI 和卡牌模板，只换夜市包装。独立工程、运行包、恢复源和完整证据保存在本目录被忽略的 local/。

- [开发合同](DEVELOPMENT.md)：当前决定、范围和实际限制。
- [实现入口](fidelity/README.md)：规则、恢复、素材与启动。
- [全量替换数据](fidelity/data/theme.json)：95 卡、28 委托、420 双语键。
- [内容查阅](fidelity/data/content-guide.md)：当前卡牌参数和委托目标。
- [原图尺寸与导入](fidelity/image-specs.json)：82 张游戏纹理。
- [实际提示词](fidelity/art/prompts/) / [生成原图](fidelity/art/generated/) / [来源谱系](fidelity/art/generation-register.json)。
- [静态验证](fidelity/evidence/fidelity-verification.json) / [交付证据](fidelity/evidence/delivery.json) / [验收说明](fidelity/04-validation-and-delivery.md)。

本机 Unity 工程 local/UnityProject/，使用 **Unity 2022.3.62f3c1**，打开 Assets/Recovery/Scenes/NightMarket.unity。完整 Windows 包 local/Builds/Release/，入口 YokaiNightMarket.exe。压缩备份和实际 hash 由交付记录登记；移动游戏时需带完整目录。

仍被防火墙提示挡住的 Windows 窗口来自 local/Builds/Windows/ 中的早期开发包，屏幕上的旧包装/初始化错误不属于本轮 Release 包。保留该窗口等待用户手动取消安全提示；未通过另开原生窗口绕过提示。

当前为本机恢复候选，原生操作、原速体验、听感与逐像素对照尚须补验。恢复实现、原音频及功能 stencil 派生 PNG 保留本机；公开备份包含原创工具、AI 原图、提示词与数据。

旧改编资料保留在 design/、data/、art/、replacement/；[旧 README](history/README-v1.md) / [旧合同](history/DEVELOPMENT-v1.md)。其设施、行动点、客人、章节、局内保存和新 AI 不驱动当前游戏。
