# 数据权威与字段合同

`catalog.json`是唯一作者源；`csv/`、opening-layouts.json、内容手册、提示词与替换记录均由derive_design.py派生。`schemas/catalog.schema.json`给结构类型约束，verify_design.py进一步检查语义、数量、引用和路径。不要直接改派生文件。

## 单位与主键

所有ID区分大小写，card C001–C095、contract N01–N28、facility F01–F12、guest G01–G05；角色W001–W042与NPC.host/guide/rival/clerk。sourceID只作来源追踪，运行不读旧XML。工作/停工单位为所属摊自己的回合；AP、铜钱、市望、任务数、slot均整数；音量0–1、时间秒、颜色sRGB十六进制、尺寸像素、pitch本版1固定。slot0–4，负面/帮工/设施各五位。

cards：kind为worker/negative/instant/positive/repair/counter/inspection；effect为work/block/steal_hand/return_workers/discard_hand/advance_all/finish_all/unlock_department/reduce_goal/repair/cancel_inspection/add_goal/reopen_task。department null仅按效果规则解读；turns仅worker2/4或negative1/2/3，其余0；amount是执行量，不作显示倒计时；repairIDs必须指negative定义；character worker必填，其他null；art指资产；ap除counter0外为1，coins0。

contracts：goals是五个岗位的初始目标，正整数、合计12；roundLimit20；guests一至两类无重复；chapter1–7；sourceEffectiveGoals/sourceAnimation/numericDecision仅来源对照，运行可剔除。剧情openingLine/successLine/failureLine均原创完整中文，引用strings的对应键导入；不从名字猜数值。

facilities：buyCoins2–4，sellCoins=buyCoins//2，stockPerPlayer1；每件两模式，tags服务列表、requiresPower布尔、powerOffsets整数邻格列表、noise布尔；price不包含AP。guest requiredTags非空、forbiddenTags可空；noise仅禁忌，不作为positive服务需求。

characters：身份、服饰、工具、岗位、动机、语气、台词和portrait用于原创制作及一致性；NPC.guide与W007共享身份。chapters/tutorial按独立流程和固定状态执行，不能通过介绍文字触发奖励。AI权重为整数、预算有上限、canSeeHidden永为false。

assets：sourceSize/runtimeSize/pivot/transparent/reference/method/status及生产路径；image_gen对应完整promptPath；native-svg/native-ui/audited-font无生图prompt，走明确源路径。参考图只能指已有资产ID，依赖图无环；规格阶段路径不必已有成品，生产验收时强制检查文件/hash。

audio：durationSeconds、loop/bus、sampleRate/channels/seed/峰值/响度与源/运行路径；status=specified不是已有WAV。audioEventMap定义全部应响事件与总线，静音不得删可见事件。strings字典为唯一中文权威，stringVariables仅声明允许占位符；含数值的卡description由本轮数据锁定，修改数值须同步更新该键再校验。

rules：固定边界/收入/费用/表现秒数；effectOrdinal为AI同值候选顺序；uiLayout坐标和字级；inputBindings默认绑定/可改范围；saveFields字段集合与save-envelope schema；budgets为尚未实测的目标；requirements和goldenCases分别追踪设计准备与未执行游戏用例；productionPlan分阶段放行。

## 修改与版本

新增/删卡、服务标签、结算次序、初始状态、目标与经济提升rules或content版本并写合同变更。只改标点/图片不改变核心规则hash，应在制作记录中留资源版本；同一个sourceID不能指多个目标卡。存档是否可迁移按09-save执行，不能改schemaVersion而不提供迁移。

字段导入规则：缺必须字段/额外未知效果/空可见文字/非法范围/引用断链阻断；源对照与生产检查信息由Editor剔除，不进运行判定。CSV内嵌列表/对象为紧凑JSON，无Excel公式，UTF-8/LF；用文本编辑或正确UTF-8导入，不转换ID前导零。
