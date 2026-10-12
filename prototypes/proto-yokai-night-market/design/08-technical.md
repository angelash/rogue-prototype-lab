# 工程与数据接口

## 独立工程

工程计划放 `UnityProject/`；编辑器 2022.3.62f3c1，Windows x86_64 Mono，Built-in Render Pipeline，Linear色空间，单场景 `Assets/Game/Scenes/NightMarket.unity`。规则不使用Unity物理、碰撞或帧时间。Product/Company/GUID按catalog.meta，persistentDataPath绝不沿用参考工程。工程创建时生成本项目 `.meta`，后续替换内容保留对应 `.meta`。

固定依赖uGUI1.0.0、TMP3.0.7、Test Framework1.1.33及必要内置模块。uGUI为本机编辑器内置包，TMP3.0.7来自相同编辑器版本Demo2022的已存在包元数据，Test Framework采用本仓已编译首批工程版本；不把其它项目编译成绩作为本项目包解析通过。计划配置：[unity-plan.json](../data/unity-plan.json)。本轮核实版本/路径，未建立新工程，包解析/编译/Player均未执行。

## 分层与状态所有者

`NightMarket.Core`：MatchState、CardInstance、FacilityInstance、TurnReducer、CardEffects、OpeningEvaluator、DeterministicRng；纯C#整数数据，不引用UnityEngine。唯一拥有AP/钱/工作/目标/实例区域/供电资格/胜负。`Application`：GameSession、CommandBus、AIController、TutorialSession、CampaignService、ResultCommitter；验证模式与事务，调度保存/AI。`Presentation`：BoardView、HandView、WorkerView、FacilityView、GuestView、ScreenRouter、CardDetailView、EventPresenter、FocusNavigator、InputRouter、AudioRouter；只消费snapshot与events。`Storage`：SaveRepository、SaveValidator、SettingsStore、JournalWriter。`Editor`：CatalogImporter、AssetAudit、BuildWindows；测试/Editor程序集不入Player。

原作30业务类逐项替换见 replacement/code-roles.json。重写接口及原创内容，不把反编译C#移植进来；拆除全局变量类/回调动作驱动判定。Tween结束只能释放输入显示锁，不能完成工作、检查胜负或发奖励。

## 命令及事件

命令公共字段：`commandID:string(UUID), expectedRevision:uint64, actor:P0|P1, type, payload`。类型 PlayCard(instanceID,targetRole?,targetSlot?)、React(counterInstanceID?|accept)、BuyFacility(facilityID,slot)、MoveFacility(from,to)、SetMode(slot,modeID)、SellFacility(facilityID)、EndTurn、BeginTurn（流程内核命令）。详情选择/取消/暂停不是玩法变更命令。

结果为 accepted/reasonKey/revision/events/stateHash，失败保持revision与随机状态。事件至少：CardPlayed、CardTransferred、WorkerPlaced、WorkerAdvanced、TaskCompleted、BlockAttached、BlockExpired、BlockRepaired、InspectionPending、InspectionAccepted、InspectionCancelled、CoinsChanged、ActionSpent、FacilityBought/Moved/ModeChanged/Sold、PowerChanged、GuestQualificationChanged、TurnStarted/Ended、RoundChecked、MatchEnded。事件字段带原因instanceID或commandID与before/after，不由UI猜原因。

按命令内顺序输出：扣AP/钱与卡移区 → 效果目标按稳定顺序 → 自然/即时完工与市望 → 全服务重算 → 客人资格变化 → 检查/胜负流程。同事务所有事件带同revision与sequence；表现按sequence播，快速模式可合并提示但日志不丢事件。rule hash使用规范化JSON字段排序UTF-8，排除cosmetic、相机、时间戳和显示动画状态。

## 内容接口

catalog.json为作者源，CSV供人工查阅/导入审查；导入脚本只读取JSON，不从CSV与Markdown拼第二套数据。C#内容DTO包含meta/rules/departments/cards/contracts/facilities/guests/characters/chapters/tutorial/strings/assets/audio/aiProfiles/screens；生产状态、黄金用例、UI布局和预算可在Editor加载为检查信息。所有ID字符串大小写敏感、永不复用；数字整数或明确注明的秒/音量；不解析中文描述推断效果。

卡引用实例与定义分开。effect枚举按catalog.effectOrdinal固定，未知效果拒绝导入。cards.target：self/opponent只表达大目标方，具体岗位选择资格由效果合同决定；counter目标固定自己反应。worker.department为角色工作类别；非worker.department为检查/停工指定岗位，null表示全岗位或需选岗位，不自行等价“随机”。

文本采用strings字典和stringVariables白名单，CSV为key/zh-CN；该版本仅中文，不保留原英语西语列。重复ID、未知引用、空名称、未覆盖资产、不合法模式、工作时长不是2/4、负面不是1/2/3、委托总数不是12等均阻断导入。图片路径是待生产目标路径，设计检查验证规格与引用；生产检查另验证实际文件、尺寸、hash、alpha及导入。

## 工具和证据

`python scripts/derive_design.py`生成CSV、完整内容手册、独立完整提示词、替换账与设施示例；`python scripts/verify_design.py`检查设计闭合。两个脚本均标准库，不安装包、不启动游戏、不访问网络。机器无原作安装/私有local时也能复跑，依赖仅sources/crunch-structure.json与本项目catalog；来源快照仅必要结构/数值/键名，无原作正文/媒体。

计划Unity入口 `-batchmode -projectPath ... -executeMethod NightMarket.Editor.BuildWindows.Run -quit -logFile ...`，实际实现后才可调用；本轮不提供虚构已存在的Unity构建脚本。首次编译后记录真实包锁、测试XML、完整构建清单/源码hash；进一步验收与当前设计校验分开。
