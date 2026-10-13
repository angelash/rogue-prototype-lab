# Escape from Duckov 逆向研究与 Unity 还原

输入：用户指定的本机 Steam **2.3.30**，Unity **2022.3.62f2 / Windows x64 Mono**。2026-10-13 已建立全量静态研究库、中文系统报告与本地检索工具；[开发合同](DEVELOPMENT.md)记录范围与实际落地。

最新路线使用本地 **Unity 2022.3.62f3c1**：反编译业务代码接入源码编译，资产继续沿旧实验宿主原生 AB/res 路线。当前工程为 **`local/HybridUnityProject`**、运行包为 **`local/HybridPlayer`**，详见[混合还原记录](reports/hybrid-restoration.md)。原始目标仍是全部内容、像素级一致；平台和全像素验收尚未通过。

## 当前目录怎么使用

- **`local/HybridUnityProject`**：当前混合源码工程。十个业务/配套程序集、1,430 C# 文件在 `Assets/Reconstructed`，已通过本地 Unity 编译构建。游戏资产在原生运行数据中，编辑器 Play 打开独立 Player。用 [start-hybrid-project.ps1](scripts/start-hybrid-project.ps1) 的 `-Editor` 参数打开，源码修改后执行 [build-hybrid-project.ps1](scripts/build-hybrid-project.ps1)。
- **`local/HybridPlayer`**：当前源码重编译运行包，保留旧宿主资源/依赖并替换本轮 Unity 编译的十个业务 DLL。新游戏、基地、保存重启、零号地区、伤害/撤离集成已实测；完整启动仍有 Steam 初始化错误，不能登记总验收通过。
- **`local/RestoredUnityProject`**：新增的编辑恢复候选。`Assets/Scenes` 有 63 个恢复场景，`GameObject` 为预制体，`Material`/`Texture2D`/`Mesh`/`AnimationClip`/`Resources` 等保存资产；`ProjectSettings` 登记 63 个构建场景。可用 [start-editor-project.ps1](scripts/start-editor-project.ps1) 打开。候选的实际导入/运行结论见还原记录，不能仅凭文件存在认定还原完成。
- **`local/UnityProject`**：原生 AB/res 实验与最小宿主构建工具，只有探针场景。保留供复跑，编辑器 Duckov 菜单可打开上述候选。旧目录中的 DLL 不代表源码化完成。
- **`local/AssetRipperExport/ExportedProject`**：未覆盖的原始恢复导出，35,815 文件。它是新编辑候选的输入，保留原导出数据以供核对。
- **`local/decompiled` / `local/il`**：反编译研究代码与完整 IL；共有 19,615 个 C# 文件，主要游戏程序集目录为 `TeamSoda.Duckov.Core`。当前混合版接入十个业务/配套程序集源码；其余引擎和第三方依赖保留匹配 DLL。一个 NodeCanvas 顶层类型只有 IL。
- **`local/Player`**：另外拼装的原生运行候选，沿原 `.assets`/流/场景布局加载。此前原标题运行通过的是它，不能替代编辑恢复工程验收。
- **`reports` / `scripts` / `evidence`**：原创分析、恢复工具和事实记录。`local/build-copy` 为只读分析输入；`Native*`、`StorageExperiments` 为试验产物与缓存。

原作者工程没有找回。当前十个业务/配套程序集已源码化，第三方/引擎依赖仍使用匹配 DLL；全游戏所有路径和全像素复刻未验收。

旧原生运行候选为 `local/Player/DuckovRestored.exe`，可用 [start-restored-game.ps1](scripts/start-restored-game.ps1) 复跑历史方案。当前混合源码版用 [start-hybrid-project.ps1](scripts/start-hybrid-project.ps1) 启动，保留 63 场景编号，使用独立保存目录；完整游玩和全像素一致尚未验收。第三方资产、截图和完整日志仅本机保存。

2026-10-13 已迁回 F 盘：Unity 缓存、本地版本库、构建/实验目录和当前运行包均位于本项目 `local/`，不再依赖 D 盘目录链接。64,999 文件 / 38,182,411,681 字节逐文件 SHA256 核验通过，D 盘原件保留为备份；详见[迁移证据](evidence/storage-migration.json)。

还原阶段完整本地快照见[备份证据](evidence/backup-restoration.json)：69,086 文件受版本管理，466 原始输入 blob 复核通过，恢复包另存 D 盘；生成缓存与运行日志按规则排除。原标题运行成功与完整游戏/像素验收分别记录。

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

反编译不是原作者源码；原生 Player 启动已通过，但完整可编辑 Unity 作者工程、所有方法逐行审阅及全游戏游玩仍未完成。静态阶段完整本地版本与报告/工具备份见 [备份记录](evidence/backup.json)，后续恢复状态以还原记录为准。

编辑恢复阶段完整本机快照见[备份证据](evidence/backup-editor-recovery.json)：110,508 文件 / 21,099,500,671 字节，466 原始输入 blob 与 Git 完整性核验通过；离线恢复包另存 D 盘，原备份保留。编辑器运行与完整游戏验收仍按上述实际结果记录。

混合源码版快照于 2026-10-14 完成，见[备份证据](evidence/backup-hybrid-restoration.json)：118,574 文件 / 25,453,817,573 字节，包含当前源码工程和可运行版本；466 输入 blob、Git 完整性及离线包校验通过。恢复包在 `D:\CodexBackups\duckov-hybrid-source-2026-10-13-local.bundle`；只有原创工具、报告和摘要备份到远程。
