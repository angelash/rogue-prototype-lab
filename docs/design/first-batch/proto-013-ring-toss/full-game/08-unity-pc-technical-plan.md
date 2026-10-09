# 套圈改造摊：Unity、PC 与工程技术方案

版本：v0.6；日期：2026-10-09（Asia/Shanghai）。状态：已建立独立原型工程，当前按用户明确的三维斜视地摊方向迭代首个单摊场景；实际实现、测试和 Player 结果见 [开发记录](../../../../development/proto-013/2026-10-09-p0.md)。

用户已明确采用 Unity、使用本地已安装版本、参考 `F:\workspace\highschool`，制作 PC 单机游戏并以 Steam 为发行目标；随后明确要求真实三维场景、斜视镜头、玩家在镜头前向前抛圈，像真实地摊。旧二维展示与固定正交 2.5D 建议已被这一方向替换，历史验证仍保留。镜头参数、三维几何实现、材质和完整工程分层是助手建议，首组三维范围见 [场景方向合同](../../../../development/proto-013/3d-scene-direction.md)。Steam 发行与平台接入详见 [11 发行方案](11-steam-release-plan.md)；玩法与经济仍以 [02](02-gameplay-systems.md)、[04](04-economy-and-balance.md) 为准，存档行为以 [06](06-technical-save-and-accessibility.md) 为准。本次文档同步没有运行 Unity、安装软件或改动外部参考工程。

## 1. 本机版本与能力证据

核验日期为 2026-10-09。版本来源是工程文本、Windows 文件版本和安装目录，不是猜测 Hub 默认目录，也没有通过启动编辑器读取许可证。

- **准确编辑器版本：Unity 2022.3.62f3c1**，revision `1623fc0bbb97`。参考工程 [ProjectVersion.txt](F:/workspace/highschool/game/SheGuessedItAgain/ProjectSettings/ProjectVersion.txt) 的 `m_EditorVersion` 和 `m_EditorVersionWithRevision` 与实际可执行文件一致。`c1` 是该已安装版本标识的一部分，不能删去后用另一个同名全球发行版本替代。
- **实际编辑器路径**：[Unity.exe](<D:/Program files/2022.3.62f3c1/Editor/Unity.exe>)。文件 `ProductVersion=2022.3.62f3c1_1623fc0bbb97`、`FileVersion=2022.3.62.1451004`，大小 89,411,408 字节。Hub 的 `secondaryInstallPath.json` 指向 `D:\Program files`；注册表的 Unity 编辑器显示版本也为 2022.3.62f3c1。
- **Windows x64 Mono Player 文件存在**：`Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_development_mono/UnityPlayer.dll` 与 `win64_player_nondevelopment_mono/UnityPlayer.dll` 均存在。可以把 Windows x64、Mono 后端作为当前机器已有构建模块的计划基线。
- **不认定 Windows IL2CPP 已可用**：[modules.json](<D:/Program files/2022.3.62f3c1/modules.json>) 中 `windows-il2cpp`、`windows-server` 的 `selected=false`；检查的 Windows Player 变体只有 Mono，没有 IL2CPP 变体。此处是文件与模块记录核验，不能推导出已经拥有 IL2CPP 所需的完整工具链。
- **参考项目具有历史构建证据**：[构建说明](F:/workspace/highschool/production/BUILD_INSTRUCTIONS.zh-CN.md) 指定同一路径、同一编辑器与 Windows x64 Mono；[历史日志](F:/workspace/highschool/artifacts/unity-build-full.log:3003) 记录 `Windows full build complete`，总输出大小 312,297,628 字节。现存 `Builds/WindowsFull/SheGuessedItAgain.exe` 为 666,624 字节、同目录 `UnityPlayer.dll` 为 31,200,080 字节；这些是参考项目既有文件，本轮没有重新构建或运行它们。

上述是创建工程前的只读环境基线，确认编辑器与 Mono Player 模块文件存在，并与参考项目的版本、历史构建路径相符。它们本身没有验证许可证、包解析或本游戏运行；后续工程已有独立验证过程，事实以开发记录为准。旧二维 P0 的结果不能直接作为新三维命中、输入或画面的验收；Steam 客户端和目标 PC 性能也需各自记录。

## 2. 参考工程读取范围与取舍

外部参考主目录为 `F:\workspace\highschool\game\SheGuessedItAgain`；原文件不改写，也不复制整个工程。参考文档自身没有统一文档版号，本文以读取日期、原文件名和工程版本记录其来源。

- [README.md](F:/workspace/highschool/README.md)：确认 Unity 2022.3 LTS、Windows x64、离线游戏及 `game/`、`production/` 的组织方式。该互动影游的题材、图片、剧情和小游戏不转入本项目。
- [manifest.json](F:/workspace/highschool/game/SheGuessedItAgain/Packages/manifest.json) 与 [packages-lock.json](F:/workspace/highschool/game/SheGuessedItAgain/Packages/packages-lock.json)：锁定了 TMP 3.0.6、uGUI 1.0.0、Test Framework 1.1.33、Addressables 1.21.21、Timeline 1.7.7 等，注册表 URL 使用 `https://packages.unity.cn`。这不授权新项目直接照搬全部依赖或切换包源。
- [ProjectBuilder.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Editor/ProjectBuilder.cs)：可参考静态编辑器入口、内容验证、`StandaloneWindows64`、Mono、严格构建和失败报告。该脚本的准备方法会重建 Bootstrap 场景并设置原游戏产品名；新工程不照搬为每次构建时覆盖制作场景的操作。
- [RuntimeBootstrap.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/Bootstrap/RuntimeBootstrap.cs)：可参考单一启动入口、服务初始化、场景展示与设置加载的职责；不把全部新玩法塞进一个 MonoBehaviour。
- [GameState.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/State/GameState.cs)：可参考规则状态与序列化 DTO 分开、集合稳定排序。其剧情变量模型不能代替本游戏的奖品资格、整数账本和固定时间步状态。
- [SaveService.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/Services/SaveService.cs)：可参考局进度、设置、Profile 分离与 `Application.persistentDataPath`。它名为 `AtomicWrite` 的实现是先写 `.tmp`，再删当前文件、再移动；删除后移动前存在中断窗口，不能直接当作本游戏事务与备份的原子保证。
- [UiFactory.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/UI/UiFactory.cs)：实际使用 `UnityEngine.UI.Text` 和 uGUI。参考 manifest 有 TMP 并不等于参考界面已经使用并验证 TMP。
- [GameAssetPostprocessor.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Editor/GameAssetPostprocessor.cs)：可参考按目录规范纹理/音频导入；影游图片、无透明边界的设置不能直接套到透明圈、三维模型或功能标记。
- [StoryGraphTests.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Tests/Editor/StoryGraphTests.cs)：可参考 EditMode 内容验证和状态往返测试的组织，不复制题材数据或把其测试结果当成本游戏验证。

参考 `GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 为 `fileID:0`，`ProjectSettings.asset` 的 `activeInputHandler=0`。其 manifest 没有 Input System 和 URP。参考 `Library/PackageCache` 中可见 TMP 3.0.6、Test Framework 1.1.33；没有找到 `com.unity.inputsystem*` 或 `com.unity.render-pipelines.universal*` 缓存目录，另核验 Input 1.6.1 候选的指定缓存路径也不存在。这里仅描述检查范围，不能宣称本机其他工程没有这些包，更不能宣称本游戏已使用它们。

## 3. 工程根目录与最小依赖建议

独立工程已位于 `prototypes/proto-013-ring-toss/game/RingTossWorkshop/`，已有 Assets、Packages、ProjectSettings 和准备/测试/构建入口。以下是完整制作的目标目录，不表示每个服务、场景和测试层都已实现；当前目录与使用方法见 [工程 README](../../../../../prototypes/proto-013-ring-toss/game/RingTossWorkshop/README.md)。

```text
prototypes/proto-013-ring-toss/
  README.md
  game/RingTossWorkshop/
    Assets/RingToss/
      Core/                 纯 C# 坐标、轨迹、对象、事件与随机状态
      Application/          摊/局流程、命令、奖励和事务协调
      Presentation/         Unity 视图、UI、输入、音频与相机
      Platform/             可选平台适配与离线空实现
      Content/              参数、十二模板、故事和中文文本
      Art/Models/           低模圈、奖品、机关与功能摊位
      Art/Materials/        确定渲染管线后的材质
      Art/Textures/         十二奖品皮肤及功能标记
      Art/Portraits/        六人物的二维半身像
      Art/Scenes/           可复用三维棚、摊地、灯和远景部件
      Art/Backgrounds/      可选远景图片，不承担主场景或判定
      Audio/                已授权音乐和功能音效
      Fonts/                已授权中文字体与 TMP 配置
      Prefabs/              六槽视图、机关、圈、UI组件
      Scenes/               Bootstrap、Stall、PresentationTest
      Editor/               内容检查、构建入口与导入约定
      Tests/EditMode/       规则、事务、存档和数据验证
      Tests/PlayMode/       Unity绑定、输入、展示与恢复验证
    Packages/               manifest.json、packages-lock.json
    ProjectSettings/        精确版本与可追踪项目配置
    Builds/Windows64/       生成结果，沿用仓库忽略规则
```

本机编辑器固定 2022.3.62f3c1；项目提交 `ProjectVersion.txt`、`manifest.json`、`packages-lock.json`、必要的 ProjectSettings、Assets 及 `.meta`。`Library`、`Temp`、`Logs`、`Obj`、生成构建不入库；不把 `.meta` 当缓存删除，也不把外部工程的 Library、账号配置或许可证复制进来。

建议首次设置采用 Visible Meta Files 与 Force Text 序列化，便于审阅场景/Prefab 和保留引用；源 `.blend`、导出 FBX 与字体按仓库二进制属性保存，Blender 自动 `.blend1/.blend2` 和 Unity 缓存/构建沿 ignore 排除。正式制作源和 `.meta` 不能因清理缓存一并删除。

建议最小依赖分两步，不为了仿照参考工程导入整套包：

- **P0 已有依赖与后续 UI 候选**：现有 manifest 使用 Test Framework 1.1.33 与音频、图像转换、IMGUI、JSON、截图及三维 Physics 内置模块；Physics 用于场中鼠标选取，功能真值仍由规则层计算。当前包文件是实际依赖依据。uGUI 1.0.0、TMP 3.0.6 是后续产品界面的候选，不能写成已经接入。首个三维场景可使用 Built-in 管线与受控材质；新包必须有明确用途并经解析、编译和包锁定核验。
- **正式输入候选**：Input System 1.6.1，以 [Unity 2022.3 中文手册](https://docs.unity3d.com/cn/2022.3/Manual/com.unity.inputsystem.html) 列出的 released 版本作为匹配候选，不称其为当前最新包。后续验证键鼠、手柄、重映射、断连和 UI 焦点后锁入包文件；本轮未导入。使用一个输入后端为主，不长期通过 `Both` 让同一操作被两套后端重复发射。
- **后续三维渲染候选**：URP 14.0.x 可评估三维网格/灯光路径，采用前选择并锁定与本编辑器匹配的小版本；真实三维并不要求先新增 URP。官方说明 14.0.x 对应 2022.x；这只支持兼容方向，不等于本机已经解析、导入或测试成功。[URP 14 兼容要求](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/requirements.html)
- **暂不列入最小依赖**：Addressables、Timeline、Cinemachine、DOTS/Burst、联网框架和完整 Steam wrapper。固定相机、十二模板和有限资产可以先用普通场景/Prefab 与明确内容清单；发现实际加载或生产需求后再单独评估，不依据参考 manifest 自动引入。

TMP 的 Essential Resources、中文字体授权、缺字回退、动态字库与构建字形均需后续实际验证；“参考缓存有包”不能代替中文界面测试。URP 与 Built-in 的材质不能混为同一资产规格，必须在正式材质生产前过渲染选择门槛，不在出包前临时切换管线。

## 4. 纯 C# 规则真值与 Unity 表现层

推荐五个程序集边界：`RingToss.Core`、`RingToss.Application`、`RingToss.UnityPresentation`、`RingToss.Platform`、`RingToss.Editor`，以及 EditMode/PlayMode 测试程序集。Core 关闭 UnityEngine 引用；Editor 代码仅进入编辑器，测试程序集不进入发行 Player。

- **Core** 建议使用自有三维 double 坐标、速度和投掷方向，以及整数圈/耐久/金额、唯一对象编号、显式种子和有序事件。真实三维的接受体、接触面和作用体必须有版本化定义，并与场景锚点一致；不能用不可见的二维平面计算中奖。04 的经济、耐久、圈额、时间步及已登记标量建议保持，旧二维线段/矩形与单排槽坐标只作历史基线。三维求交和事件排序需独立验证，不依赖 MonoBehaviour 回调顺序或让装饰 MeshCollider 更改规则。
- **Application** 接收 `Aim/Launch/Move/Toggle/Cash/Retain/Salvage/Buy/Repair/EndStage` 等受阶段约束的命令，协调当前摊与本局状态、幂等事务及存档提交。界面不能自己增金币或把旧物变成本摊新奖品。
- **UnityPresentation** 把状态与事件映射成 Transform、圈模型、价签、作用区、UI、音效和角色对白。只读模拟结果；拖拽、焦点或按钮发出命令后等规则确认，不先把物件挪走再尝试修正真值。
- **Platform** 暴露可选成就、云存档入口和平台状态接口；默认离线空实现。Core 不引用 Steam SDK，平台不可用时本地新局、存档、笔记和结算照常工作。
- **Editor** 对内容进行预检查、保存清单与构建；不在发行版中调用 AssetDatabase 或运行内容生成工具。

数据流统一为：**读取版本化内容/存档 → 验证并冻结配置 → 输入量化成命令 → 纯 C# 模拟 → 产生有序规则事件 → 原子提交状态/奖励 → Unity 表现映射与复盘**。读档和回放复用同一规则，回放模式没有正式经济提交权限。

## 5. 固定时间步、输入与事件合同

04 的 `dt=1/120 s` 是自有模拟步，不直接依赖 Unity 默认 FixedUpdate 节奏。建议用累计时间推进 runner，完整步计算真值、两步间插值只给展示。每步可以做有限子步处理首次接触；规则事件排序和同一奖品顶部接受优先关系沿用 02/04，不因 GameObject 遍历顺序变化。

三维输入明确分成左右瞄准、仰角与力度；建议仰角和速度沿用 04 的 0.25°、0.02 m/s 精度，左右瞄准范围与精度由三维实现登记，不能从旧二维发射角推断。保存真实量化输入、三维位置/方向、布局与接触版本，不拿 UI 四舍五入的文字倒推出回放输入。机关布置、风况、圈型、耐久、圈级触发次数、挡风罩本圈保护记录和本投初态一并进入复现资料。

渲染卡顿时可以少绘制帧、分批赶上模拟；不得丢弃已经接受的规则步、重复发射或跳过跨接受线的事件。暂停冻结推进；0.5× 观察改变播放推进速度而不改变每步 dt、重力、判定和事务。若性能不足而延迟展示，显示一致的状态，不用增大 dt 敷衍。

随机序列按候选布局、风况预设等用途分流并保存；模板序号、解锁快照、奇偶槽 fallback、候选覆盖和开摊冻结使用 03 的定义。禁止使用 Unity 全局 Random 或墙钟时间临时重抽局内内容。

目标是同一规则版本、同一 Mono Windows 构建内稳定复现 04 的事件、命中与账目。使用 double 和固定步本身不保证不同 CPU、后端或未来版本位级相同；跨平台、IL2CPP 或数值算法切换要另做回归，不用“确定性”一词省略验证。

## 6. 三维地摊与共同规则锚点

首个场景采用真实三维奖品、圈和摊位，相机建议固定在玩家后上方，以透视斜视角看向地面两排三列奖品。近处显示投掷手与圈，左右瞄准、仰角、力度控制向前投掷；无自由漫游。暖灯夜市、条纹棚、帆布摊地和暖色实体旧物是首组包装建议，详见 [方向合同](../../../../development/proto-013/3d-scene-direction.md) 与 [09](09-art-and-asset-production.md)。二维人物半身像只用于可选对白/UI。

建议内部与 Unity 共用 Y 向上、X 左右、Z 指向奖品的坐标约定，1 Unity unit 对应 1 标定 m。具体槽位、相机位置/视场、投掷点和尺寸由三维版本配置登记，不沿用旧正交相机与单排 XY 布局。画幅改变只影响取景与 UI，不能移动真值或补偿透视远近。

- 圈的真实三维中心、奖品接受体、板面法线、风场和机关入口等从同一版本配置生成视图与规则；训练/复盘可投影显示它们。可见功能位置与求交位置相符，不在隐藏二维平面中奖。
- 可采用公开、稳定的三维几何简化，不要求首轮实现完整环体刚体。圈装饰转动、阴影和手部动画不独立领奖、扣圈、扣耐久或重复发射。
- 鼠标选物的 Collider 只返回 itemId/slotId 并经阶段校验；它与功能接触体职责分开。功能接触来自登记的三维几何，不能由导入模型时自动附加的任意 Collider 改写。
- 背景灯、棚、货箱和人物默认无功能接触。需要真实障碍时先登记规则类型、几何、版本与验证；低画质只能删装饰，不能删功能或改变接受体。
- 三种圈、六机关和十二皮肤的完整计划继续共用规则锚点；当前只制作首组单摊资产，不把全量清单写成已完成。

验证要将三维调试体、同一投输入、实际接触点、事件与 Player 画面对应。近排/远排、左右偏移、高低弧线和受机关影响的投掷需分别核对；旧二维测试或一张三维截图均不能替代这些证据。

## 7. 三种场景与资源载入

建议只制作三个 Unity 场景职责，不为十二摊复制十二份独立场景逻辑。

1. **Bootstrap**：启动、读取设置/局外档、创建服务和界面导航，进入标题/继续/练习。场景切换保持一个明确生命周期入口，不通过多个自动创建脚本重复实例化服务。
2. **Stall**：正式局、练习和回放共用的六槽舞台；按版本化 StageSnapshot 实例化奖品/机关外观，分配相机、UI 与事件展示。场内只读资源可缓存，舞台实体和本摊资格不能跨摊意外残留。
3. **PresentationTest**：开发用的相机、模型尺寸、功能轮廓、TMP、灯光、声音、低动态和输入检查场景；使用已保存固定投掷数据。默认不加入发行 Build Scenes，不奖励金币或笔记。

先用显式 ContentManifest 加场景引用/Prefab 引用载入有限资产；每章可按需加载并释放纯装饰资源。资源找不到时阻止开摊并显示可定位的配置错误，不用错误皮肤默认成高价奖品。剧情和图鉴使用稳定文本键、角色与内容编号，完成跳过或复看不改变经济。

## 8. 内容、存档与事务实现候选

Unity 编辑期可用 ScriptableObject 提供受约束的配置入口；开局前编译/校验为不可变 Core DTO 和 ContentManifest。存档与回放保存稳定 ID、数据版本和内容摘要，不存 GameObject 引用或资源绝对路径。

继承 06 的 `Profile/Run/Stage/OwnedItem/Throw/Transaction`，显式包含本摊解锁快照、公开风况、基本圈与补救普通圈余额、重/宽轻各使用额度、两次调整预算、补救是否已买、候选和预装覆盖结果、奖品价快照与处分终态。币和笔记分账；商店物 `sourceStage=null`、`receiptEligible=false`。

建议使用可版本化 JSON DTO；`JsonUtility` 可处理字段 DTO，复杂集合先转稳定排序数组，与参考 GameState 的做法一致。新增序列化库需要有实际问题与兼容测试，不为了映射字典先扩大包依赖。

写入根由 `Application.persistentDataPath` 取得，路径选择只发生在 Unity 存储适配器，不写在纯规则层。Windows 一般对应 `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>`；公司/产品标识稳定，后续变更需安排迁移。[Unity persistentDataPath](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-persistentDataPath.html)

候选持久化方案为“完整快照代次＋一个提交清单”：把同一事务的 Run 和 Profile 写成新的临时快照、关闭并校验后，保存不可变代次文件，最后在同目录提交一个指向这些文件、带事务序号及校验摘要的 `current` 清单，并保留上一个已验证清单。Windows 可评估同卷 `File.Replace` 带备份、首次文件用同目录移动的实现；真正原子性与断电恢复要在目标文件系统实测。

这样允许一次提交同时引用局内和局外状态，避免独立先写笔记再写本局造成重复章奖励。若任一步失败，当前清单继续指向上一份完整状态；新代次没有成为已提交状态，不可重复领取。设置可独立保存，因为它不改变经济资格。垃圾代次清理在安全边界进行，不能先删唯一有效备份。

这是一种待实现和验证的候选，不宣称两次 File.Move 或方法命名 `AtomicWrite` 自动具备断电安全。主线保持 06 的一个自动续局槽；参考工程的十个手动槽不自动扩大本项目范围。恢复、损坏、迁移及失败后笔记保留都要有故障注入证据。

## 9. 测试分层与验收资料

规则测试先在 EditMode 运行不依赖场景的 Core/Application，再以 PlayMode 和 Windows Player 对照 UI/表现。已有二维 P0 验证作为历史基线保留；新三维测试、报告、构建和 Player 结果分别以开发记录登记，不在本方案预填成功结果。

- **纯规则 EditMode**：04 的无风/恒风闭式对照、接受边界、步内首次事件、机关触发去重、罩末耐久本圈保护、0 耐久、固定候选/解锁 fallback/预装覆盖、旧物快照、目标冻结、最后一圈与补救时序、同物多次处分和跨局编号。12 摊例账保持新收入 2336、旧残值 190、总收入 2526、支出 151、末钱包 2375。
- **首个三维场景补充**：两排三列锚点、左右与深度接受边界、下降穿越、板面法线与风场三维方向、首次接触排序、落地/越界，以及同输入的三维轨迹复现。新增布局不能沿用旧单排可达性结论；当前两根源机关与普通圈先验收，再扩完整内容。
- **存档与经济 EditMode**：一笔事务前后和清单提交前后中断；恢复到完整 Run/Profile；章/通关奖励去重；恢复时不补回已发射圈、不重置特殊圈额度、不使商店物拥有收据资格；坏档备份保留。
- **Unity PlayMode**：相同投掷在正常/0.5×/暂停、不同展示帧率下事件一致；三维模型中心/入口/轮廓与 Core 相符；键鼠/手柄发射只扣一次；待处置和保存错误恢复不重复动画/奖励；TMP 缺字与低分辨率缩放可读。
- **实际 Windows x64 Player**：干净输出目录启动、离线游玩、中文路径存档、窗口/分辨率切换、输入断连、保存空间不足和异常退出恢复；分别验证开发构建与发布构建。Steam 包装后的情况由 11 增补，不用编辑器里通过代替 Player 验证。

报告保存编辑器精确版本、代码/内容/参数摘要、包锁定摘要、测试 XML、日志、BuildReport、输出哈希和失败复现输入。首轮重点对照 RNG/TECH/FULL 验收，不写只重复实现代码的数量测试，也不编造目标 PC 的实际帧率。

## 10. 已有批构建入口与复现合同

本仓已有 `RingToss.Editor.ProjectBuilder.Prepare/BuildWindows` 静态 Editor 方法与 `scripts/run-unity-prototype.ps1` 的 Prepare/Test/Build 入口，参考了外部工程的入口与日志组织并独立实现。当前准备只在 Bootstrap 场景不存在时创建空场景；构建使用 Windows x64 Mono，检查 `BuildReport.summary.result`，失败抛出构建异常。完整内容/包/场景清单的自动验证仍需随制作补齐，不能把入口存在当作完整发行审查。

构建不重建已制作的 Bootstrap/舞台场景，不生成原始素材，不重抽内容，不修改经济参数。首轮 Mono 选择来自当前模块证据；不能把未核验的 IL2CPP 当作 Steam 硬性前置。构建输出在受忽略目录，来源、版本、测试结果与输出哈希以文本记录。

推荐在本仓根目录使用以下已存在的脚本入口；每次调用生成独立 `.local/unity/<时间>-<模式>/` 日志/测试目录，实际结果见开发记录。新三维实现更改后需重新验证，不引用旧输出作为新包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Build
```

保留参考说明“不向 Test Framework 测试命令附加 `-quit`”的约定；测试结果要检查 XML 与错误日志，不仅看进程码。测试入口与平台选项依据 [Test Framework 1.1 命令行](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-command-line.html)。静态 Editor 方法、路径和失败返回合同依据 [Unity 2022.3 命令行](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)。

`-nographics` 的批检查不等于图形效果已通过，也不用于灯光烘焙或画面验收；正式画面与手柄检查需要可见 Player/测试环境。后续运行任何命令前先确认测试目录、项目与输出隔离，不使用 `highschool` 路径出本游戏包。

## 11. Steam 接口与离线边界

本文件只定义平台可选接口，不决定发行政策或具体 SDK 包。候选 wrapper 必须在确定 SDK/许可、Mono x64 兼容和平台测试后引入，并锁版本；不得把参考项目不存在的服务写成已经可用。

离线空实现返回明确的平台不可用状态，不阻止普通新局、继续、练习、回放、笔记和十二摊结算。成就只消费已提交事件；重复打开结算、重放或恢复不再次触发正式奖励。平台相关 ID 不进入 Core 规则公式，不发永久命中能力，不让 Steam 初始化决定帧率和几何。

云存档候选只复用本地存储协议和经验证的快照边界，不把远端文件当作新的奖品或笔记来源；启用范围、冲突方案、SDK 与上传发布流程由 11 决定。本轮不搭 Web 服务、不引入账号登录，不把包文件或构建日志上传到平台。

## 12. 当前单摊三维迭代与后续门槛

环境核验、独立工程和原型脚本已建立；此前二维 P0 的实际结果保持为历史。当前转向首个真实三维单摊，不代表完整三章十二摊已经制作。后续门槛为：

1. 为新三维输入、两排三列布局和接受/接触几何登记版本，隔离旧二维回放；保持 04 账本、圈数、耐久等标量合同。
2. 普通圈、六槽、风扇与反弹板先通过三维轨迹、同锚点和经济隔离验证，再扩大机关/圈型与内容；不能以装饰网格碰撞替代明确规则。
3. 验证固定斜视相机、近处手/圈、棚/摊地/灯和轻覆盖 UI 的实际 Player 可读性；正式材质量产前锁定管线和所需包。
4. 记录本轮 Windows x64 Mono 构建和实际 Player 验证，再逐步实现恢复、事务与本章内容纵切；文件存在、导入成功、编译成功、测试通过、Player 通过分别记录。
5. 全部允许的候选池与预装组合获得实际回放可达证据之后，扩到完整十二摊；Steam 集成和发行验证保持独立门槛，不用平台包装掩盖未验证规则。

本文件描述方向与验证合同，不预填新三维实现完成、测试数量、构建或 Player 成功。实际证据统一进入开发记录；完整存档、跨摊、全量内容与 Steam 接入仍各有独立工作。外部 highschool 工程不因借鉴而改写。
