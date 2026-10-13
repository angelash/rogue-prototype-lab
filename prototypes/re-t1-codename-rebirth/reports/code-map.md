# 代码恢复与阅读地图

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
