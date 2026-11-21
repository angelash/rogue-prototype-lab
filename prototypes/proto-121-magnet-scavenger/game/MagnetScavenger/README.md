# MagnetScavenger / 磁铁拾荒者

## 夜班 1.0（当前 Windows 版）

工程使用 Unity 2022.3.62f3c1。打开 Bootstrap 场景并进入 Play，或运行普通包中的 MagnetScavenger.exe。规则、提示与关卡在 Assets/MagnetScavenger/Core/Shift*.cs，现场输入/绘制在 Presentation 的 MagnetRuntime、ShiftDesk 与 ShiftBoard。工单完成可接下一单，最后一单后可看交班单，并回到已开放工单重玩。

在仓库根运行下方 Test 命令执行新版与历史回归。构建可用下方 Build 命令。若要复现本轮启用图形设备的批处理构建，可从仓库根在 PowerShell 执行：

```powershell
$editor = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
$project = (Resolve-Path 'prototypes/proto-121-magnet-scavenger/game/MagnetScavenger').Path
& $editor -batchmode -quit -projectPath $project -executeMethod PrototypeBuild.Editor.ProjectBuilder.BuildWindows -logFile "$project/Builds/night-shift-build.log" | Out-Null
```

新版输出目录为 Builds/NightShift-1.0-Win64。请整体分发 exe、Data、Mono 和同目录 DLL。在[交付记录](../../../../docs/development/proto-121/2026-11-21-night-shift.md)核对实际验证与包装清单。

## v0.2 首次纵切历史记录

2026-10-10 的版本为 0.2.0-slice，本节以下所述输入、存档与输出路径对应旧版。

## 范围与权威

原创三维“夜班废料箱”是助手建议，表现离散12×8网格，不用物理磁场。四形状五ID、固定接口/首件、负载/电量、Pending留售、回收区拆后段/重装、两同图独立订单与完整安全点是本轮范围，原v0.1二维方案保留。

[项目规范](../../DEVELOPMENT.md)、[实施合同](../../../../docs/development/proto-121/implementation-plan.md)、[操作](../../../../docs/development/proto-121/input-and-ui.md)、[开发记录](../../../../docs/development/proto-121/2026-10-10-slice-01.md)分别管理方法、行为、输入、真实证据。参数只读[Rules.cs](Assets/MagnetScavenger/Core/Rules.cs)，Session拥有状态/预览/命令/事件/快照，Runtime/SceneView只做适配和表现。

助手初值v0.2.0/schema1：60电/承重6/射程4，Parts长杆+L形、Heavy长杆+重块；移动1/旋转2/吸2/留1/售1/拆2/装1。售价3/6/5/9、实际订单贡献加2。独立黄金Parts保留链余48/现金15、空头余45/现金15，Heavy余21/现金22，已通过真实生产路径Unity测试，不是平衡/最优证据。

## 开发命令

在仓库根，由协调者统一执行已有脚本：

```powershell
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-121-magnet-scavenger/game/MagnetScavenger -Mode Prepare
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-121-magnet-scavenger/game/MagnetScavenger -Mode Test
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-121-magnet-scavenger/game/MagnetScavenger -Mode Build
```

[Editor方法](Assets/MagnetScavenger/Editor/ProjectBuilder.cs)为`PrototypeBuild.Editor.ProjectBuilder`。Prepare仅Bootstrap不存在时建场景，不覆盖同名场景；保留Standard运行材质，设置1280×720窗口/Mono。Build是普通Strict，输出`Builds/Windows64/MagnetScavenger.exe`及完整Data/Mono目录/build-summary；不只交exe。`.local/`与Builds不进Git，真实日志/XML/包清单在开发记录登记。

## 当前操作与保存

- WASD/箭头平移，Q/E整链转；Space吸首件，K留，Enter优先售Pending否则所选ID。
- 1–5/点占格选稳定ID，暂存可数字选；X开拆所选及后段预告，Enter确认/Esc取消；T重装所选暂存。
- F6保存/F9恢复、R同订单重试；顶部Parts/Heavy是独立新局，成功新事务自动保存。
- H最近10事件、Y仅关键/含移动，Tab规则，均只读阻命令；M音乐，F5窗口截图，Esc保存退出确认，失败留窗。

根在回收区x0..2/y0..3才售/拆/装。Pending占格/计重但不延伸/续吸，链尾才能直接售，中段带后段拆Storage；非法整笔拒绝，最后有效Sell先目标再能量。Support存完整RoundSnapshot到persistentDataPath/checkpoints，新档领域校验、恢复重放、有效备份回退与[四存储故障源码](Assets/MagnetScavenger/Tests/PersistenceTests.cs)已通过本项目4项磁盘故障测试，不承诺Cloud或硬件断电绝对安全。

## 资源与未验范围

[九运行资源登记](../../../../sources/art/proto-121-magnet-scavenger/runtime-resource-register.json)含七本仓原创短音、121候选BGM、Noto CJK2.004/完整OFL，声明在StreamingAssets/ThirdPartyNotices。SceneView采用原创C#网格/材质，来源清单已归档并实际初态目检，无Blender/FBX/生成图；实际画面、hash/解码和耳机听感分开验。

已有桌面安全权限modal只阻正常窗口阶段；当前不宣称所有Player操作通过。手柄/重映射、性能/低配、MAG-H01–03真实玩家、随机箱/成长、完整成品音画/Steam未做。六文档先冻结交协调者补真实结果与Git备份，不等待最终Build反复改入口。
