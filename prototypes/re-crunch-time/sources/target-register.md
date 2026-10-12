# 来源登记

- 原产品：Crunch Time!；用户提供安装目录 `E:\SteamLibrary\steamapps\common\Crunch Time`。
- 读取日期：2026-10-12（Asia/Shanghai）；筛选理由：用户明确指定完整分析该本机安装版本。
- Steam manifest：`E:\SteamLibrary\steamapps\appmanifest_340550.acf`。仅保存产品/构建字段，不复制账号和本机客户端字段。
- AppID：340550；本地 build ID：484621；目录名称：Crunch Time；记录的大小：407121399。
- EXE 文件版本：4.5.2.157915；主资源/构建 Unity 4.5.2f1，内置默认资源 4.5.0f1；EXE/mono.dll 均为 x64 PE。
- 权限边界：用户授权本地分析；没有第三方源码/媒体再分发声明，派生内容仅本机保存。

## 方法参考

- 本机技能：`C:\Users\Lenovo\.codex\skills\unity-reverse-engineering-skill\SKILL.md`，已读 analysis-checklists、tooling-guide、report-template。
- [ILSpy 官方 CLI 文档](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md)，2026-10-12 查询。
- [UnityPy 官方文档](https://github.com/K0lb3/UnityPy/blob/master/README.md)，2026-10-12 查询；实际 API 以本机包与帮助为准。
- [TypeTreeGeneratorAPI 官方源码](https://github.com/UnityPy-Org/TypeTreeGeneratorAPI)，2026-10-12 查询；实际使用 0.0.10。旧 4.x 基类对齐以本包基础 TypeTree 和脚本指针独立复核。

## 事实与限制

25 个安装文件和副本全部 SHA256 一致，清单在 [build-files](../evidence/build-files.json)。游戏 Assembly-CSharp 全量 C#/IL，另外五个支持库全量 C#；对象/依赖/内容/345 非空导出全部验证，详见 [总报告](../reports/analysis.md) 和 [工具版本与流程](../scripts/README.md)。目标玩法以本构建代码、场景和 XML 分别核对，没有从商店简介或通用玩法介绍替代分析。

Steam manifest 中的账号/客户端信息不保存到项目，BuildSettings 的 token 在可读本地字段中脱敏，公开索引不含完整设置；原始构建副本/对象仅在 Git 忽略 local 内保存。未修改安装、未读取用户存档、未调用游戏网络服务、未上传派生第三方内容。
