# 121 磁铁拾荒者：首次纵切实施合同

版本：v0.2；日期：2026-10-10（Asia/Shanghai）。本轮首次候选实现与独立验证已完成，真实编译、测试、构建、初态相机及原生欠据集中在[开发记录](2026-10-10-slice-01.md)，仅原生、动态/完整HUD与听感保留未验。

## 1. 授权、产品与叙事

用户授权首批其余项目逐个开发并逐项备份；本轮助手采用首次可玩纵切范围推进121。地图、两订单、参数、三维桌景和实施顺序是助手建议。遵循[共同标准](../../../DEVELOPMENT_STANDARD.md)与[项目规范](../../../prototypes/proto-121-magnet-scavenger/DEVELOPMENT.md)。[需求](../../design/first-batch/proto-121-magnet-scavenger/requirements.md)与[设计](../../design/first-batch/proto-121-magnet-scavenger/design.md)保留2026-10-09的v0.1原案，来源是原工作簿`熟悉与意外_120个游戏方案.xlsx`版本1、`方案库!A32:Q32`，2026-10-09T03:04:42.048337Z。保留二维离散方案，三维只表现确定网格，不采用连续磁力或刚体判定。

有限包装为“夜班废料箱”：玩家是夜班回收员，在桌上一箱废铁里完成工坊零件或重料回收订单。磁头把铁件拉到固定接口；留下的货物成为下一次工具，卖掉就永久离开结构。没有成长商店、角色养成或分支剧情。开场一句“先看末端与首件，再决定货物还是工具”；结局显示交付、现金、余电，失败显示未完成订单与合法重试。教程/说明可关闭复看，不给额外奖励。

目标体验是预测首件和接入几何，并因订单、负载、占格主动留长或拆短。一箱会话包含成功/失败、重试和恢复；不是完整Steam发行，数学可达性不等于玩家觉得有趣。

## 2. 内容与参数权威

唯一参数来源为[Rules.cs](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/Assets/MagnetScavenger/Core/Rules.cs)。以下是Core负责人冻结的助手初值v0.2.0、存档schema1；UI读Rules/Session，不复制另一套费用或地图。改规则须更新版本、黄金输入和恢复合同。

- 12×8格，坐标从0开始，x向右、y向后；UI格坐标可显示为1起。根初始(1,1)、朝右，射程4、承重6、电量60、钱包0。
- 障碍(5,2)/(5,4)；回收区根坐标x0..2、y0..3。平移/整体旋转校验所有占格，装饰不成为新障碍。
- 五固定ID：1短片(2,1)、2长杆(4,3)、3 L形件(8,3)、4重块(6,5)、5短片(9,5)，初始均朝右。两订单同箱，不随机重抽。
- 短片局部占格(0,0)，重量1/售价3；长杆占(0,0)/(1,0)/(2,0)，重量2/售价6；L形占(0,0)/(1,0)/(1,1)，重量2/售价5；重块占2×2，重量5/售价9。
- 入口均为局部(0,0)；出口分别为短片(0,0)、长杆(2,0)、L形(1,1)、重块(1,0)。L形出口朝向相对入口左转，其余保持；旋转用Rules变换，不由Collider或模型中心决定。
- 移动1电、整体旋转2电、吸取2电、保留1电、出售1电、拆卸2电、重装1电；选择、预览、取消、说明和复盘无费用。非法动作整笔拒绝，不扣电、不改物件/订单/钱包。
- Parts“工坊零件”需长杆与L形各1；Heavy“重料回收”需长杆与重块各1。实际贡献订单的该件基础售价加2，普通件按基础价售。同件不重复入账；新订单/重试独立开始，钱与货物不跨局。

## 3. 核心流程与守恒

阶段为Active、AwaitingDisposition、Finished。从唯一末端沿出口方向查射程内第一占格物件；墙阻断，同距按稳定ID，前件超重不能跳到后件。预览和吸取共用Core查询，展示首件、射线、接入占格、总重、费用与具体拒绝原因。

合法吸取先校验电量、首件、负载与接口接入几何，再将实际ID从箱内移为Pending。Pending跟随根平移/旋转、占格计重，但不改变末端、不续吸。K保留才加入有序链；回收区可卖Pending。没有动画给货或未交付先给钱。

回收区可卖选中链尾或暂存ID；中段不能直接卖。X将选中链段及全部后段拆到Storage，确认前显示受影响ID、费用、新负载。T只把一个选中Storage ID重装到尾，重新检验几何和承重；末端/占格/负载同一事务更新，暂存不提供延伸。

每件唯一属于Crate/Pending/Chain/Storage/Sold之一，五ID不增删；Pending至多1、链有序，已售不能装回/再售。钱包和订单仅由真实Sell提交变化。最终合法出售先判目标完成，再判余电；电量耗尽且订单未成则失败。拒绝动作不能免费移动或隐式处置。

失败前合法补救是换根/方向、卖普通小件、回收区拆短/重装，无免费充电/降目标。耗尽后可复盘、读合法安全点或同箱重试，不能把已售件免费返还当前局。折返是否有趣需试玩。

## 4. 模块、接口与保存

- Core负责规则、状态、预览、事务、事件与快照：`Session.CreatePrototype(OrderKind.Parts/Heavy)`、Move(Direction)、Rotate(-1/+1)、Attract、Retain、Sell(itemId)、Detach(chainIndex)、Attach(itemId)，以及PreviewAttract/Retain/Sell/Detach/Attach、State、ExportSnapshot、ValidateSnapshot、TryRestore。接口由Core负责人落实并冻结，当前编译、34/34 Unity测试与普通Windows包已验证。
- [MagnetRuntime.cs](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/Assets/MagnetScavenger/Presentation/MagnetRuntime.cs)转输入为命令，只读State/预览；选择不是命令，禁灰读Core，同帧提交/恢复/重试共享门禁。
- `MagnetSceneView.cs`消费已确认状态，Build(Camera)/Render(Session,selectedItemId)/TryPick/CellScreenPoint只管表现/选格，不计算奖励。
- CheckpointStore在persistentDataPath存完整RoundSnapshot：根/朝向、链ID顺序与占格、全部物件/归属、Pending/Storage/Sold、电量/钱包/订单、命令ID和事件；不能只存布局/钱包。
- 新保存先领域校验；恢复校验版本/字段/守恒并从同订单重放成功命令，不重复吸取/给钱。临时档不是真值，主档坏时回退有效备份，新保存不得覆盖唯一有效旧档；错误优先显示，保存退出失败留窗。
- [四项存储测试源码](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/Assets/MagnetScavenger/Tests/PersistenceTests.cs)已落盘：实际快照恢复、提交前中断、非法新钱包不替换、唯一有效备份恢复后再保存。源码存在不等于通过；未知版本拒绝，不承诺硬件断电绝对安全或Cloud。

## 5. 独立黄金输入与验收矩阵

U/R/D/L为Move，Attract后数字是预期ID。这些是规则负责人逐格独立推导的预期，已通过本项目真实生产命令Unity测试，不从现实现输出反录。

- Parts保留链：`U,U,Attract2,Retain,Attract3,Retain,Detach0,Sell3,Sell2`。9命令耗12电/余48/钱包15；ID2/3已售，Pending/链/Storage清空，其余仍箱内。
- Parts空头：`U,U,Attract2,Sell2,R,R,R,Attract3,Rotate(-1),L,L,Sell3`。12命令耗15电/余45/钱包15；目标同上，差异来自真实末端与几何。
- Heavy空头：`Attract1,Sell1,U,U,Attract2,Sell2,R,R,R,R,R,U,R,R,U,U,Rotate(-1),Rotate(-1),Attract4,L,L,L,L,L,Rotate(-1),D,D,D,D,Rotate(+1),L,Sell4`。32命令耗39电/余21/钱包22，ID1/2/4已售，ID3/5箱内；不证明最优或体验成立。

按[原需求编号](../../design/first-batch/proto-121-magnet-scavenger/requirements.md)定向验证：

- MAG-A01：同根/朝向空头与长杆改变末端/首件，Preview与执行一致；补L形转向和四形状旋转占格。
- MAG-A02：前重后件不跳抓；超重、墙挡、无目标、重叠/越界、电量不足均整笔拒绝。
- MAG-A03：保留增重延伸，实际售出移除、一次售价/订单贡献；只售链尾，恢复/重试不重复获利。
- MAG-A04：拆中段带全部后段，取消无成本；重装校验、五归属守恒，费用不足无半提交。
- MAG-A05：墙(5,2)/(5,4)间通道的长杆/L形接入、拆短、两Parts与Heavy黄金账目；最后有效Sell与电量截止。
- 保存：实际完整快照、四磁盘故障、非法/未知版本、命令幂等。窗口另验真实键鼠、焦点/连按、确认取消、F6/F9/R、帮助/复盘/退出和完整HUD；相机不代替。

MAG-H01–03仍需真实玩家证据：预测下一吸、主动拆短的理由、同件因订单改用途。黄金/测试数量不能替玩家填有趣。长链恒优或主要困在菜单时先改图/成本，不扩形状掩盖。

## 6. 视觉、音频与生产顺序

场景建议已由负责人制作：暖灰工作台、深蓝金属箱、奶油12×8格/黄色回收区，黄U磁头、入口黄环/出口蓝箭；Chain青实框、Pending橙角框、选件绿圈，Core首交格白框射线/墙阻断红叉。左回收台右暂存托盘，装饰不增加可交互规则。格距.66m，中心((x-5.5)×.66,.045,(y-3.5)×.66)，相机viewport(.225,.14,.775,.66)。原创C#程序低模，无Blender/生图；来源清单和初态相机已归档目检，动态/完整HUD未验。

[九运行资源登记](../../../sources/art/proto-121-magnet-scavenger/runtime-resource-register.json)已有七本仓原创短音、一121原创循环候选和Noto CJK许可；不是WoC媒体或付费语音，声音无经济权限。实际hash、解码、播放和听感分开验证。

顺序：首件/唯一归属→四形状/整链负载→Pending/拆装/两订单→完整保存故障→原创样件→统一Prepare/Test/普通Mono Strict Build→相机初态→正常窗口/听感/玩家假设。已有安全权限modal只阻依赖正常桌面阶段，不代操作系统安全权限。独立结果已归档；单项提交后再进入下一项目。性能/最小画幅、手柄/重映射、完整内容与Steam是后续缺口。
