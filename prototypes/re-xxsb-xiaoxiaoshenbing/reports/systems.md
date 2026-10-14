# 小小神兵：系统与玩法分析

版本 v0.3；2026-10-14。结论来自包内实际 IL、反编译阅读文本、配置与资源对象；没有运行游戏或访问服务。下列入口均指向本地研究文件。完整类型/方法/调用与资源检索由[本地目录工具](../scripts/README.md)提供。核心系统经过人工阅读，其余模块已全量索引；不把索引数量当作逐方法人工理解的证明。

## 1. 产品和核心循环

《小小神兵》是三国包装的自走棋客户端。玩家在准备阶段购买、出售、合成和部署单位，利用羁绊、指挥官、装备及规则变体组成阵容；进入自动战斗后，客户端推进战斗逻辑并呈现结果。主界面另有角色成长、物品/背包、任务、商店、活动、邮件、排行及社交模块。哪些在线功能当前由服务器开放，不能由类名或历史配置直接证明。

证据入口：[BattleModel](../local/code/decompiled/GamePlay/GamePlay/Battle/BattleModel.cs)、[BattleControl](../local/code/decompiled/GamePlay/GamePlay/Battle/BattleControl.cs)、[AutoChessManager](../local/code/decompiled/GamePlay/GamePlay/AutoChess/AutoChessManager.cs)、[配置表目录](../local/content/tables)。

## 2. 启动、热更新和应用生命周期

启动场景包含 Launcher.Driver。资源用 YooAsset 1.4.6 清单组织，主包内列出的资源及依赖全部可用。业务采用 HybridCLR 热更新：21 个 TextAsset 托管镜像中，10 个是完整业务 DLL，11 个为 AOT 补充镜像。补充镜像中保留的部分 IL，与原生 IL2CPP 方法体分开保存。

GamePlay.AssemblyEntry 调用 GamePlayStartup，依次预载战斗一致性清单、启动图标配置和合并游戏配置，再建立 MiniMain。图标或配置预载失败会停止这段启动流程；一致性清单失败有单独警告路径。MiniMain 注册管理器和 MVC 模型/控制器，Update 顺序为时间、设备质量、输入、模型、控制器、管理器；LateUpdate 补充管理器表现。暂停恢复后跳过一次运行更新，退出/销毁采用去重关停。

证据：[AssemblyEntry](../local/code/decompiled/GamePlay/GamePlay/AssemblyEntry.cs)、[GamePlayStartup](../local/code/decompiled/GamePlay/GamePlay/Startup/GamePlayStartup.cs)、[MiniMain](../local/code/decompiled/GamePlay/GamePlay/MiniMain.cs)、[托管恢复](../evidence/managed-recovery.json)、[启动 Driver 类型树](../local/assets/trees/base-level0/level0/8_MonoBehaviour.json)。

ScriptingAssemblies 注册表列出 120 个名字，但只有一部分对应实际保留的 native 镜像/热更新 DLL。GamePlay.BattleRobot.Executor、GamePlay.AutoTest 和 GamePlay.DevScenes 的名称出现于注册表，没有在本包中找到相应完整业务 DLL。GamePlayStartup 的验收适配器采用反射与重试；这不能证明那些测试程序集已随包恢复。

## 3. 自走棋流程和输入路由

BattleModel 管理比赛/教程/测试/回放入口、资源预载、待应用协议、退出与结算；AutoChessManager 协调准备/战斗/结算、棋盘、观战与表现。独立 FlowPipeline 记录等待条件和超时，例如重连追赶 30 秒、观战棋盘表现就绪 5 秒。界面事件与运行时状态间存在明确桥接，而非按钮直接改所有状态。

请求路由明确区分 Rejected、HandledLocally、SendToServer。教程可由本地服务处理，正常比赛的购买、出售、货币等与协议响应结合。因此不能将教程离线经济推广为正式联机经济，也不能从客户端计算战斗结果推断服务器没有校验。

证据：[请求路由](../local/code/decompiled/GamePlay/GamePlay/Battle/BattleRuntimeRequestRoute.cs)、[流程步骤](../local/code/decompiled/GamePlay/GamePlay/AutoChess/AutoChessFlowSteps.cs)、[BattleControl](../local/code/decompiled/GamePlay/GamePlay/Battle/BattleControl.cs)。

## 4. 单位、合成、部署与经济

data_hero 有 37 条实际记录，unit_type 分布为 25 个棋子、6 个召唤单位和 6 个建筑；解释来自 HeroHelper 的三个常量，不把每条记录等同于当前可购买英雄。cost、model、icon 和名称从配置读取。

星级代表的卡牌数在 HeroHelper 中为 1、2、4、8（星级限制在 1–4）；出售估算为 `max(0, cost × cardCount − 1)`。这是客户端帮助器的具体行为，不是实测服务器发奖。例如配置费用 2 的二星单位，该帮助器返回 3。准备商店与长期商店是不同系统，后者还包含礼包、活动商店、免费刷新及服务器商品状态。

棋盘逻辑位置、场景网格、展示坐标使用多个明确结构和转换器。LogicWorld 的延迟生成位置搜索覆盖单侧 1–20 格；不能据此把所有场景、待命区和观战棋盘都解释为同一尺寸。

证据：[HeroHelper](../local/code/decompiled/GamePlay/GamePlay/Hero/HeroHelper.cs)、[单位配置](../local/content/tables/data_hero.json)、[长期商店](../local/code/decompiled/GamePlay/GamePlay/Shop/ShopModel.cs)、[LogicWorld](../local/code/decompiled/GamePlay/GamePlay/Combat/LogicWorld.cs)。

## 5. 战斗内核、时钟和顺序

PlayerWorld 使用 **0.125 秒固定步（逻辑接收 125 毫秒）**；正常每次表现更新最多推进 3 步，剩余积压保留。回放/加速与无表现快进有单独预算，最大 5,000 步。LogicWorld 与 RenderWorld 共享数据边界但分别负责规则和表现，RenderWorld 使用积压比例插值。

CombatRegister 给出实际逻辑系统顺序：阶段、回合、决策、技能、六边形网格、移动、朝向、距离、弹道、Buff、属性、碰撞、变体格、死亡、结算。Entity/Component、全局组件和桥接组件显式注册。LogicWorld 记录生成/排序身份、战斗 seed、每步 hash 与跟踪入口；存在这些代码不等于本次已经证明跨设备确定性。

两条随机路径分别保留：CombatRandom 使用有界线性同余流程；LogicWorld.RandomInt 使用有符号整数状态推进后转换为 uint 取模。不能把两条路径或它们的溢出语义合并成一份近似算法。

证据：[PlayerWorld](../local/code/decompiled/GamePlay/GamePlay/AutoChess/PlayerWorld.cs)、[系统注册](../local/code/decompiled/GamePlay/GamePlay/Combat/CombatRegister.cs)、[LogicWorld](../local/code/decompiled/GamePlay/GamePlay/Combat/LogicWorld.cs)、[CombatRandom](../local/code/decompiled/GamePlay/GamePlay/Combat/CombatRandom.cs)。

## 6. 技能、Buff 和伤害

技能从配置分解成触发、搜索、动作、移动、弹道、伤害、召唤、声音和效果等单元。SkillFactory 构建技能及组件，注册触发器，移除技能时清理调整状态与注册。data_skill 有 197 条、data_skill_action 有 148 条、data_battle_buff 有 87 条；这些是实际配置条目数，不保证每条都在当前可达内容中被使用。

DamageCalculator 的基础式为 `max(0, 攻击−防御) × 技能系数 × 施方倍率 × 距离倍率 × 受方倍率`，运算经 Maths/decimal 包装。星级压制差最多按 3 级处理。暴击率大于 1 时按百分数折算，再限制到 0–1；暴击倍率基础值为 1.5，加上暴击伤害属性。OnePunch 状态只用于基础攻击，成功应用后有单独消耗路径；不能将其看作所有攻击的常驻乘区。

证据：[DamageCalculator](../local/code/decompiled/GamePlay/GamePlay/Combat/DamageCalculator.cs)、[SkillFactory](../local/code/decompiled/GamePlay/GamePlay/Combat/SkillFactory.cs)、[技能表](../local/content/tables/data_skill.json)、[动作表](../local/content/tables/data_skill_action.json)。

## 7. 指挥官、羁绊、装备和变体

配置包含 9 条指挥官、17 条羁绊、42 条合成/规则变体及 9 条战斗装备。指挥官有专属策略类；变体通过 handler、池、奖励、商店、状态与格子规则拆分；装备有技能同步、寿命/耐久、效果执行及目标冻结路径。字段和策略是否生效取决于实际 handler 与模式资格，不能只看描述文字。

证据：[指挥官模块](../local/code/decompiled/GamePlay/GamePlay/Commander)、[指挥官表](../local/content/tables/data_commander.json)、[变体表](../local/content/tables/data_merge_modifier.json)、[装备表](../local/content/tables/data_battle_equipment.json)、[战斗 handler](../local/code/decompiled/GamePlay/GamePlay/Battle)。

## 8. 教程、回放、观战、重连与机器人

教程有 5 轮、21 步，结合 46 条引导配置和本地 TutorialBattleService：等待真实操作、进入部署、揭示敌方、战斗、结算及下一轮各有显式状态。购买成本由教程槽配置或英雄费用回退提供。本次只分析代码，没有代理输入通过教程。

BattleReplayService 保存初始房间、输入/状态帧、检查点、商店历史、视角和阶段时钟；Seek 从检查点恢复并回放后续帧。重连另有 catch-up、冻结输入和表现就绪协调。BattleRobot 与 Companion 两个实际 DLL 包含决策、训练/检验适配结构；缺少的 Executor DLL 不以名字补造。

证据：[教程服务](../local/code/decompiled/GamePlay/GamePlay/Battle/TutorialBattleService.cs)、[教程回合](../local/content/tables/data_tutorial_battle_round.json)、[回放服务](../local/code/decompiled/GamePlay/GamePlay/Battle/BattleReplayService.cs)、[重连](../local/code/decompiled/GamePlay/GamePlay/AutoChess/AutoChessReconnectCoordinator.cs)、[机器人源码](../local/code/decompiled/GamePlay.BattleRobot)。

## 9. 文本、界面、模型和音频

69 条 UI 配置与 UI/MainAsset、UI/ArtAsset 资源相连。主代码包含加载门、拖拽、布局、表现提交、RenderTexture 场景预览、Spine、GPUSkinning、混合渲染和 URP 接口。图标配置、业务名称和资源对象分别关联到稳定入口；没有把离线导出的图片排列视为实机画面。

语言表有 2,399 条。LangMgr 使用 UTF16 字符单元和 IEEE double 运算做哈希查表；已为 1,022 个配置字符串建立中文关联，保留原键和 hash。示例英雄名称“文丑/甘宁/陆逊”来自这种实际映射。部分配置本身已是中文，部分键属于旧/可选内容，不用全量文字数量证明所有文字都出现在游戏。

AudioClip 215 个全部有原编码与可解码样本；3 个 FMOD RIFF 载体的块结构另验。音频事件和曲目配置仍区分用途；本轮没有听感、混音或实机播放验收。

证据：[语言管理](../local/code/decompiled/GameCore/GameCore/Lang/LangMgr.cs)、[中文关联](../local/content/language-links.json)、[资源关联](../local/content/assets-linked.json)、[媒体清单](../local/media/index.json)。

## 10. 长期进度、网络与 SDK 边界

玩家、背包、任务、活动、商店、邮件、排行、聊天和好友都有独立 MVC 模块与生成协议类型。CacheMgr 使用 PlayerPrefs，并按全局、角色、服务器前缀分隔键；RoleLocalSettingCache 保存音量、振动、画质、分辨率等。PlayerPrefs 缓存不等于权威联机进度，也不构成本次实际存档/恢复测试。

Android 主入口为 com.zero.main.MainActivity，manifest 声明的 Application/Activity/Provider/Receiver/Service 已登记。DEX 以 FNSDK、TapTap、支付、推送/统计和 Unity bridge 等为主；原生库还包含渲染/地形与多家 SDK。研究仅读取本地字节，没有登录、请求服务、取得用户运行时身份或测试交易。

证据：[CacheMgr](../local/code/decompiled/GameCore/GameCore/Cache/CacheMgr.cs)、[角色设置](../local/code/decompiled/GamePlay/GamePlay/Player/RoleLocalSettingCache.cs)、[协议源码](../local/code/decompiled/GamePlay.Proto)、[Android 覆盖](../evidence/android-recovery.json)、[原生范围](../evidence/native-disassembly.json)。

## 11. 保留的旧内容和兼容返回值

包内含旧世界地图、雾、A* 和 SDF 数据，与自走棋当前主循环分开。部分兼容入口有默认/null/空列表返回，例如 ConfigLoader 的擦除类型入口、ShopModel 的某些旧商店分支。应以实际路径和开关判断采用范围；不能因代码存在就把所有旧内容写为可玩功能，也不能随意把默认返回认定为反编译缺失。

完整恢复层次及剩余缺口见[恢复边界](recovery-scope.md)。
