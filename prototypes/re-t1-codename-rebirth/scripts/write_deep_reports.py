"""Write the reviewed 2026-10-13 static-analysis report snapshot.

This is prose, not a new verification run. Measurements come from evidence/.
"""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
REPORTS={
'analysis.md':'''# 代号：新生测试包逆向分析

2026-10-13，v0.4。用户已确认授权。本次完成指定 APK 的全量载体清点、主要封装解码、业务热更新反编译、配置恢复、资源对象索引与媒体导出。**原作者源码、原 Unity 工程和服务端不能由这个 APK 还原**；原生 AOT 声明、实际 IL、机器代码和反编译文本分别保存。

## 身份与输入

标签 `代号:新生(测试版)`，包名 `com.gnml.t1yfnbcs`，版本 `0.9.0` / `9000000`，Unity **2022.3.62f3c1**，IL2CPP ARM64 / ARMv7。入口为 com.zero.main.MainActivity，Application 为 FNCrashCatchApplication，min SDK 22、target 30；manifest 的 debuggable 不单独证明所有调试功能开启。[身份](../evidence/identity.json)

原 APK 1,532,660,866 字节，SHA256 `9ee8b8d033772495b477f9b984b1e6b30eaee8959359fbf3e7e5c3768a2a84ea`。2051 ZIP 条目及解包的 1,552,692,913 字节已做 CRC/哈希核对；分析使用校验副本，原件没有改写。[输入](../evidence/intake.json)、[文件清单](../evidence/apk-files.json)

## 实际恢复

- **托管代码**：全部 38 份 QXPM 镜像，即 debug/release 各 3 份业务热更新和 16 份 AOT 补充镜像。208 分片、163,613 加密块的摘要与 AES-GCM 检查通过，每个输出为有效 PE。38 工程生成 17,872 个 C# 文件，全部 IL 指令读取无失败。AOT 补充镜像多数方法没有 IL。[解码](../evidence/hotfix-decoding.json)、[覆盖](../evidence/deep-coverage.json)
- **IL2CPP**：QXGM 3145 页、摘要表和头 HMAC 通过，恢复标准 metadata v31；生成 85 份 AOT 声明 DLL、12,586 类型声明和 199,560 原生方法地址。方法体仍以 ELF 机器代码为权威，DummyDll 的空体不是恢复的 C#。[元数据](../evidence/qxgm-decoding.json)、[本机声明](../local/il2cpp-arm64/dump.cs)、[地址](../local/native-methods.csv)
- **配置**：QCFG v2 两个容器有 **208 表、30,676 条记录、2362 列、35,592 池字符串、7256 池对象**。所有池/表段、对象消费、列宽及主键闭合。207 行表与生成类型比较，只有 task_action 的代码字段 format 不在表中；example 类型没有包内表。[配置](../evidence/config-decoding.json)、[关联](../evidence/content-linking.json)
- **资源**：YooAsset 22,162 地址对应 1017 文件，全 MD5/CRC/大小匹配。981 自定义资源包全部恢复、解压并读出 **191,092 对象**，类型树没有失败；1077 TextAsset 按原字节导出。[包](../evidence/bundle-decoding.json)、[对象](../evidence/bundle-objects.json)
- **媒体**：3204 Texture2D 中，3201 有可导出数据，3 个字体图集为 0×0 且无像素。17 浮点数据贴图另存逐字节一致的 RAW/NPY，PNG 仅为归一化预览。6867 网格导出 OBJ gzip；728 AudioClip 保存原编码数据和解码音频；27 个 FMOD 库另导出 333 样本。蒙皮、形态键、材质、动画与混音原资料仍在包中。[媒体](../evidence/media-export.json)、[FMOD](../evidence/fmod-samples.json)
- **其它载荷**：64 JSON、14 碰撞文件的 49,987 形状、57 FMOD RIFF 银行结构已读。641 HCRH、48 SDF、2 token map 与 QFGS 泛型共享清单保留完整字节和格式登记，未声称已转换全部原生语义。[载荷](../evidence/payload-inspection.json)
- **Android**：28 DEX 的 SDK 完整指令反汇编与独立解析核对为 11,505 类定义、71,190 方法体、2,059,980 指令单元；重复定义保留来源。JADX 有 6719 常规 Java 文件与 11,502 个逐 DEX fallback 文件，常规恢复有 15 错误，原指令未丢弃。[Android](../evidence/android-recovery.json)
- **基础 Unity**：此前仅基类的 970 组件，967 已补齐；加上原成功对象，7468/7472 类型树完整。剩余 3 脚本组件和 1 BuildSettings 不兼容，原字节及诊断保留。[基础补齐](../evidence/base-recovery.json)

## 游戏结构与内容

客户端包含拟人动物角色、射击、物品搜集与撤离机制，以及战备、背包/仓库、配件改装、技能、部门任务、家园建筑/工作台、交易、组队匹配和回放。相应方法、协议、配置与界面资源可回查。[架构](architecture.md)、[逐系统入口](systems.md)

序列化热更新配置的值 1 对应 ReleaseHotfix，不能凭 APK 文件名的 debug 判定 DebugHotfix。Release 三个业务程序集共 6972 类型、56,814 方法定义，其中 55,733 含实际 IL；包含协议、绑定器和编译器辅助类。[代码地图](code-map.md)

EntityMgr 明确注册 52 ECS 系统。EcsWorld 顺序执行 OnUpdate/OnLateUpdate，在边界回放命令缓冲；射击、子弹、后坐力、散布、技能、Buff、移动与相机分别处理。匹配、背包、建筑、制造和撤离通过请求/回包更新状态，服务端最终规则没有因此恢复。

实际表中有 64 武器、77 弹药、147 配件、1025 通用物品、7 角色、14 地图、100 部门任务、74 Buff、67 技能和 7214 语言行。地图含宿舍、教学、靶场、六号机场与测试场景；记录存在不等于全部开放。按真实 LangMgr 哈希关联后，14 地图、7 角色及 64 武器名称全部对应。[内容与资产](content-and-assets.md)

## 精度与边界

业务源码使用完整 AOT 声明引用重反编译，保留正确属性/索引器语义。两套 DressableModelMgr 各有 4 条 ILSpy 类型诊断，共 8 条；原 IL、token 和指令可回查。未宣称反编译文件已通过 Unity 重新编译。

QGSG 与 QXTL 外层签名未独立验签；内部摘要、HMAC、分片/块 SHA 和 AES-GCM 标签已验证。没有修改签名、凭证或许可。未安装/运行 APK、登录、发送服务端请求、操作存档，也没有 Android 输入、性能、全帧视频或听感测试。[完整边界](recovery-scope.md)

25/25 深层字节、格式、覆盖与索引检查通过；本机检索的 15/15 检查通过，含源码跳转、中文配置、媒体预览、路径范围、快速切换及桌面/窄窗口布局。[深层校验](../evidence/deep-verification.json)、[页面 QA](../evidence/deep-explorer-qa.json)

所有原件及第三方派生产物留在 local/ 独立无远程 Git。公开仓库只备份原创报告/工具和汇总证据；源码、配置全文、媒体不公开上传。初查证据保留，与深层结果分别登记。

完整本地提交 `433faff` 保存 71554 文件 / 8.22 GB；C 盘独立备份的全部 Git 对象检查与原 APK blob SHA256 通过。F 盘研究文件保留原位，工作库借用 C 库对象，迁移版本历史须保留 C 库。[本地备份](../evidence/deep-backup.json)、[原创资料远程备份](../evidence/deep-publication.json)

## 使用

[本地只读检索](../scripts/serve_catalog.py)支持类型/源码、方法、调用位置、配置全文/中文名称、对象与媒体。启动后访问 http://127.0.0.1:8769/。[原离线地址页](../local/explorer.html)保留全部地址/依赖及初始载体索引。[复跑文档](../scripts/README.md)
''',
'code-map.md':'''# 代码恢复与阅读地图

2026-10-13。反编译结果不恢复原注释、历史或未打包源码。

## 业务 IL

local/managed/{debug,release}/hotfix 各有 ConfigDefine、GameCore、GamePlay。Release 分别有 427/956/5589 类型、1295/7771/47748 方法，其中 1273/7547/46913 含 IL。全部 38 镜像的 token、RVA、原签名、字段、参数、指令数量、调用位置和字符串在 local/managed-metadata/{profile}/{hotfix,aotmeta}。

主要阅读路径为 local/source/all/release/hotfix，debug 作为差异对照。两套 DressableModelMgr 各保留 4 条类型诊断；未执行 Unity 编译验收。[覆盖/诊断](../evidence/deep-coverage.json)、[系统入口](systems.md)

GameCore 提供 MVC/事件、配置、资源、UI、音频、网络、模型与对象池。GamePlay 包含 ECS、控制器/模型、UI View、Proto/Sproto、教程及回放；完整源文件和方法调用索引可本机检索。

## AOT 与 HybridCLR

每 profile 16 份补充镜像包含 Launcher、YooAsset、QXPlugins、FNSDK_Core、QXDebugSheet、com.qx.recorder.Runtime、LitJson、mscorlib、System/Core、UnityEngine Core/Animation/AndroidJNI/JSON/UI、URP Core。保留泛型 IL可反编译，多数普通方法为 extern/无 IL；每程序集的真实计数在证据中，不当成完整框架源码。

QXGM 已还原为 metadata v31，SHA256 41760e6f06eddb426a5e2de7fb95d8a6751b119e28f4b57045ba7dd250c663f4。Il2CppDumper 6.7.46 定位 CodeRegistration 0x475cfb0、MetadataRegistration 0x48debc8，导出 85 份 DummyDll、12,586 类型与 199,560 方法地址。135 个 ScriptingAssemblies 登记名与 85 个原生镜像是不同集合，不能相减当作解析失败。

local/il2cpp-arm64/dump.cs 是类型/字段/方法声明；script.json、il2cpp.h、native-methods.csv 用于原生定位。DummyDll 不含 AOT 实际方法体。ARM64/ARMv7 ELF 保存机器代码，未生成所有方法的高级伪代码。[官方工具边界](https://github.com/Perfare/Il2CppDumper)

已局部反汇编 QXGM 加载/流、ResourcePayloadMixer、QXPM 清单/索引/分块和 NativeOpenManifest。原创工具从原 ELF 读取构建参数，未公开硬编码私有密钥。HCRH、SDF、QFGS、QXTM 原资料完整保留，内部渲染/泛型算法未全面转换。

## Android 与原生载体

28 DEX 的定义、重复来源和完整指令在 local/dex、local/dex-disassembly；6719 常规 Java 在 local/android-java/sources，逐 DEX fallback 在 local/android-fallback。常规 JADX 有 15 个错误，empty DEX 无类提示按原类数识别，不计作损坏。[核对](../evidence/android-recovery.json)、[JADX 版本](https://github.com/skylot/jadx/releases/tag/v1.5.3)

Java/Kotlin 主要是 SDK、Unity 平台桥、支付、语音、上报和系统适配。全部 50 ELF 的头/节/依赖/动态符号已有记录，但闭源 SDK/Unity 中间件完整实现未恢复；没有调用身份、支付、反作弊或在线接口。

## 调用索引精度

SQLite 直接记录 IL call/newobj 位置。泛型 TypeSpec 部分名称以 token 保留，虚调用、反射、委托、网络消息及资源驱动跳转需结合方法体和配置；它不是完整动态调用图，也不证明所有模块玩家可达。
''',
'content-and-assets.md':'''# 内容、场景与资产

2026-10-13。配置记录、序列化对象和逻辑地址分别计数。

## 配置与文字

两份 QCFG 有 208 表、30,676 条记录；misc 是 628 键 KV 表，其它 207 行表共 2362 列。池、行跨度及索引精确消费。local/config/tables 保留完整数据；映射使用 $map 的键类型与 entries，不静默改变整数键。[配置](../evidence/config-decoding.json)

实际表有 64 武器（219 字段/行）、77 弹药、147 配件、1025 通用物品、41 背包、961 容器、3217 容器用途、35 装备、7 角色、151 怪物、67 技能、74 Buff、100 部门任务、28 建筑、55 建筑等级、4 工作台和 7214 语言行。武器表包含射速、弹速、装填、部位系数、距离衰减、散布、后坐力和瞄准等；客户端值不证明服务器最终规则。

LangMgr 对非数字键执行特定 double/UTF-16 哈希后读取 data_language.content。已关联 8030 配置字符串、87 表，全部地图、角色、武器名称对应；任意字段匹配仍是候选，不证明实际 UI 可见。icon_conf 有 1795 个图标/图集映射。[关联](../evidence/content-linking.json)、[本机全文](../local/config/resolved-content.json)

task_action 的代码声明 format 没有对应列；example 类型没有表。原状保留，不修改第三方数据，也不据此宣称运行故障。

## 地图与内容范围

14 条地图名称为宿舍、新手关、新手操作教学、新手操作教学2004、进阶教学1/2、水城（地编看效果）、六号机场、靶场、机场外围（PVE）、容器测试场景、TA测试场景、水城（策划白模）、新手关测试。测试/白模不算正式上线地图。

14 份碰撞数据共 49,987 形状，保留分组、位置/旋转、盒/球/胶囊/柱参数及邻接 ID。还有 14 NavMeshData、5 TerrainData。碰撞、导航和地图表的 ID 集合不完全相同，不强行一一对应。[载荷](../evidence/payload-inspection.json)

表内还有 22 地图任务、74 任务步骤、108 任务规划、60 撤离点、162 对话、134 引导、199 引导阶段和 9 进阶教程。未由名称补写未见剧情或声称所有触发条件已运行。

## 对象与媒体

981 包有 191,092 对象，含 47,282 GameObject、39,031 MonoBehaviour、6867 Mesh、3793 Sprite、3491 Material、3204 Texture2D、1283 AnimationClip、728 AudioClip、603 SkinnedMeshRenderer、452 Animator、191 AnimatorController、150 Shader、108 SpriteAtlas。[全类型计数](../evidence/bundle-objects.json)

全部包内类型树通过。39,030 脚本组件对应 131 种脚本身份，另一个 m_Script 为零。语义 JSON 对大数值数组做摘要；完整顶点/像素/序列化值在解码包中，缩略 JSON 不等于全字节。[对象](../local/assets/objects-enriched.json)

local/assets/export 有 3201 纹理输出、6867 OBJ gzip、728 原编码音频及解码样本。3 个动态字体图集为空；17 浮点贴图用 RAW/NPY 保全值，PNG 归一化预览不可回用于数值贴图。OBJ 不保留完整蒙皮、形态键、Unity 材质/场景变换，原包仍保留这些信息。

57 FMOD RIFF 库约 27.4 MB，结构闭合；27 库的 333 样本已导出 WAV。可能与 AudioClip 重复，不相加为独立音效总数。未验证事件/混音运行或听感。[媒体](../evidence/media-export.json)、[音频库样本](../evidence/fmod-samples.json)

641 HCRH、48 SDF 保留原生渲染/体素资料，未转换完整编辑器场景。16499 DrawKeys 地址用于渲染组织，不能算物品或关卡。全部地址/依赖仍在[离线地址页](../local/explorer.html)。

## 原生文件与预览

487 图片后缀文件，初查 483 可识别。两个相同 PNG 仅首字节损坏，全部 chunk CRC 正确；另存首字节修复副本并验证，原件未动。另两份 SDK 载荷不是可直接打开的 PNG。36 MP4 容器/轨道通过，逻辑地址 38，未全帧/听检。

基础 7472 对象现有 7468 完整类型树；Driver、UniversalRenderPipelineGlobalSettings、GUISkin、BuildSettings 的兼容缺口单列。[补齐](../evidence/base-recovery.json)

抽样纹理可见拟人动物、武器/配件、背包/HUD 图标，但不代替 Android 实际画面验证。第三方字体和媒体许可没有因此变更；本仓原创游戏未采用这些资产。
''',
'recovery-scope.md':'''# 恢复精度与未覆盖范围

用户 2026-10-13 回复“已获授权”，已满足本地封装/代码/资产研究前提，不解释为公开再分发或在线操作授权。原 APK 没有修改或运行。

## 已达到

全部 APK、DEX 指令、ELF 载体、清单及 981 包已清点；QXGM、38 镜像、208 配置表及 191,092 包内对象恢复并索引。业务 C#、调用位置、配置/文字、媒体、碰撞与 UI/资源可定位到字节、token、地址、表/行、path ID。无需重新从头解包。

## 保留的边界

- 原作者源码、注释、Git 历史、完整 Unity 工程、编辑器工作流、未打包内容、原 DCC 文件与服务端实现。反编译和 OBJ/PNG/WAV 不等同于原资料。
- 199,560 AOT 地址的全部高级伪代码、闭源 Unity/SDK/原生渲染算法。机器代码和类型声明完整保留；AOT 补充镜像只有部分泛型 IL。
- HCRH/SDF/QFGS/QXTM 的完整语义与编辑器转换；FMOD 的事件/混音运行。载荷原字节已保存。
- 基础 Unity 的 4 个不兼容对象、两套 DressableModelMgr 的 8 条局部类型诊断、JADX 常规的 15 个错误。原字节/指令与错误没有隐藏。
- QGSG 与 QXTL 外层签名未独立验签；内部摘要、HMAC、分片/块 SHA 和 AES-GCM 标签通过。未修改签名/许可。
- Android 安装/真实输入、联网内容、账号/存档、实际渲染、性能、全帧视频与听感。检索页 QA 不替代游戏运行。

完整包内静态研究已形成可追溯交付。上述边界不以虚构源码或服务器补齐；进一步原生算法阅读、运行研究应使用精确定位和真实条件，继续分别记录客户端、运行及外部证据。

## 保管

local/.git 是无远程的独立资料备份，保存校验 APK、解包/解码、原编码媒体、反编译及配置。公开分支仅原创报告/工具/摘要；本机链接不意味着公开再分发。历史初查证据与当前深层校验分别保留。
'''}

if __name__=='__main__':
    for name,text in REPORTS.items():(ROOT/'reports'/name).write_text(text,encoding='utf-8')
    print('Wrote reviewed static-analysis snapshot: '+', '.join(REPORTS))
