# Crunch Time! 全量静态逆向分析

版本 v1.0；2026-10-12；目标为用户本机 Steam AppID 340550、build ID 484621。安装位置：`E:\SteamLibrary\steamapps\common\Crunch Time`。本报告以安装字节、程序集 IL、序列化对象和 XML 为依据。

## 结论与阅读入口

这是一个 Unity 4.5.2f1、Windows x64、Mono 托管逻辑的单机卡牌游戏。玩家与 AI 各经营一家公司，随机取得一个游戏项目；通过五部门员工推进任务、增益加速、负面事件和总部干预阻碍对手，先达到全部任务目标的一方获胜。预算影响排行榜，不直接决定胜负。显示使用正交相机、SpriteRenderer 和 TextMesh，核心逻辑与卡牌动画、UI 状态紧密结合。

本安装版本的游戏程序集已全量反编译与索引；随包另外五个托管库也已全量反编译。所有 Unity 容器对象已取得原始字节、可读字段与依赖，内容与可解码媒体已整理。**恢复的是发布构建中的内容和反编译代码，并非原作者完整工程**：编辑器源场景、Prefab 组织、原始美术工程、脚本注释、被剥离代码和 Unity 原生引擎源码无法由这些产物完整还原。本轮没有运行游戏、进行正常键鼠游玩、主观试听或重新编译反编译工程。

- [架构、状态机、界面、AI、保存](architecture.md)
- [全部内容与有效规则](rules-and-content.md)
- [资产、场景、音画与导出覆盖](assets.md)
- [全部游戏类型和代码定位](code-map.md)
- [可搜索的本地目录](../local/explorer.html)：卡牌、项目、资产预览、完整代码符号、462 个场景对象与全部文本。
- [复跑方法](../scripts/README.md)；[来源/工具](../sources/target-register.md)；[合同](../DEVELOPMENT.md)。

## 完整性证据

原安装共 25 个文件、407,121,399 字节。复制后及分析结束后逐文件 SHA256 均一致；安装目录未写入。可追踪清单在 [build-files.json](../evidence/build-files.json)。

25 文件逐一归类为 2 原生 PE、6 托管程序集、5 Unity 序列化容器、10 Mono 配置/ASPX 辅助文件、1 启动 BMP、1 PlayerConnection 配置，见 [build-structure.json](../evidence/build-structure.json)。启动 BMP 432×163 可正常解码；框架配置保留原字节，不误认为游戏关卡或脚本。旧 browscap.ini 不是 UTF-8，以无损 Latin-1 字节视图检查，原始编码未确认，未转换原件。

游戏 `Assembly-CSharp.dll` 为 162,816 字节，包含 40 个元数据类型、663 个方法、959 个字段。30 个顶层业务类型及嵌套/编译器类型均纳入索引；643 个方法有 IL，20 个委托运行时方法没有 IL，解析 28,281 条指令，无 IL 解码失败。完整 C# 在 [local/decompiled](../local/decompiled/Loader.cs)，完整 IL 在 [Assembly-CSharp.il](../local/il/Assembly-CSharp.il)，可公开的符号/调用索引在 [code-index.json](../evidence/code-index.json)。没有发现游戏业务代码混淆。

所有六个托管程序集共 4,216 类型、41,320 方法、19,599 字段，其中 34,673 方法有 IL；共输出 3,641 个 C# 文件（含生成的 AssemblyInfo），10,960,339 字节。支持库是 Unity 的托管包装和 Mono/.NET 基础库，不把这些数量算作游戏原创业务代码。逐程序集计数、版本及引用在 [assemblies.json](../evidence/assemblies.json)。EXE 和 mono.dll 均为 x64 原生 PE；导入库与函数见 [native-index.json](../evidence/native-index.json)。托管 DLL 的 I386/ILOnly PE 标识不意味着本游戏进程是 x86。

五个 Unity 容器共 1,985 个对象，全部 TypeTree 解析成功，4,443 条非空 PPtr 引用全部闭合，46 个 MonoBehaviour 的脚本指针全部找到对应类。包括 462 GameObject/Transform、95 卡牌定义、28 项目定义、420 文本键和 74 个 Resources 键。三份全量对象证据：[containers](../evidence/containers.json)、[objects](../evidence/objects.json)、[dependencies](../evidence/dependencies.json)。

导出并实际解码验证 345 个非空文件：124 纹理、78 Sprite、91 音频、12 字体、13 内置网格、5 XML、22 Shader。全部非空导出文件有字节数和 SHA256。13 个动态字体空纹理和内置 Arial 空载荷显式登记；这 14 个对象仍有原始字节与序列化字段。材质、渲染器、碰撞体、粒子、设置等对象以原始字节/字段保存，没有以 PNG/OBJ 替代其原始信息。

19 项定向验证全部通过，见 [verification.json](../evidence/verification.json)。PNG 使用 Pillow 完整载入；91 音频使用 FFprobe 取样并 FFmpeg 全程解码；12 字体逐表解析，13 OBJ 顶点/面非空，5 XML 结构通过。媒体解码不是原生显示、音量混音、听感或玩法测试。

本地内容目录另通过 21 项浏览器检查，包括搜索/分类/分页、图片加载、音频元数据、原始对象/字段/C#/IL 链接、全部分栏数量和手机宽度；桌面/手机截图已目检，见 [explorer-qa.json](../evidence/explorer-qa.json)。截图含第三方图片，只在 local 保存。

## 主要发现

1. **项目字段存在明确不一致。** 28 个项目 XML 都配置 `Animation`；`Loader.LoadProjectCards` 只读取 `Testing`，且默认 2。因此全部项目测试任务实际为 2，16 个项目的有效值不同于 XML 的 Animation 数值。有效总任务数分布为 11 项×6、12 项×12、13 项×10；不能直接照抄 XML 当运行规则。
2. **规则推进由动画完成驱动。** 员工计时、任务条动画、队列等待与胜利判定互相连接。`ProjectCard.CheckProjectFinished` 在任务条更新完成后调用；迁移实现时若只复制外层回合枚举，会漏掉中间等待、反制及结算回调。
3. **卡组不是反复洗牌循环。** 每方独立复制全部 95 张定义，从剩余牌随机抽取并移除；从第二周起补到至少五张，已有五张仍加抽一张。当前代码未看到空牌堆保护或弃牌重洗，长局耗尽是静态识别出的待实机验证边界。
4. **排行榜收录赢家，包含 AI。** 不是只记录玩家胜利；预算为出牌+500、每次完成部门任务+1250。完成数量不被任务目标上限截断，重开任务不扣已有预算，再完成可再次得分。这是代码行为，未在原生对局中复现。
5. **发布包未实现当前对局存档或在线对战。** 游戏代码只有设置与十名排行榜的 PlayerPrefs 写入；统计保存入口为空。没有发现游戏程序集的网络/Steam API 调用。原生 Unity 和基础库仍附带联网能力，不能据此宣称整个进程绝不会联网。

关键定位和完整数值见分报告及 token 索引。所有“确认”均指本构建静态字节事实；运行表现和未提供的作者源工程单独保留边界。

## 产物保留方式

`local/build-copy/` 保存原安装副本；`local/decompiled/`、`local/decompiled-support/` 和 `local/il/` 保存全量代码；`local/raw-objects/` 和 `local/typetrees/` 保存每个对象；`local/exports/` 保存媒体；`local/content-catalog.json` 保存完整双语文本和内容关联。原始容器与 DLL 保留，便于纠正任何反编译/导出解释。

这些第三方代码和媒体由项目 `.gitignore` 留在本机。Git 备份只含原创报告、原创分析工具和必要的数值/符号/哈希索引。安装持有权限未被解释为对外再分发素材的许可，未对目标安装做修改或补丁。
