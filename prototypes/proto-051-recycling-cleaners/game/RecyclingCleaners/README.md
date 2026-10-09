# 回收保洁队：Unity首次可玩候选

版本：v0.2；日期：2026-10-10（Asia/Shanghai）。Unity2022.3.62f3c1，目标Windows x64普通Mono Strict/PC单机。用户授权逐个项目开发，本轮助手采用项目合同的首次纵切范围，只做051；候选已实现，Prepare成功、EditMode28/28通过，最终构建与相机证据已归档，原生操作和听感待验。分项结果见 [开发记录](../../../../docs/development/proto-051/2026-10-10-slice-01.md)。

## 范围与所有者

一个6×6离散房间、三类污物、两类回收料、小/大桶、窄/广刷头和有限滤芯/剂量。三维斜俯视为助手表现建议，网格/位置/覆盖/容量与清洁真值归Core；原二维俯视设计保留。题材“清晨回收小队：把废料留给下一次清洁”，无成长商店/多房间/元素战斗。

- [项目规范](../../DEVELOPMENT.md)、[实施方案](../../../../docs/development/proto-051/implementation-plan.md)、[操作合同](../../../../docs/development/proto-051/input-and-ui.md)。
- 原依据：[需求v0.1](../../../../docs/design/first-batch/proto-051-recycling-cleaners/requirements.md)、[设计v0.1](../../../../docs/design/first-batch/proto-051-recycling-cleaners/design.md)。
- 规则/初值由 [Rules.cs](Assets/RecyclingCleaners/Core/Rules.cs)、命令/快照由 [Session.cs](Assets/RecyclingCleaners/Core/Session.cs) 独占，已只读核对规则 `0.2.0` / 快照格式1；[Core README](Assets/RecyclingCleaners/Core/README.md) 已记录两布局/两容量完整黄金输入。UI/场景/存储/测试不另算收益或清洁。

## 本地入口

在仓库根PowerShell使用已安装Unity：

```powershell
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-051-recycling-cleaners/game/RecyclingCleaners -Mode Prepare
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-051-recycling-cleaners/game/RecyclingCleaners -Mode Test
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-051-recycling-cleaners/game/RecyclingCleaners -Mode Build
```

已只读核对Editor入口 `PrototypeBuild.Editor.ProjectBuilder.Prepare` / `BuildWindows`；Prepare只在不存在时创建 `Assets/RecyclingCleaners/Scenes/Bootstrap.unity` 并设置本工程构建清单，不覆盖已有场景，同时确保 `Resources/RuntimeDefault.mat` 保留运行时Standard shader引用。Test是本项目EditMode。日志/XML在仓库 `.local/unity/RecyclingCleaners-<时间>-<模式>/`，`BuildOptions.StrictMode`普通Mono包位于 `Builds/Windows64/RecyclingCleaners.exe`、Data/Mono等配套目录及 `build-summary.json`；无Development调试选项。Player需完整目录。

本次Prepare日志 `.local/unity/RecyclingCleaners-20261010-005354-316-Prepare/unity.log` 成功；最终真实Test XML `.local/unity/RecyclingCleaners-20261010-010508-736-Test/tests.xml` 为28/28 Passed、0失败（22项Core规则+4项实际RoundSnapshot故障存储+2项Runtime帧边界），见 [归档报告](../../../../docs/development/proto-051/evidence/test-results.xml)。最终包构建0错1警，`-nographics` 未更新AmbientProbe/ReflectionProbe的诊断保留；大小、UTC时间、摘要/逐文件SHA256见 [构建清单](../../../../docs/development/proto-051/evidence/build-manifest.json)，不在多处重复最终字节数。

`-captureOnce` / `-captureDirectory <目录>` 的相机诊断只渲染镜头，不提交玩法命令、不证明HUD/原生输入/实际听感。真实测试/构建/相机由 `scripts/record-project-evidence.py --project 051 --tests <真实XML路径> --capture <真实图路径>` 归档，当前 [镜头图](../../../../docs/development/proto-051/evidence/scene.png) / [诊断事件](../../../../docs/development/proto-051/evidence/input-events.log) / [原生欠证](../../../../docs/development/proto-051/evidence/native-qa.json) 均已存在。已存在工程不为重试重新运行初始化脚本。

## 操作与规则读图

已只读核对 [CleanerRuntime.cs](Assets/RecyclingCleaners/Presentation/CleanerRuntime.cs) 的绑定，真实原生验收尚未执行：

- WASD正交移动（W/S为y+1/y−1，A/D为x−1/x+1），移动后目标跟随角色；方向键或场景鼠标只选格。Core0–5→UI1–6，补给Core `(0,0)` 显示 `(1,1)`。
- Space吸碎屑，F擦水/顽渍，G用清洁剂；1/2换窄/广头，T滤芯开关，V手动转换。
- E在供给点出售两类全量，X丢普通整类/Z丢可转化整类，Q恢复耐久；位置/材料/行动/耐久不满足时Core拒绝。
- F6保存、F9恢复、R立即重试；H最近十条事件，Y或面板右上按钮切“仅关键/含路线”（后者含移动）；Tab帮助、M音乐开关。
- F5保存 `Application.persistentDataPath/player-window.png`；Esc先关闭帮助/复盘/退出面板，否则显示保存退出确认，Save失败不关窗口。

无连续自动模拟，帮助/复盘互斥且与退出阻挡普通命令；复盘切路线纯UI、不花行动/耐久。同步命令门控与IMGUI键消费防重复须实际焦点/连按核对。顶栏小/大桶、近/远布局对照立即重置、滤芯初始关闭；首个合法新动作前可F9旧局，此后自动更新检查点。R使用该轮原初配置，不把后来开滤芯当初态。

目标/范围读Core，装不下的碎屑留场；材料出售/丢弃/转换/剂使用互斥。自动转换附加耐久不足时回普通容量吸取，不拒绝原本合法基础动作。耐久0不是立即失败，可合法移动回补给点，Restock花钱/行动恢复耐久，不补行动或滤芯。有效清洁先更新完成，再判行动耗尽；全清即结束，末桶不自动变现且结束后不能再出售。完整初值与黄金输入见实施方案/Core README，候选参数不称已平衡。

## 保存、资产与欠证

完整安全点包括布局/残污/位置/工具/两料/剂量/滤芯/预算/财务/阶段/命令/事件。Core `ExportSnapshot()` / 静态 `TryRestore` / `ValidateSnapshot` 导出、生产命令重放校验纯数据；root存储适配使用 `Application.persistentDataPath/checkpoints` 的完整payload/版本/SHA，合法新档才写入，合法旧主档才更新备份，损坏主档不能盖唯一有效备份。每次成功新命令自动保存，F6手动保存；错误优先提示，保存失败留当前轮，“保存并退出”失败不关窗口。恢复历史可看、`LastEvents`清空不重播奖励，实际故障结果以本项目记录为准。

三维房间为原创C#参数网格，14类资产/21共用材质见 [资产来源](../../../../sources/art/proto-051-recycling-cleaners/README.md) / [配方哈希](../../../../sources/art/proto-051-recycling-cleaners/asset-register.json)，不冒称Blender/FBX或外部模型。字体/七原创短音/原创BGM九份副本见 [运行资源登记](../../../../sources/art/proto-051-recycling-cleaners/runtime-resource-register.json)，源/运行同字节已重新核对、字体声明保留；Player报告fontTrue，实际音频听感仍未验。

首Player的Standard shader剥离null异常、行号3与柜标题重叠、恢复/重试同帧移动三项缺陷已修复；最终隔离相机诊断无Exception/PLAYER_READY fontTrue，最终图三污物/残量可读且标签无该重叠，诊断未提交玩法命令。现有系统安全权限modal当前阻挡原生输入，不代理代操作；正常可操控窗口与真实动作/保存/退出/听感尚未验、欠证。28项规则/故障存储/Runtime自动输入适配通过不等于功能美术、玩法体验或完整纵切QA通过，不借032成绩，不称Steam接入/发行完成。完整手柄/低配性能、成长商店与完整音画内容为后续；提交备份由root实际执行。
