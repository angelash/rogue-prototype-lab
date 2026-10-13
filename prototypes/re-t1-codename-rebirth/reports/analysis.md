# 代号：新生测试包分析报告

2026-10-13（Asia/Shanghai）。范围为指定 APK 的结构与可直接读取的客户端元数据；**完整业务代码、规则数值与主要资产解码尚未完成**。自定义封装深入解析的授权范围已请求确认，当前没有收到确认答复。

## 版本与来源

Android 标签 `代号:新生(测试版)`，包名 `com.gnml.t1yfnbcs`，版本 `0.9.0` / `9000000`，minimum SDK 22、target SDK 30，ARM64 / ARMv7。Android manifest 允许调试；这不能单独证明 Unity Development Player 或所有调试入口均已启用。[身份](../evidence/identity.json)

原文件名 `t1_compat_debug-0.9.0-x-202610131448_1782199009743830-1-4399--yf_test.apk`，1,532,660,866 字节。原件与隔离副本 SHA256 均为 `9ee8b8d033772495b477f9b984b1e6b30eaee8959359fbf3e7e5c3768a2a84ea`。2051 条目 CRC 与解包大小全部通过，解包总字节 1,552,692,913，原件大小/修改时间未变化。[intake](../evidence/intake.json)、[全部文件哈希](../evidence/apk-files.json)

Unity **2022.3.62f3c1** 来自 `globalgamemanagers` 与 `level0` 文件头。Android 入口 `com.zero.main.MainActivity`，基础 `level0` 有 `Launcher` 与 `SceneRoot`；没有恢复或打开原 Unity 工程。[容器](../evidence/readable-containers.json)、[对象](../local/readable-objects.json)

## 实际完成与覆盖

- APK：全部路径、大小、CRC、SHA256、头部与工作副本。
- YooAsset：普通清单 2,669,735 字节与扩展清单 666,054 字节全部读完；22,162 唯一地址/路径逐项对齐，1,017 文件全部存在且大小吻合。所有地址及登记依赖在 APK 内。[摘要](../evidence/manifest-summary.json)
- DEX：28 文件，11,505 类定义 / 11,482 唯一类名，75,797 定义方法、46,116 字段、71,190 方法体位置、2,059,980 个 16-bit 指令单元。SHA1 / Adler32 全部匹配。完成签名/继承/位置索引，未完成 Java/Kotlin 反编译或完整指令反汇编。[摘要](../evidence/dex-summary.json)
- ELF：50 库，ARM64 / ARMv7 各 25，全部文件头、节、动态符号/依赖索引。`libil2cpp.so` 无 `.symtab` / `.debug*`，不能把运行时导出当作全部游戏方法。[native](../evidence/native-summary.json)
- 可读 Unity：146 容器、7472 对象；6501 完整类型树成功，971 不完整，其中 970 MonoBehaviour 只读取标准基类，1 BuildSettings 不兼容。[摘要](../evidence/readable-asset-summary.json)、[失败](../local/readable-errors.json)
- 脚本身份：4352 MonoScript 描述、4276 唯一类型、63 程序集名称。GamePlay 1930 描述 / 1929 唯一，GameCore 435 / 431，ConfigDefine 215 / 215，Launcher 47 / 46。这是可见脚本描述集合，不是全部 IL2CPP 类型。[身份索引](../local/code-symbols.json)
- 原生媒体：487 个图像后缀文件中 483 通过 Pillow 格式检查，4 不能识别；36 独立 MP4 的 FFprobe 容器/轨道检查通过。未做全部帧解码或听感/游戏美术验收。[图片](../evidence/raw-images.json)、[视频](../evidence/raw-videos.json)

## 游戏组织与结论精度

业务主要在 Unity 的 GameCore / GamePlay / ConfigDefine 与热更新层。DEX 主要是 FNSDK、TapTap、支付、Android/Kotlin、语音和平台接口，读取全部 DEX 不等于取得业务代码。注册清单有 135 程序集名称，含 HybridCLR、YooAsset、URP、HybridRenderSystem、FMOD、GPU Skinning、TerrainVT、QX 插件及测试模块；登记不证明全部功能已运行。[结构](../evidence/structure-summary.json)、[代码摘要](../evidence/code-summary.json)

脚本和资源名称显示战斗 ECS、武器/弹药/技能/Buff、AI 移动、背包/仓库、撤离结算、组队匹配、家园、工作台、交易、任务、回放与教程。ConfigDefine 中有 209 个 `data_*` 表类型名，**没有读取表行与数值**，不能宣称伤害、掉落、经济或关卡规则已经完整还原。[架构](architecture.md)、[代码地图](code-map.md)

16,499 地址属于渲染 DrawKeys（约 74.45%），不能计为道具。主要其它组包括场景 1975、UI 预制体 841、角色/装备 502、特效 545、音频目录 730。地址、物理文件、序列化对象和玩家内容是不同单位。[内容与资产](content-and-assets.md)

## 未完成范围

1. **IL2CPP**：13,081,444 字节 `global-metadata.dat` 头为 `QXGM` / 2，不兼容标准 metadata。ARM64 80,707,384 字节、ARMv7 69,421,588 字节的 `libil2cpp.so` 尚未绑定完整业务类型/方法地址；没有生成虚假 dummy DLL 或方法体。
2. **热更新**：debug 127、release 121 地址，含 `.qxps.bytes` / `.aotmeta.*` 和 `HotfixRuntimeConfig`。尚未解析，不视为标准 DLL，不认定运行分支。
3. **主要配置/资产**：981 非原生资源文件不是标准 UnityFS 头，清单加载方法 3；普通 UnityPy 初读零对象记为未解析。config_byte_0/1、地图/碰撞/导航、模型/贴图/动画及 FMOD 包内数据待恢复。
4. **原作者源码/工程**：APK 不保证携带编辑器源码、注释、历史、原始 DCC 文件和未打包资源；反编译结果也不等于原作者源码。
5. **运行/在线**：未安装或运行 APK，未登录、调用服务端、下载更新、操作账号/存档、验证真实输入、性能或听感。当前资源清单全在包内不能证明在线内容也完整。

完整任务仍未完成；权限明确后先研究封装/加载链，再关联代码、数据表和全量资源对象。反作弊、身份/支付校验、凭据与在线服务不属于本轮测试对象。[接续范围](recovery-scope.md)

## 交付

[本地检索](../local/explorer.html)提供全部地址/依赖、脚本身份、Android 类和包文件的搜索、筛选、分页与详情。[复跑工具](../scripts/README.md)、[校验](../evidence/verification.json)保存实际范围。浏览器索引验证与 APK 原生测试分别登记。

完整 APK、解包字节、标准 DEX/ELF 元数据与基础 Unity 类型树仅本机 `local/` 保留并独立版本备份；原创报告、工具与必要摘要进入本仓版本管理，不公开上传第三方 APK、代码和媒体。
