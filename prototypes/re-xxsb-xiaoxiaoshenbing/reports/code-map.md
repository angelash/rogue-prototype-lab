# 代码分层与索引

日期：2026-10-14。第三方代码/指令/字符串全文仅本地保存；声明、实际 IL、机器码和反编译文本分别登记。

## 实际业务 IL

10 DLL 为 ConfigDefine、ConfigDefine.Generated、GameCore、GamePlay.Config、GamePlay、GamePlay.Proto、GamePlay.ShareDefine、GamePlay.BattleRobot、GamePlay.BattleRobot.Companion、Launcher.Hotfix。合计 51,291 方法声明、50,738 IL 方法体。基础 MVC/资源/输入、配置、自走棋/战斗、生成协议、机器人和启动桥都有实际方法体。

- [原 TextAsset PE](../local/code/input-hotfixes)与[按 assembly identity 归一化副本](../local/code/references)。
- [全部类型/字段/方法/签名及 IL 调用/字段/字面量](../local/code/index)：只读 PE，未执行目标程序集。
- [C# 阅读工程](../local/code/decompiled)：业务 4,239 文件；21 镜像共 7,319 文件，全部 ILSpy 退出 0，未重新编译。
- [业务分组统计](../local/code/system-map.json)、[人工系统阅读](systems.md)。全索引覆盖与逐方法人工阅读分开。

## AOT 与 native

11 AOT 补充镜像为 FNSDK_Core、LitJson、mscorlib、QXPlugins、System.Core、Unity.RenderPipelines.Core.Runtime、UnityEngine.AndroidJNIModule、UnityEngine.CoreModule、UnityEngine.JSONSerializeModule、UnityEngine.UI、YooAsset。保留 2,689 IL 方法体，其余补充声明不冒称全部原生高级算法。

metadata v31 与 ARM64 ELF 配对，自动找到 CodeRegistration/MetadataRegistration。[dump.cs](../local/il2cpp/arm64/dump.cs)、[script.json](../local/il2cpp/arm64/script.json)和[DummyDll](../local/il2cpp/arm64/DummyDll)保留 67 镜像声明与 133,186 方法地址。DummyDll 的空/默认方法是工具占位，不作为真实 IL。工具“may be protected”通用提示和后续自动定位成功均在日志保留；原二进制未修改。

[ARMv7 映射](../local/il2cpp/armv7)同样实际恢复 67 声明镜像和 133,186 地址；[双 ABI 独立核对](../evidence/native-abi-comparison.json)检查方法名多重集合一致及两个平台地址均在可执行节中，不把一个平台的偏移套到另一个平台。

[20 ARM64 库线性汇编](../local/native/arm64)覆盖全部 43 可执行节，已标注全部恢复 IL2CPP 名称；指令与 skip-data 分开。节范围及地址映射不等于独立函数长度、完整控制流图或原 C++。40 ELF 的节/符号/依赖在[ELF 索引](../local/elf)，两 ABI 原件在[解包库](../local/apk/lib)。

## Android

35 标准 DEX 的 SHA1/Adler32 通过；17,317 类定义含重复 SDK，14,935 唯一类名。SDK 独立核对 113,830 方法体和 3,157,749 个 16-bit 指令单元。

- [DEX 元数据](../local/dex) / [原字符串](../local/dex-strings)。
- [常规 Java](../local/android/java)：8,114 文件；重复定义、内部类合并和优化使文件数不等于类覆盖数。
- [逐 DEX fallback](../local/android/fallback)：17,311 文件；[SDK 全指令](../local/android/disassembly)保留全部原始定义。
- [诊断](../local/android/diagnostics.json)、[manifest 结构](../local/android/manifest-xmltree.txt)、[组件](../local/android/components.json)。

JADX 常规 10 节点错误和未知类引用保留；非空逐 DEX fallback 全退出 0，空 DEX 单列。`assets/39285EFA.dex` 原包装已恢复，新增 100 类、429 方法体、69 常规 Java 和 100 fallback 文件，完整签名/校验和及 SDK 指令计数通过。[恢复证据](../evidence/embedded-dex-recovery.json)。原 35 DEX 证据保留，合计 36 DEX / 17,417 类定义 / 15,014 唯一类 / 114,259 方法体 / 3,169,066 指令单元。当前没有原作者工程/服务器/重新构建完成证明，见[恢复边界](recovery-scope.md)。
