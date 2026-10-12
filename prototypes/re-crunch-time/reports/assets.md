# 全部资产与场景组织

数据入口：[全对象清单](../evidence/objects.json)、[依赖图](../evidence/dependencies.json)、[容器](../evidence/containers.json)、[导出统计](../evidence/asset-summary.json)、[解码验证](../evidence/verification.json)。原始字节和字段在 `local/raw-objects/`、`local/typetrees/`，媒体在 `local/exports/`；每个导出文件有哈希和对象 ID。浏览入口：[本地资产目录](../local/explorer.html)。

## 容器与全对象覆盖

- `mainData`：1,547 对象，Unity 4.5.2f1；单个完整运行场景、设置和资源映射。
- `sharedassets0.assets`：280 对象，Unity 4.5.2f1；主场景公共纹理、音频、字体、脚本等，约 272 MB。
- `resources.assets`：74 对象，Unity 4.5.2f1；Resources 中的 Sprite、材质和五份 XML，约 109 MB。
- `unity default resources`：82 对象，Unity 4.5.0f1；内置网格、材质、字体引用/GUI 资源。
- `unity_builtin_extra`：2 对象，Unity 4.5.2f1；内置 Shader。

合计 1,985 对象，4,443 条非空跨/同容器引用全部找到目标。所有对象既有原始 `.bin` 又有可读 JSON；没有只导出图片就跳过其它类型。Assets 中的对象 ID 仅在容器内唯一，所以索引主键是 `容器:pathID`。

主要对象数量：462 GameObject、462 Transform、177 MeshRenderer、156 TextMesh、154 SpriteRenderer、137 Texture2D、91 AudioClip、78 Sprite、48 Material、46 MonoBehaviour、29 MonoScript、23 MeshFilter、23 AudioSource、22 Shader、13 Font、13 Mesh、10 MeshCollider、9 BoxCollider2D、5 TextAsset。另有单个相机、粒子系统/渲染器/音频监听和各引擎设置；全量类型列表见 asset-summary。

## 图像与材质

137 纹理中 124 有载荷并导出 PNG；13 为动态字体空纹理。78 Sprite 全部导出 PNG，其中不少共享底层纹理；“Sprite 数量”不是全新原始图片数量。124 纹理有 123 个不同导出哈希，78 Sprite 有 77 个不同哈希，索引保留重复对象的身份而不误删。

主要视觉包括 4:3/16:9 两套桌面和标题背景、0–4 回合卡框/遮罩、五部门图标、员工与事件图像、项目纸张、盖章、箭头、预算/排行/按钮/选项/教程元素。最大背景 Table_4_3/Title_4_3 为 2048×1536；16:9 背景 1920×1080。项目/卡牌界面由多层 Sprite、文字、线条、粒子组合，并非每张卡有一张完整预渲染卡面。

48 材质记录 Shader 指针、颜色、纹理环境、缩放/偏移等完整字段；默认透明 Sprite、文字和细节色用于分部门。全部依赖已闭合，原 Material 对象和 JSON 都保留。22 Shader 导出可读 `.shader`，含发布平台编译片段，不宣称已取得作者未编译的 HLSL/Shader 工程。

旧版 Sprite 的三角遮罩按 vertices/indices 重建；PNG 全量载入通过，代表导出可用，不代表与原生 GPU 渲染逐像素一致。具体兼容处理见[工具说明](../scripts/README.md)。安装中的 ScreenSelector.bmp 属于引擎启动分辨率对话框，432×163 RGB 完整载入通过，原件保留在副本清单内，不混入对象媒体计数。

## 音频和混音

91 AudioClip 全部导出并完整解码。按实际名称前缀分组为 21 MUS、49 SFX、19 HUD、2 AMB：

- MUS：玩家回合八段、AI 回合八段，标题/项目循环、胜利、失败引子与循环。
- SFX：发牌十四变体、whoosh 六变体、键盘八个、bug 三个、卡牌摇动三个，以及负面/增益/修复等事件音。
- HUD：鼠标悬停、按钮、回合结束/文本、总部、任务增减/进度、计时、新记录、盖章等。
- AMB：谈判决策计时循环、暂停循环。

全部采样率 44,100 Hz；导出时长范围约 0.071474–42.665896 秒，总时长 626.750954 秒（不是一局播放时间），不同循环/变体独立计数。91 个导出哈希均不同，文件总量 110,603,432 字节。编码/通道/单文件时长在 `local/media-verification.json`，完整文件关联在 objects。

MusicManager 22 个序列化 AudioSource 引用，按玩家/周次切换、随机初始回合片段、控制切入位置与淡出；音乐最大音量常量 0.25。SoundManager 的主音效、发牌、whoosh 数组和播放索引均已从场景字段还原，重复播放有短时间节流和变体选择。场景总共 23 AudioSource，多出的 Core 音源供即时音效；“91 clip”不等于同时存在 91 音源。

本轮执行解码检查，未执行主观听感、原生循环拼接、混音响度和节奏检查。第三方音频没有加入本仓其它原型。

## 字体、网格、动画和粒子

13 Font：12 有可导出字体数据，共 7 个不同文件哈希；其余为内置 Arial 引用、无字节载荷。字体使用 DeconStruct、MyriadPro、HandmadeTypewriter，分别构成标题、数字、符号、预算、引言等配置。12 个输出逐表解析通过；没有自动获得这些字体的再分发授权。

13 Mesh 全在 unity default resources，全部导出 OBJ 且有顶点/面；没有目标作者的独立角色/场景三维模型。场景卡片/文字仍使用 MeshRenderer、MeshFilter 和碰撞体，因此不能从“采用三维组件”推断其是三维角色游戏。

没有 AnimationClip/Animator，Tween/摇摆/盖章/折叠/任务进度全部在业务类 Update 与动作队列中实现。一个 ParticleSystem/ParticleSystemRenderer 是卡牌效果模板，运行时复制后改颜色等参数；完整模块字段保留，未生成独立特效编辑器工程。

## 场景和脚本对应

462 GameObject 均有对应 Transform；18 根节点包含 Core、Camera、CardBase、CardProjectBase、CardSelector、Positions、Tutorial、Options、Buttons、Title、三个 Fader、退出和计数文本等。层级、父子、位置、旋转、缩放与组件列表见 `local/scene-hierarchy.json`。

46 MonoBehaviour 中 45 属于场景：Loader、AI、Options、CardManager、SoundManager、MusicManager、TitleManager、ProjectCard、PlayCard、Tutorial 各 1；Fader 3、TaskIcon 5、TextOutline 14、Rumble 7、Continue 2、Stamp 2、ScoreCard 2。另一个是内置 GUI 资源中的 GUISkin。29 MonoScript 包含场景脚本以及运行时创建/辅助类；不把 MonoScript 数量等同于已挂载组件数量。

`local/behaviours.json` 中每个组件有脚本容器/pathID、类名及全部序列化业务字段。声音数组、位置锚点、教程页引用和卡牌默认值均保留；通过指针可一路回溯到 Texture/AudioClip/Material/Shader 原始对象。

## 未恢复的信息

发布场景的完整对象可以分析，但未还原编辑器场景 YAML/Prefab 源组织、资源 GUID/.meta、PSD/音频工程/字体源、Unity 原生引擎源码。全部原始二进制在副本中保留；本轮没有运行资产重建后的 Unity 工程，也没有把导出素材复制到其它开发项目。
