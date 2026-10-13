# 输入、工具与来源登记

2026-10-13；用户明确指定 `E:\SteamLibrary\steamapps\common\Escape from Duckov` 并要求在当前仓库建立逆向项目。只研究该本机已安装构建，未从网络下载游戏内容。

## 输入身份

Info.ini：platform=Steam，version=2.3.30；app.info：TeamSoda / Duckov。Unity 容器实际版本 2022.3.62f2；Windows x64 Mono。全输入 466 文件、3,091,569,706 字节，清单/长度/SHA256 在 [build-files.json](../evidence/build-files.json)，汇总在 [build-summary.json](../evidence/build-summary.json)。不同于仓库制作基线编辑器 2022.3.62f3c1，未用本机编辑器版本替代目标版本。

原件只读，分析副本为项目 `local/build-copy`。全部原始 .dll/.assets/level/.resS/.bank/CSV/MP3 与附带文件均保存；源码/媒体为第三方内容，只在本机研究与无远程版本快照保留。未把用户要求解释为第三方内容再分发许可；本项目没有新增商用复用许可结论。

## 实际工具

Python 3.10.8（本机 C:\Python310\python.exe）；UnityPy 1.25.0；TypeTreeGeneratorAPI 0.0.10；texture2ddecoder 1.0.6；fmod_toolkit 0.1.3；Pillow、pyfmodex 实际版本见 [tools.json](../evidence/tools.json)。.NET SDK 9.0.309；ILSpy 9.1.0.7988。

本机既有工具缓存来自 `prototypes/re-crunch-time/local/tools/`，只复制解析工具，未使用 Crunch Time 的代码/内容/资产。复制到当前项目 `local/tools/ilspy`、`local/tools/python`；元数据读器源码在 scripts/metadata-index，使用 .NET 自行构建。工具输出、失败日志与修复记录保留。

方法参考：[UnityPy 官方说明](https://github.com/K0lb3/UnityPy/blob/master/README.md)、[ILSpy 命令行官方说明](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md)、[TypeTreeGeneratorAPI 官方项目](https://github.com/UnityPy-Org/TypeTreeGeneratorAPI)。这些文档支持工具选择和模式读法，具体游戏结论均取自本机目标构建。

## 原生及在线边界

9 个原生 PE 的元数据摘要见 [native-summary.json](../evidence/native-summary.json)，完整导入/导出本机 local/native-pe.json。原生媒体解码使用副本内 FMOD DLL，NOSOUND，未播放事件。未加载目标托管 DLL 执行游戏逻辑、未跑 Mods、未接触真实账号/存档，未调用平台 API 或绕过授权保护。

## 证据使用

evidence 是公开可备份的版本/数量/哈希/类型信息与原创检测结果；完整业务内容、游戏文本、反编译方法体、图片/音频/网格和安装包留在 local。静态发现、助手解释和未验证推论由报告逐项说明；内容登记和实际可达不同，构建版本后续变化须重新登记输入。
