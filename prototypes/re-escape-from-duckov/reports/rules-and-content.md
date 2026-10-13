# 规则、内容目录与追踪方法

本机 Steam 2.3.30 的静态证据；完整配置、全部本地化和游戏文本位于本机 `local/`，此文保留原创分析与少量必要字段/数值。统计机器证据见 [content-summary.json](../evidence/content-summary.json)。

## 配置入口和计数口径

主要登记库都在 `resources.assets`：ItemAssetsCollection `76722`、任务库 `76578`、配方库 `76575`、建筑库 `76418`、商店库 `76730`、作物库 `76417`、场景登记 `76724`，总玩法配置 `76721`。对象编号使用“容器名:pathID”；DLL 类型 token 与资产 pathID 是两种不同编号。

- 物品：1,569 登记，无重复 ID、无空 prefab 引用；含角色躯壳和模板条目，不是全数可获得的玩家物品。
- 任务：315 登记，无重复 ID；522 子任务引用、846 奖励槽位（845 有效引用、1 原始空槽）。存在 ID -1 / 等级 9999 的模板，因此不把 315 当实际可玩任务数量。
- 配方：269 条，无重复 ID；产出与成本物品 ID 均能在当前物品登记中找到。
- 建筑：27 配置条目；商店：12 货表；作物：23 作物条目，另有 24 seedInfo 条目。
- 角色：156 CharacterRandomPreset，61 条 isBoss 标记；Buff：116；天赋树 10 / Perk 163。
- 场景：63 BuildSettings 条目、55 SceneInfo 条目、16 LevelConfig 对象。
- 掉落：5,653 容器配置实例、388 物品生成配置实例。
- 规则：7 RulesetFile，包括旧/额外规则文件；不能据此断言菜单提供七种模式。
- 图：15 NodeCanvas 行为树与 1 QuestRelation；全部脚本对象族共 1,053 族，专门目录之外仍可在族列表中检索。

## 物品如何连接属性与资产

ItemAssetsCollection 每个条目连接 metaData 与 Item prefab。metaData 提供 ID、品质、标签、显示键、描述键、堆叠与单价、口径和图标；prefab Item 连接 StatCollection、插槽、修饰器、变量、使用工具、图形与 agentPrefab。目录保留组件清单，可逐项进入枪械/弹药/附件/近战/技能等设置。

组件筛选得到附件设置 263、配方物品设置 215、弹药设置 145、枪械设置 124、近战设置 47、技能设置 19、GPU 设置 3、夜视设置 3。这些分类按实际组件存在统计，可能重叠，不应强制求和等于全部物品。

追踪示例：物品 ID 1 的 Item 为 `resources.assets:109239`，图标为 Sprite `resources.assets:5429`，该 Sprite 引用 Texture2D `resources.assets:1429`。Item 又连接 StatCollection `resources.assets:89446`、SlotCollection `77706` 与 ModifierDescriptionCollection `77041`。检索工具支持点击这些编号，不需要靠文件名猜测资产身份；图标共享也不代表物品完全相同。

## 射击与护甲实际采用的规则

证据：`Projectile.UpdateMoveAndCheck`、`Health.Hurt`、`ItemAgent_Gun`。投射物带距离、弹速、穿透次数、重力、阵营、伤害、暴击/元素/Buff/出血等上下文；单步 SphereCast 查询后的命中按距离排序，距离超过 halfDamageDistance 时应用 dmgOverDistance（非正配置回落为 0.5）。反弹、爆炸与追踪有独立分支。

Health.Hurt 中的一个护甲衰减分支为：

`剩余护甲 = clamp(护甲值 − 穿甲值, 0, 999)`；`护甲倍率 = 2 / (剩余护甲 + 2)`。

此式仅在存在适用护甲且伤害非 real、未忽略护甲等条件下参与，随后还受规则、敌我、暴击、元素与抗性等影响；不能当作完整最终伤害公式。剩余护甲为 0/2/8 时，该单独倍率为 1/0.5/0.2。对护甲耐久的破甲处理与这个穿甲衰减不是同一字段。

暴击判断会选择头部护甲，否则走身体护甲；从此处不能推断所有“爆头”都只由碰撞到头部决定。Projectile 还会因为追踪目标或反弹设置暴击率，所以应按完整上下文追踪。代码包含加载/无敌/死亡过滤与冲刺命中过滤；静态阅读并未验证这些分支在游戏中的手感或无敌持续时间。

## 掉落是条件组合

证据：LootBoxLoader.Setup / RandomActive、RandomContainer、ItemAssetsCollection.Search、LevelConfig。容器能按固定条目生成，也能随机抽显式池，或先抽标签与品质再从 ItemFilter 搜索结果随机取一项。关卡倍率改变随机数量，低品质概率参数影响品质池；特殊、Demo 与高级负面状态标签可能被排除。

固定物品出现概率、现金概率、容器激活概率和随机权重是不同层次；一个物品在配置中出现不等于该地图必然出货。同次区域的激活结果通过位置键缓存；没有把种子或抽样结果当作已经实际跑过的掉落分布。

搜查配置中的按品质时间数组为 `[1, 1, 1, 1, 1.5, 2, 1]`，查找时品质索引经范围截断。它描述该配置的等待参数，不是所有 UI 的总交互耗时。

## 任务条件与奖励是独立状态

Quest.MeetsPrerequisit 先检查等级与 Demo，要求任务关系图存在节点，再确认所需任务 ID 已完成，最后逐个求值 prerequisite 条件。关卡/场景条件还有 SceneRequirementSatisfied；领取、完成和奖励领取分别有状态。Quest.SaveData 以任务/奖励 ID 保存子状态，加载后通知奖励并可自动领取已完成任务的自动奖励。

子任务引用的主要类型为 SubmitItems 250、KillCount 124、TaskEvent 58、ReachLocation 37，其余为建筑 12、存档条件 7、蛇分数 7、撤离 6、信标解锁 6、天赋 5、交钱 5、使用物品 2，以及三个其它小游戏的各 1 个任务。这里是引用次数，不是类型数量。

奖励引用为 EXP 304、Money 281、RewardItem 101、UnlockStockItem 159；合计有效奖励为 845；任务 ID 939（resources.assets:103691）的 rewards[2] 原始指针为 0，构成第 846 个空槽。完整任务详情保留这一空值及每个有效 reward 对象与脚本信息。

## 基地与长期经济

Cost 可以包含金钱和物品成本。EconomyManager 支付时使用可用账户余额，再使用现金物品（ID 451），随后处理成本物品与事件；StockShop 将基础总价值按货表购买倍率或出售倍率取整，库存概率/刷新时间与解锁分开登记。单次交易是否允许账户受 LevelConfig 控制。

建筑以 ID、唯一 GUID、区域、坐标和旋转保存；占地格计算可以独立阅读。返回建筑得到可再次放置的 token，购买放置优先用 token，再支付成本；建筑替代关系还能满足某些“已建成”查询。token 是游戏建筑凭证字段，不能与平台认证令牌混淆。

作物 CropData 存 gardenID、坐标、cropID、score、watered、growTicks 与最后更新时间。Crop.Tick 仅在已浇水且未成熟时增加墙钟 ticks。CropData 有 33/66 分界的品质枚举，**当前 Crop.Harvest 直接使用 resultNormal 和 resultAmount**，并未按 Ranking 选择 resultPoor/resultGood；配置拥有三档结果不证明实际收获采用三档。未改变系统时钟或实际测试离线增长。

天赋通过行为组件修改属性、添加 Buff、解锁制作/商店相关能力和扩充基地功能。静态目录保留需求、连接与行为对象；未把所有节点默认视为同时可解锁。

## 本地化、对话与其它内容

StreamingAssets 有十种语言 CSV，完整解析到 `local/localizations.json`。简体中文有 5,857 个唯一键、14 个空值、无重复键；繁体 5,837 键，相对简体少 20 个。英语 5,840 键，相对简体少 20 / 多 3；其它语言的缺失/空值见内容摘要。这只是键集合差异，运行时回落与最终画面要单独验证，不能直接判定为玩家可见缺字。

简体按原 CSV 分组：物品 2,942 行、任务 683、天赋 212、Buff 164、Dialogues 124、UIText 414 等；组名是生产标签，不等于游戏中独立对话或剧情段落数量。其它语言的分组字段大多不同，不能直接逐组比较翻译覆盖。

全部脚本对象族、文本资产、本地化、关系图和 UnityEvent 引用为附属内容留下检索入口。音频事件与样本分别索引；故事的演出顺序、隐藏触发、全部小游戏胜负条件、成就平台行为尚未逐个运行验证。
