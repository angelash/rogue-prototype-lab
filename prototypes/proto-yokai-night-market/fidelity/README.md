# 原作行为恢复 · 妖怪夜市 v2

当前权威是 2026-10-12 用户的 Unity 复刻要求。保留 Crunch Time 的 30 份业务脚本、原 XML 规则、AI、输入、计时、场景结构和模板，换夜市图片、字体与文案。旧 v1 原创改编设计仅存档。

- [行为合同](01-behaviour.md)：出牌、回合、AI、计分、输入与保存。
- [恢复与适配](02-unity-recovery.md)：场景、prefab、旧引擎桥接和复现顺序。
- [美术与导入](03-art-and-import.md)：82 图规格、生成、技术导出和修订。
- [验收与交付](04-validation-and-delivery.md)：真实覆盖、失败记录、构建和缺口。
- [流程与原生用例源](test-cases/flow-and-native.md)：新增重开/保存断言与实际键鼠检查。
- [UI 功能层规格](data/ui-surfaces.json) / [UI 回归用例](test-cases/ui-surfaces.md)：设置、左框、阶段底图、进度/状态标记和原生语言图标。
- [替换数据](data/theme.json) / [内容查阅](data/content-guide.md) / [原图规格](image-specs.json)。
- [文案制作源](data/copy.json)：95 条双语台词、28 份双语委托说明、界面与名称修订；由 create_fidelity_theme.py 合入全部 420 键与生产 XML。[主题颜色](data/presentation.json)记录当前 17 个字色、35 个组件颜色及 28 个材质颜色；后续校对遵循仓库 game-copy-polish skill。
- [v2.8 包装集成](art-direction-v2.8/integration-and-review.md)：38 张 UI 的实际生产、尺寸/透明度/几何锁定、原生纸签与当前构建检查。

本机运行 ../local/Builds/Release/YokaiNightMarket.exe。独立 Unity 工程 ../local/UnityProject/，场景 Assets/Recovery/Scenes/NightMarket.unity，prefab 在 Assets/Recovery/Prefabs/。使用 Unity 2022.3.62f3c1 Windows x64 Mono。完整业务源码在本机 Assets/Recovery/Scripts/，公开 scripts/ 保存原创恢复与验证工具。

首次显示两页教程，按任意键推进。鼠标悬停手牌查看展开信息，点击当前手牌使用；目标选择继续用鼠标。Space/右键交班；Tab 切换快进；Esc 持续按住约三秒退出，提前松开取消。设置含逐牌停顿、快进、声音、教学及两语言槽。六项设置和十条成绩使用独立产品 PlayerPrefs；原作没有局内保存。

工程、参考实现、音频、功能 stencil 派生 PNG、全部制作源和测试用例纳入本仓完整本地分支 codex/yokai-night-market-source，详见 [版本管理](../VERSION_CONTROL.md)。构建/缓存/结果忽略。AI 原图独立创作；成绩/标题底板的两次修订只引用自己生成的原图，谱系已登记。公开原创资料和完整本地 Git bundle 分开备份。

自动规则与渲染证据不等于原生输入、逐像素比较或原速体验一致；准确状态见交付记录。

v2.5 按原作修正操作提示：SubTitle 实为 Continue.Background，无字底板保留原 alpha、628×174 尺寸、网格和位置；运行使用 81 张生图和 1 张程序导出的提示底板，82 份历史生成源全部保留。原生文字分别显示“点击继续”“关闭逐步暂停”“点击调停”。28 条英文委托副说明缩为单行；presentation.json 另声明委托副说明 characterSize=0.85，解决替换字体在折叠标题下贴边的问题。节点变换、引用、点击范围与业务代码不变。新增 NightPromptProbe 与 -PromptsOnly 覆盖中英文两画幅的四种提示状态及全部委托标题间距；当前结果见交付记录。

v2.6 将 26 张闹场/挖角牌原先纯黑的分类条改为陶红色。黑条原本是原作的类型标识，夜市深色包装中辨识度不足；只改 00_NEG_Detail 的 _Color，并保留轮数槽、进度轨道、形状和规则。NightVisualProbe 逐层渲染受影响卡牌的色条，检查实际有色像素，同时继续两语言两画幅的完整卡牌调查。制作和验收入口见美术、用例与交付记录。
