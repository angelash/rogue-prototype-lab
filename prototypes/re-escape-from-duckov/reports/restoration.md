# Unity 还原记录

2026-10-13；执行依据：[项目合同 v2.3](../DEVELOPMENT.md)。

用户要求全部内容、像素级复刻；使用本机 Unity 2022.3.62f3c1（原构建 2022.3.62f2）。输入为已核验的 Steam 2.3.30 隔离副本，不改原安装与真实存档。

## 当前工作

正在恢复完整 Unity 工程。优先采用原生场景、资产和完整脚本序列化引用；已有研究库提供稳定对象编号、63 场景、201 托管程序集、内容登记和全引用对照。

原生 Player 已进入原标题/主菜单；完整运行闭环、全内容可达、音画和像素对照尚未验证，不能以启动成功替代还原完成。

## 编辑工程入口纠正与崩溃诊断

用户指出 `UnityProject` 没有完整场景/资产、代码主要是 DLL，点击 Play 会崩溃。该反馈属实：这个目录一直是 AB/res 探针和最小宿主，`EditorBuildSettings.m_Scenes` 为空；此前将它简单标为独立工程入口不准确。先前通过原标题测试的是后处理拼装的原生 Player，不是 AssetRipper 作者工程的导入、Play 或完整 BuildPipeline 构建。

当前入口分工已在 README 说明。新增 `local/RestoredUnityProject` 由保留的 AssetRipper 导出建立，包含 63 个 `.unity` 场景、3,112 预制体、2,863 材质、4,395 PNG、238 AnimationClip、37 AnimatorController，使用本地 f3c1 和独立 `RoguePrototypeLab / DuckovEditorRecoveryLab` 身份。`local/decompiled` 有 19,615 个 C# 文件，主业务目录有 1,106 个文件（类型/嵌套类数量是另一口径）；它们是反编译派生资料，尚未整体成为 Unity 可编译源码。业务运行 DLL 暂时保留组件绑定。

用户崩溃日志明确指向 Player Burst DLL 的 `$BurstDirectCallInitializer` / `BurstCompilerHelper.IsCompiledByBurst` 调用未准备好的 Editor 原生编译服务，产生 SIGSEGV。关闭编译选项与设置环境变量均不足以阻止其静态构造。早期副本兼容分支能消除原生崩溃，但随后 Player Input DLL 在 Editor 更新阶段产生大量 `Unrecognized InputUpdateType: Editor`；该路线未作为正式工程方案。

现接入本机缓存的 Burst 1.8.21、Input System 1.14.0 与本地 UGUI 1.0.0 编辑器包，移出对应四个 Player DLL 与 meta，备份保留。UI/Input 的 DLL MonoScript 引用重映射到包源码 GUID；最终残留旧引用为 0。Input 包的 UGUI asmdef 名称适配 Unity 2022，Tests 原样保留于不导入的目录；编辑检查使用 Burst 托管回退。旧宿主 Play 已不再原生崩溃，但因没有游戏资源仍有 3 个资源错误，普通 Play 已增加用途提示/阻止误启动。

资产布局修复：FMOD Settings 原子资产被导出成独立资产，原 `Resources.LoadAll<Platform>("FMODStudioSettings")` 会清空平台列表；将 12 个平台资产移到同名 Resources 子目录并保持 GUID/内容，恢复加载。两个场景映射 `.generated.generated` 文件改为 `.generated.bytes`，恢复 TextAsset 资源键，内容哈希不变。编辑器读取的 `FMODBanks/Desktop` 补原 Master 银行，StreamingAssets 原银行保留。样条 UI 仅在 Editor 跳过不受支持的 GCMode 切换并同步执行同一 spline job，Player 分支保留原计算/调度；所有 DLL 修复前副本及哈希保留。

实际验证：编辑器编译与导入完成，63 场景逐个打开，共 412,789 GameObject、19,065 导入路径；4 场景有 16 个空脚本组件，导出文本本来即为 `m_Script: {fileID: 0}`，其原始输入来源仍需追溯。首次菜单组件读取独立保存路径时目录未建立，已在 Editor 初始化补建本项目自己的 Saves 目录。

Startup 无图形 Play 已进入 MainMenu 并能退出，最新 8 秒记录仍有 163 个错误；无图形输入设备及资源/渲染错误不计为通过。正常图形尝试另暴露 URP 后处理材质空引用，尚未通过图形运行。原生 Shader 桥接尝试在完整 AB 加载时停滞，已经停止专属测试进程，默认不开启；其参数 `--duckov-native-shader-editor` 只作为后续实验入口，不能称正式 Shader 修复。没有用禁用画面后处理证明像素等价。

[编辑工程证据](../evidence/editor-project-recovery.json)保存上述结果与本机日志路径。原始导出未覆盖，原安装和原生 `local/Player` 未修改；此前启动通过的结论继续仅适用于该原生 Player。编辑工程完整业务源码化、正常画面/原生操作、所有游戏流程、保存重载及逐像素对照均未完成。

## 原生资源路线实测

用户随后提出优先用 AB/res 原生调用。已建立独立 Unity 宿主、原程序集输入登记和恢复实验工具。使用官方便携 AssetRipper 1.3.5，完整导出为 `local/AssetRipperExport/ExportedProject`：35,815 文件、3,458,499,849 字节，包含 63 场景、238 动画剪辑、37 控制器；这只是导出数量，未作为运行完整性证明。版本/下载哈希见 [进度证据](../evidence/restoration-progress.json)。

原 `resources.assets` 和 `.resS` 不是可直接调用的 AB。恢复实验保留原对象载荷，用本机 Unity 生成 AB 目录结构，并在隔离副本补类型树、适配版本元数据。最新宿主 Player 包 88,071,704 字节，0 构建错误、0 警告；独立身份为 `RoguePrototypeLab / DuckovRestorationLab`。Library 和实验缓存分别放 D 盘专属目录并通过本机目录链接访问，原输入不移动。

[原生资产实测](../evidence/native-asset-probe.json)确认在 Unity 2022.3.62f3c1 / Direct3D11 Player 中加载了原 AnimationClip（0.8 秒 / 50 FPS）、Mesh（1,431 顶点 / 1 submesh）、编译后的 `Shader Graphs/SodaDecalWaterV2` Shader（支持）和 1024×1024 Alpha8 字体图集。纹理数据流另保持原 `.resS` 侧文件。原数据对象与流的哈希记录在本机实验清单。

实际失败与修复保留：未经适配的无类型树 AB 被版本/脚本检查拒绝；资源目录 ID 与跨文件 `archive:/...` 路径修正后，原 Graph Shader 成功，先前崩溃没有证明原 Shader 与本地版本不兼容。65 个资产文件含 129,067 原对象，129,066 载荷字节不变，仅 TMP SpriteAsset 的一处材质指针为 AB 目录位置做了显式重映射。63 个场景含 1,472,139 原对象，其载荷字节全部不变；这是封装/静态核验，尚无场景运行结论。

合并 AB 在 Editor 可恢复原玩法/场景配置和 URP 原类型，但 Player 仍出现脚本绑定失败；脚本登记、身份哈希、类型树规范化及依赖预加载实验没有通过全量 Player 验收，不能把 Editor 成功当成 Player 成功。对生成工具发现的旧读缓冲与实际写入字节差异，已增加落盘后重读哈希核验；旧失败候选保留在专属 D 盘缓存。

随后改用原 Player 的 Resources/shared 原生文件布局、脚本登记与资源映射，并使用本机新构建的 UnityPlayer 与许可证/构建设置；只适配版本元数据和独立产品/存档身份。[原生 Resources 实测](../evidence/native-resources-probe.json)通过：原 `GameplayDataSettings`、物品库、任务库、场景配置、`URP-HighFidelity`、原 Graph Shader 均成功，Player 日志未再出现此前的 VLB 初始化与脚本缺失异常。该实测仍使用空相机场景，不表示完整游戏、全部配置字段或像素等价已通过。

因此采用原生 res 布局作为完整运行恢复主线；AB 作为独立资产和缺制作源对象的补充路线。恢复场景时保留原场景编号，避免 AB 场景 `buildIndex=-1` 影响原小地图与多场景逻辑。原代码、Steam 检查、FMOD 银行与插件保留，所有实验仅在副本和独立存档身份中进行。

Unity 官方提供 [ResourcesAPI](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ResourcesAPI.html) 与 [SceneManagerAPI](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.SceneManagerAPI.html)；后续优先通过这些接口接入原资源键/场景加载，减少游戏逻辑改动。范围仍为原合同全部内容。

## 原启动流程与普通窗口实测

候选运行包：`D:\CodexCaches\duckov-restoration-full-game-v3\DuckovRestored.exe`；工程：`local/UnityProject`。本机宿主引擎 SHA256 与候选 UnityPlayer 一致。原 Resources/shared/63 场景对象载荷逐文件写出后重读核验，字节不变；版本标记适配为 f3c1。`globalgamemanagers` 保留原 MonoManager/资源映射与其他运行设置，只替换独立产品身份，以及本机宿主的 BuildSettings 构建/许可尾段，并恢复原 63 条场景路径。不是直接复用原 f2 引擎启动文件，也不代表导出的编辑工程已经完整导入。

[正常运行证据](../evidence/native-game-qa.json)：Unity 2022.3.62f3c1 正常窗口启动，原 Startup 流程进入 `MainMenu`（编号 1），构建场景数 63、1 个相机、记录 0 个异常；本机截图 `local/game-qa-normal/game.png` 实际显示原标题及“点击屏幕继续”。随后 Windows 原生点击能够打开原主菜单、空存档列表。未进入实际关卡，未验证新建角色、保存重载、基地/出击/撤离闭环。

先前 batchmode 运行虽然进入 MainMenu，但 `Keyboard.current` 为空、截图黑色；这个失败记录保留于 `local/game-qa/`，不计为画面通过。普通窗口模式没有该异常。原 `ResolutionSetter` 从 Options 选择分辨率，会覆盖命令行分辨率；测试生成的独立 Options 原为 3840×2160，随后仅为还原版改为 1280×720 窗口，并保留修改前文件 `local/options-before-resolution.ES3`。修改后的画面尚未复验。

桌面自动化在上述阶段被用户物理 Escape 停止，未继续输入；不把后续流程登记为通过。正常运行截图与完整日志仅本机保存，公开证据只含运行状态与路径；原 Steam/FMOD 行为保留。原启动 SDK 会进行正常平台初始化并产生平台日志，该事实区别于此前的静态研究阶段。

因此，缺类型树或缺编辑器制作源的对象，可以在已验证的原生布局中保留运行载荷使用；不需要先强行转成 PNG/OBJ 或重建源 Shader。脚本程序集、对象指针、依赖文件、流偏移与平台插件仍需一起对应。AB 是补充路线，原生 res 布局目前是完整运行主线。全场景运行、内容覆盖、音画验收及同条件逐像素比较仍待完成。

## 工程迁回 F 盘

2026-10-13 用户清理 F 盘后要求迁回。上述 D 路径是早期试验位置；当前 Unity 工程仍为 `local/UnityProject`，Library、本地 `.git`、NativeProbe、NativeMerged 和四组绑定实验已成为 F 盘实体目录。运行候选现为 `local/Player/DuckovRestored.exe`，启动脚本按所在工程解析默认路径。其余旧候选归档到 `local/StorageExperiments/`。

[迁移证据](../evidence/storage-migration.json)：19 个目录、64,999 文件、38,182,411,681 字节（35.56 GiB），每个文件在复制时计算源 SHA256，再独立读取目标校验，通过后切换原目录链接；8 个活动 junction 已撤掉，D 盘原件保留。完成时 F 可用 301,324,279,808 字节（约 280.63 GiB）。Unity 安装位置和独立保存身份沿用原值；迁移未重建或更改游戏内容。逐文件清单位于本机 `local/storage-migration/2026-10-13-return-to-f-verified/`。

初次串行复制仅生成未激活阶段目录，随后改用多线程逐文件核验完成迁移；旧阶段与记录保留并被忽略，不参与版本快照。运行日志不纳入新增快照，完整工程/资产输入与原创报告分别备份。

[还原阶段本地备份](../evidence/backup-restoration.json)已通过：69,086 文件 / 15,161,947,550 字节纳入本机无远程版本库，466 原始输入 blob 与工作字节一致，Git 完整性检查通过。新恢复包 `D:\CodexBackups\duckov-restoration-2026-10-13-local.bundle` 为 6,024,793,112 字节，完整历史校验通过；SHA256 为 `eb919efc4c70f45191af54cc82e5a728fac8f3643a7b3a5156ddd9bd72d04da0`。原静态包不覆盖，缓存可从原输入、恢复工具和已保存宿主重新生成。
