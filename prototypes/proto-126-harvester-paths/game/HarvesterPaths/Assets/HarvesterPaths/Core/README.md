# 收割机自己铺路 Core 合同

版本 **0.2.0**，存档格式 **1**；2026-10-10。`Rules.cs` 是首个有限纵切的唯一数值来源，`Session.cs` 是规则/事务权威。所有具体数值和地图都是助手可验证初值，不是试玩平衡结论。保留原始 [需求](../../../../../../../docs/design/first-batch/proto-126-harvester-paths/requirements.md) 和 [设计](../../../../../../../docs/design/first-batch/proto-126-harvester-paths/design.md) v0.1。

本目录纯 C#、无 UnityEngine。Unity 输入/场景/音效只读快照与已提交事件，不能自行扣油、改变地块或发交货收益。工程/构建/真实操作成绩见项目开发记录，不在这里预填。下列轨迹为独立账目推导及生产 API 回归的输入；离线 .NET 检查不替代 Unity 测试和真实 Player 输入。

## 固定地图、机器与三轮

- 10×10；内部坐标0起，`CellId=y*10+x`；`Up` 为 y+1，`Right` 为 x+1。玩家坐标文本显示内部坐标+1。仓库是地面功能标记 `(1,1)`，初始车也在此、向右；仓库不是第四地块。
- 三种 `TileKind`：Crop、Ground、Mud。作物固定10格：x=2、4 各 y=1..3，x=6 的 y=1..4。泥地：x=3 的 y=0..4，x=5 的 y=0..5。其它都是地面。
- 作物不能驶入，须从前方收割；每格仅产 **1粮+1秸**，整笔装车后成为可通行地面，`Harvested=true`。泥地铺路变地面，`Paved=true`。`InitialKind` 始终保留来源；收后通路和铺路三轮延续，无重生或随机地图。
- 第1/2/3轮目标分别3/3/4粮，固定行动预算24/32/52。`RoundIndex` 为 **1..3**。轮间显式确认，不自动切换。
- 初油24、油箱上限24、初现金4；基础共享容量6，粮秸各占1空间。实际粮交付单价2现金；仓库存秸不收入、不取回，也不参与车载转换/铺路。

## 动作成本与模块

每轮锁定 `ModuleKind.None/Roller/WideHead/CargoBox` 一种，不能叠加或轮内免费切回基础机。新局选择首轮模块；后续轮仅 `ContinueRound` 选择。

- 基础机：地面前进1行动/1油，泥地前进2行动/3油；90度转向1行动/0油；窄幅收割1行动/1油，切前方1格。
- 滚轮：容量4；泥地改为1行动/1油，其余同基础机。
- 宽切头：容量6；收割2行动/2油，切前方横排3格；90度转向2行动/1油。移动同基础机。边界外切幅点不作为目标，幅内所有实际作物都须装得下；地面/泥地不产物。不存在免费窄切开关。
- 大箱体：容量10；所有前进动作额外+1油，所以地面1行动/2油、泥地2行动/4油；其它同基础机。
- 仓库 `Deliver(amount)` 交粮、`UnloadStraw(amount)` 卸秸、`Refuel()` 补油各1行动/0油。数量必须大于0且≤实际车货；交粮还须≤本轮尚需。交粮扣车粮并付价一次，卸秸进入唯一仓库账。补油花2现金、补至24，满油拒绝且不收费。
- `ConvertStraw()`：任意位置，1行动/0油，固定消耗车载2秸，实际补油 `min(6,24-当前油)`；油已满拒绝。接近满油仍固定耗2，预览明确实际收益。
- `Pave(x,y)`：1行动/0油、车载2秸；仅自身正交相邻一格未铺泥地（不含自身、远处或对角），变为地面且三轮保留。材料不足整笔拒绝。同一秸不能兼得油与路。
- 油与行动独立。0油不会自动失败；还能进行合法的0油转向/交货/转油/铺路或仓库补给。0行动未达标才失败。缺油的移动/收割整笔拒绝，不偷偷扣步或改变位置。

`PreviewMoveForward/PreviewTurn/PreviewHarvest/PreviewPave/PreviewDeliver/PreviewUnloadStraw/PreviewRefuel/PreviewConvertStraw/PreviewContinueRound` 读取相同规则，拒绝返回明确原因。预览不花预算，不修改地图、不发奖。

## 事务与阶段边界

全部命令先检查阶段、位置、目标、数量、行动、油、材料/现金和整批容量，全部合法才扣费用和完整执行。拒绝动作 `Success=false` 且状态/命令/事件不变。有效动作提交成功 `Success=true`，即使该动作导致截止失败，也可安全保存终态。

- `Active`：允许通常动作。实际交粮先更新目标，再检查行动截止；最后1行动达标有效。
- 第1/2轮达标变 `RoundComplete`。旧预算仅可在仓库卸秸/付费补油，照常扣旧行动；不能再移动、转向、收割、交粮、转换或铺路。旧预算用尽不把已达标轮改为失败。
- `ContinueRound(module)` 仅 `RoundComplete`、仓库、非第3轮；它本身0行动但仅能执行两次，并一次锁定下一轮模块。保留车位/方向/地块/粮秸/油/现金/仓库存秸，只设置下一固定预算和本轮交粮0。现货超过下一容量则整笔拒绝；先用旧预算卸秸，或选能承载的模块进入后再花新预算卸货。0旧行动仍可合法进入下一轮。
- 第3轮目标完成为 `Finished/Completed`；任一轮0行动未达标为 `Finished/Failed`。结束后不再交货/维护。`Retry()` 创建同首轮模块的新三轮初态，不把失败钱粮带入新局。

守恒：`收割格数=车粮+总交粮=车秸+仓秸+已转油秸+已铺路秸`；`当前油=初油+实际补油增量-累计耗油`。现金只来自初钱、实际交粮与实际补给支出。三轮各有 `RoundState`（目标、预算、实际交粮、行动消耗、模块、进入/完成状态），不能拿上一轮的进度抵新目标。

## API 与完整检查点

`Session.CreatePrototype(ModuleKind module=ModuleKind.None)`；`State`、`LastEvents`、`ExportSnapshot()` 深拷贝。命令均支持可选 `string commandId=null`：MoveForward、Turn(±1)、Harvest、Deliver(amount)、UnloadStraw(amount)、Refuel、ConvertStraw、Pave(x,y)、ContinueRound(module)。`CommandResult` 为 Success/AlreadyApplied/Reason/Events。

- `VehicleState`：X/Y/Facing/Module/Fuel/Grain/Straw；Capacity/CargoUsed getter 读取 Rules。
- `TileState`：Id/X/Y/InitialKind/Kind/Harvested/Paved。`HarvestPreview.CoverageCells` 为完整切幅（可能有边界外点），`TargetCells` 仅真实作物；另含产粮、产秸、空间、行动/油成本。
- `RoundSnapshot`：规则/格式/首轮模块、阶段/结果、RoundIndex、ActionsRemaining、DeliveredThisRound/DeliveredTotal、StoredStraw/Wallet、HarvestedCrops/ConvertedStraw/PavedStraw、FuelSpent/FuelAdded、ActionIndex、Vehicle/Tiles/Rounds/Commands/Events。全部存储数据为可序列化公开字段；派生 getter 不作为独立存档真值。
- `GameEvent`：稳定 Sequence/ActionIndex/RoundIndex、动作种类、车坐标、实际数额、行动/油费用、粮秸油钱变化、目标格、原因。轮完成/截止事件不再次扣同一动作费用。
- `ValidateSnapshot(snapshot,out reason)` / `TryRestore(snapshot,out Session,out reason)` 先检查版本/结构/资源范围/守恒，再从初态按**完整成功命令**重放并比较全部公开存档字段（包括事件、轮账和地块）。不接收无记录的免费收获、进度、油、钱或预算；无参数命令不接受伪造参数。未知版本拒绝，不默默降级。
- 命令ID仅首次成功登记，重复ID `AlreadyApplied=true`、空事件、不再执行；恢复保留已提交ID，`LastEvents` 清空以免重播发奖。回放仅生成新 Session，不写当前正式状态。
- 本地一致性检查不是密码学反作弊。磁盘提交、SHA/有效备份和故障注入由独立 Support/Runtime 负责，Core 不读写文件。

## 基础机三轮黄金输入与独立账目

记 `H`=收割、`M`=前进、`T-`=逆时针90度、`T+`=顺时针90度、`D(n)`=交粮、`U(n)`=卸秸、`F`=仓库补油、`C`=转2秸。每个乘号代表逐次提交相同生产命令。全过程无模块、不重置资源或地图。

1. 首轮：`H,M,T-,H,M,H,T+×2,M,T+,M,D(3),U(3)`。12行动交粮，再1行动卸秸，共13行动/7油；终车(1,1)向左，剩步11/油17/现金10/仓秸3，车粮秸0。
2. `ContinueRound(None)` 后：`T+×2,M×2,H,M,T-,H,M,H,T+×2,M,T+,M×3,D(3),U(3),F`。20行动交粮，再2行动卸秸/补油，共22行动/15油；补油实际+22。终车(1,1)向左，剩步10/油24/现金14/仓秸6，车粮秸0。
3. `ContinueRound(None)` 后：`T+×2,M×4,H,M,T-,H,M,H,M,C,H,T+×2,M×2,T+,M×5,D(4)`。30行动、耗油26、转油实际+6；终剩步22/油4/现金22、车粮0秸2/仓秸6/转油秸2，三轮完成。

总粮10全部实际交付；总秸10=仓6+车2+转2。油：24+22+6-(7+15+26)=4。钱：4+10粮×2-1次补油×2=22。带收费的命令13+20+26=59（泥地一次动作可扣2行动），加两次显式轮切为**61命令**；三轮累计行动65。每条轨迹均避开未收割作物驶入，容量从不超过6。

## 定向验证与剩余范围

`Tests/SessionTests.cs` 覆盖 HRV-A01–A05 对应的整批收割/重复拒绝、粮秸互斥、部分交粮与补给扣步、四模块同初态容量/泥地费用、末行动交粮及三轮守恒。另覆盖非法目标、远程仓库、油0/步0边界、达标维护、换模块容量拒绝、快照深拷贝、重复命令、完整恢复和伪造快照拒绝。独立 Support 磁盘用例由协调者负责。

承诺基础 None 有完整三轮路线；当前不声称三个模块全部组合的三轮最优解或全量可达已证明。地图、步限、模块、粮秸路线取舍是否有趣仍需真实玩家验证。未包含自动农业、连续农机物理、天气、多车、随机地图、稀有升级、仓库再装货、弃货、完整发行或 Steam 接口。
