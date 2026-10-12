# Unity 恢复和适配

日期 2026-10-12；Unity 4.5.2f1 Player → 已安装 Unity 2022.3.62f3c1 Windows x64 Mono。本机来源安装与逆向快照只读。公开工具自己编写，恢复业务实现位于被忽略的本机工程。

## 工程结构与 prefab

local/UnityProject/Assets/Recovery/Scripts 保存 30 份原业务 C# 和薄兼容桥；Media 是私有生产 XML、夜市 PNG 与原音频；NightFonts 保存 13 份与原字号配置对应的 Noto CJK 字体；Scenes/NightMarket.unity 是唯一构建场景；Prefabs 保存 18 个场景根。Assets/Editor/RecoverySource/manifest.json 只在本机保存原类型树和引用。

全部 462 GameObject / Transform 恢复父子、444 个非根兄弟序、位置、旋转、缩放和功能组件，合计 1,063 槽。156 TextMesh、154 SpriteRenderer、177 MeshRenderer、23 AudioSource、23 MeshFilter、10 MeshCollider、9 BoxCollider2D、相机和粒子等沿原引用恢复。

18 根覆盖 462 节点：00 Core 23、01 Positions 183、03 Camera 1、20 CardBase 27、25 CardProjectBase 78、30 CardSelector 3、50 Options 17、50 Buttons 5、60 Title Background 5、60 TitleManager 2、61 Fader 1、62 SubFader 1、63 OptFader 1、64 Count 1、64 Count 2nd 1、65 Exit 1、65 Exit 2nd 1、90 Tutorial 111。它们是源场景根导出的编辑 prefab；原 Editor prefab 连接不在 Player 中。

## 声明的兼容差异

1. collider2D 改为 GetComponent；Screen.showCursor 改为 Cursor.visible；旧投影阴影属性改为对应枚举；Application.isLoadingLevel 用同步场景状态桥；Application.LoadLevel/loadedLevel 改用同场景 SceneManager 加载。
2. Unity 4 将 ScriptableObject 作为对象槽序列化的七处用 LegacyObjectSlot 持有，业务对象仍创建原 ScriptableObject 类。LegacyObjectSlot 独立脚本文件拥有有效 MonoScript GUID。
3. Loader -150、ProjectCard 100 的源执行顺序恢复，避免语言与模板尚未初始化。初始 TextMesh 的查表键保留；只改 XML 值。六个教程静态示例标题不参与查表，按明确 ID 替换为夜市名称。
4. Unity 新建 Sprite 的 FullRect/UV 行为不等于源紧网格。恢复 78 个 Sprite / 970 个顶点和 indices、原 UV、枢轴、rect、pixels-per-unit，修复序列化网格后重新导入。
5. 原桌面位于远裁剪边界，新相机 farClip 增加 0.001，保留可见桌面，其余源相机参数不变。Physics 与 Physics2D 开启 autoSyncTransforms，保留移动后立即射线查询。
6. 48 材质用源 shader 名找到 Unity 2022 内置对应，保留材质属性、引用和 PIXELSNAP 等关键词；没有用统一 shader 替代所有材质。
7. 原六档质量映射到 Unity 2022 字段，Windows 默认档 0，最高档 AA2/vSync0；源 ambient 0.2、无 skybox/雾。原 blendWeights/textureQuality 映射新字段。
8. 原公司名八项数组只替换展示字符串，保留长度与 Random 调用。Option.ReloadText 的 Sound 仅显示为“声音”/Sound，避免岗位“招徕”的共用键误标声音开关；选项标识、检测和保存逻辑不变。六个教程静态示例名按 ID 替换。全部数值、AI、回调和原 bug 行为维持。旧粒子属性通过薄桥绑定当前模块，没有新写粒子驱动规则。

完整源码对照和允许差异 hash 在 [fidelity-verification.json](evidence/fidelity-verification.json)。跨版本引擎光栅化、字体、音频、随机数和输入底层不由源码对照证明。

## 从本机快照复现

已有可用工程不需再次执行准备脚本。重建会重置私有参考输入，必须按完整次序操作并保留先前备份。

1. 从工程根执行 python -X utf8 prototypes/proto-yokai-night-market/scripts/prepare_fidelity.py，要求已授权原作分析快照。
2. 执行 create_fidelity_theme.py 生成夜市 XML、公司池、字体与初始文本键；已有 Resources XML 同字节同步，保留 .meta，新增工程由 Restore 建立逻辑键。现有精修提示词不覆盖；新任务 SubTitle 分类需保持 title，不用旧 v1 规格。
3. 用已安装 Unity 的 batchmode 执行 RecoveryEditor.Restore；读真实日志和 structure-audit.json。
4. 执行 scripts/restore_quality.py，映射源六档；再执行 NightAssetExport.Export，导出所有已登记 AI 原图到源尺寸并覆盖工作 PNG，保留 .meta。
5. 运行 verify_fidelity.py；执行 RecoveryEditor.BuildRelease，获得完整 Windows 目录与 release-build-report.json。
6. QA 单独用 BuildQAFinal / BuildQAVisual，额外编译 NIGHT_MARKET_VALIDATION，隔离产品保存身份；正式包中不编译这些驱动。

scripts/run_local_qa.ps1 串行运行两画幅渲染、生产规则、两画幅启动回合和加速整局；拒绝覆盖既有证据并检查报告/截图时间。包装图单独修订可用 EvidenceSuffix 与 VisualAndMatchOnly 复验渲染及结算，保留此前同规则证据；提交记录必须说明实际使用的范围。这个脚本不能替代原生输入验收。

真实命令日志、包锁、.meta 和 ProjectSettings 纳入本机源备份，Library/Temp/Logs/obj 缓存不纳入。准备脚本和恢复工具不能代替后续 Player 测试。
