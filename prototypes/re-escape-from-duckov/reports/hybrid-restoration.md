# 反编译源码 + 原生 res 混合还原

2026-10-13。按用户最新路线实施，本地 Unity 2022.3.62f3c1、Windows x64 Mono。

## 工程与资产

源码工程 `local/HybridUnityProject` 的 `Assets/Reconstructed` 有十个程序集、1,430 个 C# 文件，主业务为 1,105 个文件；另外十个脚本登记辅助类由 Unity 重新生成。运行入口为 `local/HybridPlayer`。原 `local/decompiled`、输入构建、AssetRipper 导出和旧候选保留。

实际从源码替换的程序集为 TeamSoda.Duckov.Core、TeamSoda.Duckov.Utilities、TeamSoda.MiniLocalizor、ItemStatsSystem、SodaLocalization、Plugins、Assembly-CSharp、KAnimationCore.Runtime、FlexReader、PackageTools。引擎、第三方框架、渲染和平台依赖继续使用匹配 DLL，不冒称它们已源码重写；NodeCanvas 局部反编译缺口保留原记录。

资源沿已验证旧宿主的 Resources/shared/原生场景布局加载，保留原 63 场景编号、资源键、对象载荷和流偏移。AB 为独立资产补充路线，之前完整 AB 绑定与 Shader 编辑桥接失败没有作为通过方案。

Unity Assets 中只需构建种子场景，完整游戏场景在原生运行数据内。编辑器 Play 打开独立 Player；修改 C# 后先执行构建入口。此模式不能在编辑器直接编辑原生场景物体并自动写回原载荷，也不把空场景当成游戏预览。

## 源码与构建证据

先用本机 Roslyn 和精确原 Mono 引用集编译十个反编译项目，无 NuGet 恢复/.NET 升级。仅修正 DuckovDialogueActor 两个显式接口 getter 的 C# 写法、ItemTreeExtensions 一个局部变量重名。接入 Unity 时将文件作用域命名空间换为 C# 9 块，由 Unity 生成脚本登记，并补源码程序集引用与 SharpZipLib。

本机 Unity 首次编译和构建结果 Succeeded、0 错误、75 警告、2,063 MonoScript；整理入口后的复跑为 0 错误、4 警告、2,062 MonoScript，最后增量构建为 0 错误、0 警告。原警告主要是废弃 API、未等待任务和未使用变量；没有为消警告改写原逻辑。运行包十个业务 DLL 来自该 Unity 构建。

直接使用新宿主全套编辑器包作为运行依赖的候选在 Startup 提前退出，保留为 `local/HybridPlayer-UnityHost-v2`。当前按用户指定旧宿主路线保留匹配运行依赖，只接入本轮 Unity 编译的业务 DLL。可选 QA 观察器按同一依赖集编译，不改变游戏规则和平台检查。

ABI 核验覆盖十个程序集、1,716 个非编译器内部类型、5,900 序列化字段：程序集身份、继承/接口、字段名称/类型/顺序、公开方法签名一致，差异为 0。编译器生成状态机/匿名类型/私有常量数据另属重编译实现。该结果证明绑定接口，不能证明所有方法语义相同。

独立重读运行包 207 个原生资产/场景/流文件与十个业务 DLL 摘要，失败为 0；引擎与已验证本机 f3c1 旧宿主相同。原对象载荷不变由此前封装时逐对象哈希和本次文件摘要共同追踪。

## 回归与缺口

早期 Roslyn 源码候选的真实鼠标点击通过标题→主菜单→新游戏→默认均衡难度，显示序章三维画面。最终 Unity 编译包通过原按钮回调复测这些路径。真实键盘两次遇窗口激活失败，未计为通过。输入层注入方向后角色首次移动约 7.93 米、最终包复测约 7.95 米，此结果明确是集成测试。

序章角色初始化、背包开关、基地加载、保存/场景重载完成。退出并重新启动后，磁盘自测标记读取成功，角色与 Base_SceneV2 加载成功。只使用独立 `RoguePrototypeLab/DuckovHybridRestorationLab` 自测槽位 6，不读写原真实存档。

零号地区通过原 SceneLoader 加载，首次检查有四个角色、最终包复测有五个角色。伤害管线施加 5 点真实伤害，生命值精确减少 5；测试后恢复 5 点继续回归。原撤离事件触发并返回基地。这是场景/伤害/撤离集成证据，真实瞄准射击、AI 全行为、步入撤离区域倒计时未实测。

完整启动仍记录 **SteamAPI_Init() failed**，上述游戏回归未记录其它异常。保留原检查，没有过滤错误使总测试显示通过；已请求用户确认 Steam 登录及从库启动条件。先前 AfterSceneLoad 才记录错误的启动检查漏掉早期平台错误，本轮观察器从 BeforeSceneLoad 记录，以完整记录为准。

画面对照使用同一 f3c1 引擎/原生资源、60 FPS 固定模拟，在第 900 帧起冻结标题并截图，对照原业务 DLL 与重编译业务 DLL；1280×720 的 921,600 像素全相同、RGB 差值为 0，PNG SHA256 相同。原 URP 噪点随 Time.frameCount 变化，未对齐帧的样本不相同；没有关闭原后处理来获得相等。详见[混合证据](../evidence/hybrid-restoration.json)。黑屏无效样本被排除。这不是原 f2 与 f3c1 的全场景/动态/天气/战斗画面证明。

最终重复构建后 Assembly-CSharp 摘要改变，其余九个一致，因此对最终十个 DLL 重新执行上述集成测试和标题对照，并将各测试的构建摘要保存在证据目录。旧标题对照与最终样本有 4,889 像素微差；新采集的原 DLL 对照相对旧图也出现相同微差，最终包与该新对照全像素相同。保留所有样本，不把一次相同推为全部画面可复现。

完整可运行版本另存 `local/hybrid-source-player-build-20261013-233539`，包括实际 Unity 编译 DLL、原生资源、匹配引擎、构建摘要及绑定审计，纳入本地离线快照；活动 Player 和 Unity 生成缓存按规则排除。

本版已落实混合路线并通过上述代表性游戏回归；**总自测尚未全部通过，不能宣称 100% 还原**。平台初始化、真实键盘/完整操作、全部场景内容可达、武器/任务/AI/音频覆盖和原 f2 全画面对照仍需验证。

## 复跑

- `scripts/start-hybrid-project.ps1 -Editor` 打开源码工程；不带参数打开 Player。
- `scripts/build-hybrid-project.ps1 -Launch` 从源码构建、审计、接回旧宿主资源并运行，旧候选按时间保留。
- `scripts/start-hybrid-project.ps1 -SelfTest` 菜单检查；`-Suite` 输入层/场景/保存集成检查；随后 `-Resume` 验证进程重启保存，`-Combat` 验证伤害/撤离，`-Pixel` 截取冻结标题样本。Suite 会先保留独立自测槽位 6 的旧数据，再从空槽位复跑。
- `audit_hybrid_abi.ps1`、`verify_hybrid_player.py` 分别核验绑定接口和运行摘要。完整日志/截图只在 local 保存，公开证据仅摘要与事实。
