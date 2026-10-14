# 小小神兵 APK 静态逆向分析

版本 v0.3；2026-10-14（Asia/Shanghai）。用户已确认本地反编译与资产提取授权。目标为 **小小神兵 0.1.3 / 1000300**，包名 `com.gnml.xxsb.tt1`，主 Activity `com.zero.main.MainActivity`。原 APK SHA256：`4fb214bc2ed43a871ad2d35c4b2cae4cbe1125b649952795bd6a8e292f8fd07f`。

包内代码、配置、对象、媒体和依赖已经形成全量静态研究入口。核心系统经过实际代码阅读；可恢复 IL、声明、机器码及原制作源分开登记。当前成果不包含原作者 Unity 工程、服务器或真实游玩验证。

## 阅读入口

- [逐系统与玩法分析](systems.md)：启动、自走棋、经济、战斗、技能/伤害、指挥官/变体、教程/回放/重连、表现、存储和网络。
- [代码分层与索引](code-map.md)：实际业务 IL、AOT 补充、IL2CPP 声明、原生汇编和 Android。
- [内容与资产](content-and-assets.md)：全部配置、中文、对象/资源映射、媒体及空间数据。
- [恢复边界](recovery-scope.md)：类型树缺口、工具诊断、原作者工程、服务器和运行范围。
- [本地检索与复跑](../scripts/README.md)：源码、方法/调用、中文配置、对象和媒体的只读入口。

## 游戏与架构

实际代码确认主玩法为三国自走棋：准备商店、部署/合成、羁绊、指挥官/装备/变体、自动战斗与结算。战斗逻辑固定 125 ms 一步，表现插值单独推进。正式请求与本地教程的路由分开；不能从客户端计算结果推断服务器没有校验。

资源主版本为 Unity **2022.3.62f3**，一份内置默认资源另为 2022.3.54f1。metadata v31，IL2CPP 与 HybridCLR 共用：10 个业务 DLL 和 11 个 AOT 补充镜像实际存在。YooAsset 1.4.6 的 1,438 地址/484 清单包全部本地可用，另有启动 bundle；每包 MD5/CRC/大小及逻辑对象关联通过。

## 全量覆盖

- 原包 230,798,839 字节；全部 1,488 ZIP 条目 CRC/大小/SHA256 通过，解包 370,964,058 字节与原登记一致。[原件与副本](../evidence/preparation.json)、[解包](../evidence/extraction.json)。
- 21 托管镜像 / 10,096 类型 / 85,028 方法声明 / 53,427 IL 方法体，无 IL 解码失败；7,319 C# 阅读文件，其中业务 4,239。[托管恢复](../evidence/managed-recovery.json)、[反编译](../evidence/managed-decompilation.json)。
- 两 ABI 各 67 IL2CPP 声明程序集、133,186 条方法名称/地址；名称多重集合一致、地址均在可执行节内。20 ARM64 ELF 全可执行节 53,005,796 字节线性反汇编，13,250,506 条指令、3,772 字节 data 标记，全部恢复地址已标注。ARMv7 原件、声明及地址映射保留。[原生](../evidence/native-disassembly.json)、[双 ABI 核对](../evidence/native-abi-comparison.json)。
- 35 原标准 DEX + 1 恢复嵌入 DEX，合计 17,417 类定义 / 15,014 唯一类 / 114,259 方法体 / 3,169,066 指令单元，SDK 独立计数一致；8,114 标准与 69 嵌入常规 Java、17,411 fallback 文件。[Android](../evidence/android-recovery.json)、[嵌入恢复](../evidence/embedded-dex-recovery.json)。
- 485 bundle 全解压，59,312 对象；59,311 完整类型树、1 内置 GameSkin 待解。235,902 已解析引用全部闭合，1,438 逻辑资产全部关联。[类型树修复](../evidence/asset-repair.json)、[关联](../evidence/content-linking.json)。
- 150 配置项：146 行表、2 KV、2 空表头占位；10,495 行、130 KV、1,447 列、8,257 池引用、2,399 语言行和 1,022 中文关联。[配置](../evidence/content-summary.json)。
- 3,388 媒体对象全处理，无失败：1,501 纹理、1,004 精灵、230 网格、215 音频、190 动画、114 Avatar、122 Shader、6 ComputeShader、3 Font；另 3 空运行时纹理未伪造图像。媒体导出 395,394,328 字节。[媒体](../evidence/media-export.json)。
- 92 TextAsset 原字节全部保存/分类；24 JSON、24 SDF 块、12 A* 块、5 A* 包装/索引、21 PE、3 FMOD RIFF、3 其他二进制。[载体](../evidence/payload-summary.json)。

## 验证与保存

全部第三方原件、恢复代码、配置全文和媒体保存在项目 `local/`。本轮没有安装/运行 APK、联网、修改/重签、移植或重建，也没有真实输入、画面、听感或性能结论。格式检查和代表导出图目检不替代实机验收。

准备阶段[旧报告](preparation-v0.1.md)、[准备证据](../evidence/preparation.json)和[原始本地提交](../evidence/backup.json)保留为历史；深入校验、检索 QA 与完整备份在项目 README 链接当前记录。
