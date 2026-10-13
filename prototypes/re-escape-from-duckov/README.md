# Escape from Duckov 逆向研究

输入：用户指定的本机 Steam **2.3.30**，Unity **2022.3.62f2 / Windows x64 Mono**。2026-10-13 已建立全量静态研究库、中文系统报告与本地检索工具；[开发合同](DEVELOPMENT.md)记录范围与实际落地。

## 先从这里读

- [总报告](reports/analysis.md)：覆盖、结论与阅读路径。
- [系统架构](reports/architecture.md)：启动/场景、角色、战斗、AI、物品、撤离/死亡、存档、任务、基地与平台边界。
- [规则与内容](reports/rules-and-content.md)：ID 到配置/实现的追踪、伤害/掉落/经济规则、完整内容目录口径。
- [资产分析](reports/assets.md)：场景结构、图像/网格/动画/Shader 与 FMOD。
- [覆盖与限制](reports/coverage-and-limitations.md)：C#、字段、媒体失败与未执行范围。
- [类型地图](reports/code-map.md)、[来源](sources/target-register.md)、[工具与复跑](scripts/README.md)。

## 本地检索

[研究目录](http://127.0.0.1:8784)提供代码、物品、任务、配方、角色、天赋、全部对象及音频搜索；点击对象编号查看字段、引用和导出。服务关闭后，用 [start-explorer.ps1](scripts/start-explorer.ps1)重新启动。监听仅为本机，数据库只读。

主要本机文件：`local/content-catalog.json`、`local/localizations.json`、`local/assets.sqlite`、`local/code-index.json`、`local/code-metadata/`、`local/decompiled/`、`local/il/`、`local/exports/`、`local/build-copy/`。完整游戏文本、源码派生和媒体留在 local，不进入公开远程。

## 实测覆盖与证据

466 输入文件逐文件哈希一致；201 程序集全部元数据/IL，核心业务 1,802 类型 / 11,804 方法；131 容器 / 1,601,352 对象，4,457,141 非零引用；内容登记 1,569 物品 / 315 任务 / 269 配方。支持媒体生成 10,151 文件，FMOD 568/568 样本与 373 事件。

[独立核验](evidence/verification.json)通过原字节、引用、登记与导出检查，[检索验证](evidence/explorer-qa.json)通过 91 API 检查及实际 UI 复核。一个 NodeCanvas 业务类型无 C#、两个字段对象受模式限制、七个 Shader 仅原载荷；任务 939 有一个原始空奖励槽。原始输入均保留，限制分别记录。

反编译不是原作者源码；此轮没有还原完整可运行 Unity 作者工程、逐行审阅所有方法或执行全游戏游玩。完整本地版本与报告/工具远程备份见 [备份记录](evidence/backup.json)。
