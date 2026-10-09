# 126 收割机自己铺路：首次纵切实施合同

版本：v0.2；日期：2026-10-10（Asia/Shanghai）。本轮助手采用有限首次可玩候选推进，Prepare、39/39正式规则/存储测试、最终Mono构建0错0警告与初态相机已完成，真实证据见[开发记录](2026-10-10-slice-01.md)；原生/动态/完整HUD/听感与HRV-H01–03仍待验。

## 1. 授权、依据与作品范围

用户授权首批其它项目逐个开发并逐项备份；具体顺序、地图/参数、三维田地样件和首次纵切范围是助手实施选择。遵循[共同标准](../../../DEVELOPMENT_STANDARD.md)和[项目规范](../../../prototypes/proto-126-harvester-paths/DEVELOPMENT.md)。[需求v0.1](../../design/first-batch/proto-126-harvester-paths/requirements.md)与[设计v0.1](../../design/first-batch/proto-126-harvester-paths/design.md)保留2026-10-09原案；原`熟悉与意外_120个游戏方案.xlsx`版本1、2026-10-09T03:04:42.048337Z、`方案库!A37:Q37`与“后续候选”标签不改。

有限包装“清晨试验田”：玩家驾驶小收割机完成三批粮食交付，同一片田里留下收割后的通路。开场“先交粮，再决定秸秆怎样帮下一段路”；仓库牌公开订单/成本，没有成长商店、农业生态或剧情分支。教程可关/复看，成功报三轮真实交付与资源去向，失败报当前已交/尚需与截止原因。三维只表现二维离散格，不做农机物理、天气、自由农场、多车或完整Steam发行。

核心是行动截止、燃油和共享粮/秸空间共同改变路线；补油不能购买更多时间，秸秆不能同时转油和铺路。目标是基础车可完成有限三轮，再比较模块/订单/材料用途，不因数学路线存在宣称玩家取舍已成立。

## 2. 内容与唯一参数源

以下为Core负责人/协调者约定的助手初值v0.2.0、schema1，已与实际[Rules/DTO](../../../prototypes/proto-126-harvester-paths/game/HarvesterPaths/Assets/HarvesterPaths/Core/Rules.cs)只读核对。参数/地块/费用只从该源读取，Session维护状态/预览/命令；UI和文档不维护第二份运行配置。原二维方案保留，具体镜头/样件另登记。

- 10×10，0起坐标x向右/y向后；仓库功能点(1,1)，初车同点朝Right。三地块Crop/Ground/Mud，仓库不是第四地块。Crop不可直接驶入，收割后变Ground；Mud可付费驶入。
- 泥地x3/y0..4、x5/y0..5；10作物格为(2,1..3)、(4,1..3)、(6,1..4)，其余Ground。每格一次产粮1/秸1，总粮10/秸10，收后地面/铺路跨轮保留。
- 三轮目标3/3/4，固定预算24/32/52。基础容量6，共享粮+秸；初油/上限24，初现金4。
- 基础前进：普通地1行动1油，泥地2行动3油；转向1行动0油，前方收割1行动1油。范围内全部目标产物能装下才整笔收割，无半次采集或静默删秸。
- 仓库部分交粮、卸秸、补油各1行动；粮实际交付售价2/单位，补油花现金2补到24。交粮amount必须>0、≤车粮且≤本轮尚需，只移出真实粮并一次增加进度/现金。
- 车载2秸转最多6油，1行动，按油上限记录实际增加；车载2秸铺一个相邻Mud，1行动，改为跨轮持久Ground。材料/预算不足整体拒绝，不能复用同一批秸；具体封顶/拒绝边界按实际Rules/Session核对。
- None基础对照；Roller泥地1行动1油、容量4；WideHead前横3格、收割2行动2油、转向2行动1油、容量6；CargoBox容量10、每次移动额外1油。每轮最多一种，WideHead不免费缩成窄模式，模块收益与代价必须同步展示。

## 3. 三轮状态与守恒

订单达标进入RoundComplete；前两轮仍用旧剩余预算，仅允许仓库卸秸/补油维护，不再移动/转向/收割/交粮/转油/铺路。显式ContinueRound仅RoundComplete、车在仓库且轮数<3，锁定下一模块并设置下一固定预算；容量不够拒绝，需先合法卸货或选可承载模块，不能静默删货。

轮切保留地图/收后通路/铺路、车位/朝向、车粮/秸、燃油/现金、warehouseStraw与历史事件，只重置新轮目标/已交/预算并换本轮模块。warehouseStraw本slice仅公开入库数量账，无取回/远程转油/铺路，只有车载秸可用；玩家可留车秸跨轮。第三轮达标结束成功，任一轮未达标预算耗尽则结束失败；Retry整局回初始，不免费重置单轮地图。

每次事务先检查预算/油/材料/空间/阶段/位置→完整执行→扣成本并更新交付→先判目标再判截止。最后有效交付可达标，若旧余行动为0仍能显式进入下一轮但不能做旧轮维护；不足成本的非法动作不消耗/半提交。燃油不足是明确拒绝，不暗增行动；无合法补救时可复盘/读合法安全点/重试。

守恒至少核：未收作物数+累计已收=10；累计收粮=车粮+累计交粮；累计收秸=车秸+warehouseStraw+累计转油耗秸+累计铺路耗秸；累计现金=4+2×累计交粮-2×有效补油次数。油另记初值/实际增加/实际消耗，封顶增加不能按名义6冒记。总共享货不越模块容量，收后格无重复产物，同粮不能跨轮重复交。

## 4. 基础黄金与连续账

M前进、H收割、T-1/T+1左/右转。以下是负责人逐格独立推导的None预期，已通过本项目正式Unity生产路径测试，不证明最优、全部模块组合或体验。

- 第1轮：`H,M,T-1,H,M,H,T+1,T+1,M,T+1,M,Deliver3`，12行动/耗油7、达标余12/油17/现金10/车秸3。再UnloadStraw3，合13行动，旧余11/仓秸3。Continue(None)后第2轮32行动、车(1,1)Left、油17/钱10、车货0、仓秸3，地图3格已收。
- 第2轮：`T+1,T+1,M,M,H,M,T-1,H,M,H,T+1,T+1,M,T+1,M,M,M,Deliver3`。泥地额外成本计入20行动/耗油15，达标余12/油2/钱16/车秸3。再UnloadStraw3和Refuel，合22行动，旧余10/油24/钱14/仓秸6。Continue(None)后第3轮52行动，地图6格已收，其余延续。
- 第3轮：`T+1,T+1,M,M,M,M,H,M,T-1,H,M,H,M,ConvertStraw2,H,T+1,T+1,M,M,T+1,M,M,M,M,M,Deliver4`。共30行动/耗油26、实际转换+6油，结束余22/油4/钱22/车粮0/车秸2/仓秸6，累计交粮10、全部作物Ground。

三轮终账：粮10全交，秸10=仓6+车2+转换消耗2，铺路消耗0。没有自动卖余秸或结局免费补油。每轮存“入轮→达标→旧轮维护→Continue后”快照，并比位置/朝向、全地图、车货/库秸/油/钱/交付/预算/模块，而非仅看Completed。

另为转油/铺路做同一安全点分支：分别核真实耗2秸、实际油增/目标Mud变Ground与后续通过费用、预算扣除，不能用上述无铺路黄金证明铺路已验。模块对照在同图同订单记录收益/代价，基础None完成不推成所有4³模块序列全通。

## 5. 实现、保存与生产

Core负责人已冻结接口：`CreatePrototype(ModuleKind=None)`、Retry、MoveForward、Turn(±1)、Harvest、ConvertStraw、Pave(x,y)、Deliver(amount)、UnloadStraw(amount)、Refuel、ContinueRound(module)，命令有可选commandId；对应预览、ExportSnapshot、static ValidateSnapshot/TryRestore。实际准确版本Unity兼容已通过本项目39项正式验证，不从动画/帧率/鼠标高亮判定。State的Vehicle拥有车位/朝向/模块/油/粮/秸/容量；RoundIndex、ActionsRemaining、DeliveredThisRound/Total、StoredStraw、Wallet、HarvestedCrops、ConvertedStraw/PavedStraw、FuelSpent/Added、Tiles/Rounds与事件分别核账。

[HarvesterRuntime.cs](../../../prototypes/proto-126-harvester-paths/game/HarvesterPaths/Assets/HarvesterPaths/Presentation/HarvesterRuntime.cs)已落盘/只读核对：W/上前进、Q/E转、Space收割/C转油、IJKL或鼠标选格/P铺路；Z/X数量1..10，Enter交粮/U卸秸/F补油；1–4只预选模块，在新三轮按钮或合法N生效，R保持InitialModule。F9/R/领域提交/新局共用Claim，F6键盘保存后return；新成功事务自动保存，失败退出留窗，H/Y只读最近10事件/过滤Moved与Turned。完整绑定见操作合同，实际native待验。

场景负责人采用原创明快农田台景，格距.72m，中心((x-4.5)×.72,.045,(y-4.5)×.72)；仓库蓝垫/米白圈、左粮仓、麦束/土垄/泥湿车辙/铺秸条，青白车显示真实窄宽割台/滚轮/箱体，后沿样件只是说明。Coverage蓝框/实际作物金角读PreviewHarvest，铺点白环；不自算收割。具体来源清单与实际初态相机已归档并目检，未采用Blender/生图。

安全点保存全部100格及修改来源、车位/方向/本轮模块、车粮/秸/warehouseStraw、油/现金、轮数/目标/已交/余行动/阶段、累计资源去向、稳定命令ID与事件。每成功新提交及显式轮切自动保存；保存前领域校验，恢复按版本/守恒/生产命令重放验证，不能重收作物/重复交粮/返还行动。[四存储测试](../../../prototypes/proto-126-harvester-paths/game/HarvesterPaths/Assets/HarvesterPaths/Tests/PersistenceTests.cs)已实际Unity通过：轮/车/地图快照恢复、提交前中断、非法新钱包不替换、唯一有效旧备份再保存；原生保存退出未验，不承诺断电硬件保证或Cloud。

工程[Editor入口](../../../prototypes/proto-126-harvester-paths/game/HarvesterPaths/Assets/HarvesterPaths/Editor/ProjectBuilder.cs)已存在：仅缺Bootstrap才建场景，保留Standard运行材质，普通Windows x64 Mono Strict包。统一Prepare→真实EditMode→Build→相机初态/资源→正常窗口/听感。源码/独立预检/图各自结论，不能替代native。

[九运行资源登记](../../../sources/art/proto-126-harvester-paths/runtime-resource-register.json)含七本仓原创短音、126原创循环候选、Noto CJK2.004及完整OFL；九副本hash/八WAV解码和随包字体声明已核对，听感仍未验，不采用WoC媒体/付费TTS。原创场景来源与资产配方见[来源入口](../../../sources/art/proto-126-harvester-paths/README.md)，源码/资产源与运行副本分别登记。

## 6. 质量与去留矩阵

- HRV-A01：一格只收一次、变通路；Crop不能驶入、收割预览与全部产物一致。
- HRV-A02：同批秸分支转油/铺路互斥、封顶实际增加、不足材料/容量整笔拒绝，铺路实际改变通过费用。
- HRV-A03：仓库部分交/卸/补有成本，补油不能增加本轮行动；达标旧预算只维护，Continue明确一次且不返资源。
- HRV-A04：None/滚轮/宽头/箱体公开利益/代价，模块单选和跨轮容量校验；Wide全产物不足不偷窄，基础三轮黄金可达。
- HRV-A05：最后有效交付优先目标，失败截止具体原因；三轮订单进度独立、总粮/秸/现金/油闭合，快照恢复无重复。
- 存储/输入：真实快照故障、未知/伪造/幂等；可见Player前进/转向/目标格/部分数量/维护/显式轮切、焦点/连按/窗口/复盘/存读/退出另验。

HRV-H01–03为真实玩家假设：同图订单/模块是否改路线、秸用途是否改回仓和装货、宽切是否产生可理解压力。若外圈全清恒优、加油破坏截止、铺路只有贴图或宽头无代价恒优，先修地图/预算/容量，不扩作物掩盖。本项真实结果已归档，原生/动态与听感接续补验；完整音画/性能/设备/手柄重映射/Steam留后续，既有系统权限modal只阻依赖正常桌面阶段。
