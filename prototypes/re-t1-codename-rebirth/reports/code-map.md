# 全包代码载体地图

覆盖全部代码载体与可读取的符号范围；不是全部方法体恢复完成声明。

## Android DEX

28 文件，11,505 类定义 / 11,482 唯一类名，75,797 定义方法、46,116 字段、71,190 方法体。完整签名、继承、字段/方法 ID、code offset、指令单元长度在[本地逐 DEX 索引](../local/dex/)，[总表](../evidence/dex-summary.json)可核对。23 重复类名保留 DEX 来源，未合并成虚假单一程序集。[重复记录](../local/dex-duplicates.json)

大类群为 FNSDK 1571、TapTap 1338，以及 Android/Kotlin、支付、语音、崩溃上报和平台接口。它们是包前缀类定义计数，不能当作游戏自身代码总数；本轮未生成 Java/Kotlin 源码或完整指令反汇编。[结构](../evidence/structure-summary.json)

## Unity 与热更新

ScriptingAssemblies 登记 135 名称，IL2CPP 包没有同名完整托管 DLL 供直接反编译。4352 MonoScript 描述去重为 4276 类型，涉及 63 程序集名，是完整类型集合的可见子集。[全部身份](../local/code-symbols.json)

- GamePlay 1930 描述 / 1929 唯一，业务/ECS/UI/协议线索。
- GameCore 435 / 431，资源/配置/UI/声音/网络及适配。
- ConfigDefine 215 / 215，其中 209 个 data_* 表类型，其余为绑定器等。
- Launcher 47 / 46，加载、资源补丁、平台适配及检测框架。

debug 127、release 121 个热更新地址，含 qxps/aotmeta 候选与 HotfixRuntimeConfig，仍在非标准 bundle 内。没有恢复实际程序集名、运行分支或方法体。[候选摘要](../evidence/content-summary.json)、[全资源地址](../local/manifest-assets.json)

## IL2CPP 与 native

50 ELF，两个 ABI 各 25，全头/节/动态依赖/符号记录见[native 总表](../evidence/native-summary.json)及[完整索引](../local/elf/)。重要载体是 ARM64 80,707,384 字节、ARMv7 69,421,588 字节的 libil2cpp.so。ARM64 的 2709 个有名动态符号含大量 C++/IL2CPP/Mono 兼容接口，不是 2709 游戏方法；无 symtab/debug 节。

global-metadata.dat 13,081,444 字节，QXGM / 2 头。没有解封装、恢复完整 type/field/method token、绑定 native RVA、建立完整调用图或生成 native 伪代码。[原文件证据](../evidence/apk-files.json)

## 精度与接续

可见 Unity 类型反查原文件/path ID，DEX 方法反查文件/方法 ID/code offset。完整游戏业务方法、IL2CPP RVA 与热更新映射仍缺失。权限范围明确后从加载、配置和战斗入口聚焦，再覆盖全部载体，不将未解析内容标为反编译成功。
