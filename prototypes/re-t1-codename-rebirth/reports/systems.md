# 逐系统分析与阅读入口

2026-10-13。以下路径为本机 release 反编译视图，完整 token/IL 在 managed-metadata；所有配置在 local/config/tables。未把客户端实现或存在的模块当作服务器规则/正式开放内容。

## 战斗、输入和射击

[EntityMgr](../local/source/all/release/hotfix/GamePlay/GamePlay/ECS/EntityMgr.cs) 注册 52 系统、实体池与全局组件；[EcsWorld](../local/source/all/release/hotfix/GamePlay/GamePlay/ECS/EcsWorld.cs) 确认更新/命令缓冲/销毁顺序。[BattleControl](../local/source/all/release/hotfix/GamePlay/GamePlay/Battle/BattleControl.cs) 的场景/进入/预热就绪门控制 battle_ready。

[ShootSystem](../local/source/all/release/hotfix/GamePlay/GamePlay/ECS/System/ShootSystem.cs) 检查条件、奔跑延迟与武器运行参数，再提交射击/反馈。[BulletSystem](../local/source/all/release/hotfix/GamePlay/GamePlay/ECS/System/BulletSystem.cs) 处理飞行/射程、段射线最近命中、起点内碰撞、友方/墙与特效。准星目标、呼吸、自动瞄准、后坐力、决策和散布分别更新。tps_weapon 每行 219 字段，配合 tps_bullet/部位系数等；最终服务器伤害未恢复。

## 移动、AI、技能和状态

ECS/System 的 CharacterMove/PlayerMove/AiMove、JumpMove/TrajectoryMove、Stamina、Skill/SkillProp/SkillSummon、Buff、State/Hp 顺序执行。表有 ai_architecture 226、ai_battle_behavior 196、behavior_tree 23、monster 151、skill 67、skill_behavior 210、battle_buff 74；完整条件/状态字段已读，是否当前版本开放需运行证据。

## 地图、交互和碰撞

[CollisionUtil](../local/source/all/release/hotfix/GamePlay/GamePlay/Collision/CollisionUtil.cs) 定义 flat/blocks 与盒/球/胶囊/柱格式，14 文件 49,987 形状精确消费。QueryWorld、TagRules、过滤器与 BulletSystem 共用查询。Map/任务、door/key、switch/mechanism、mini_map_*、map_leave 组织场景交互；地图、碰撞、导航 ID 并非同一集合。

## 背包、战备、容器和改装

[BagControl](../local/source/all/release/hotfix/GamePlay/GamePlay/Bag/BagControl.cs) 区分查询/战场查询/临时战备、更新、买卖与装备弹药，刷新实体和界面。tps_item_common 1025、bag/space、container/use、equipment_loadout、secure_container、item_put_rule 决定客户端显示/放置。Container/Drag/CombatReadiness 管理拾取/拖拽/战备。

[WeaponModControl](../local/source/all/release/hotfix/GamePlay/GamePlay/WeaponMod/WeaponModControl.cs) 对应配件 147、部位类型、槽、65 customization_loadout 与 UI；[SkillTreeControl](../local/source/all/release/hotfix/GamePlay/GamePlay/SkillTree/SkillTreeControl.cs) 对应 33 技能树条目和技能行为。图标数不等于武器数，库存/消费仍有服务器边界。

## 匹配、队伍和撤离

[MatchControl](../local/source/all/release/hotfix/GamePlay/GamePlay/Match/MatchControl.cs) 根据 start/quit/success 回包设置状态/地图，Team/CustomRoom/PlayMode 与 tps_gameplay/tps_match_team 组织模式/人数。[EvacuateControl](../local/source/all/release/hotfix/GamePlay/GamePlay/Evacuate/EvacuateControl.cs) 用 map_leave 和回包管理进入/退出/倒计时、结算类型及 CG/界面；收益公式未由客户端显示证明。

## 家园、制造、部门和经济

[HomelandControl](../local/source/all/release/hotfix/GamePlay/GamePlay/Home/HomelandControl.cs) 查询/激活/升级/确认建筑，更新 HomeModel/事件；28 建筑、55 等级存条件/功能，HomeBuilding* 处理场景交互。WorkbenchControl 更新制作槽/红点；武器/装备/药品/弹药配方 176/54/30/22 条。

[DepartmentTaskControl](../local/source/all/release/hotfix/GamePlay/GamePlay/DepartmentTask/DepartmentTaskControl.cs) 查询/接取/领奖/构造提交列表，客户端检查请求及数量。任务 100、部门商店 992、store 1235 条与 Department/PurchasePermit/TradingHouse 相连。买卖/奖励依回包处理，没有模拟物品或扣款操作。

## 教程、人物、剧情及外围界面

Guide 阶段/触发/完成策略对应 guide 134、guide_stage 199、advanced_tutorial 9、dialog 162；撤离成功通知教程。hero 7 条关联模型/技能/档案语言键与结算视频，CG 24 和 36 实体 MP4 保存演出载体。7214 语言行保留，未补写未见剧情或验证所有分支。

System/view 与 ui 144 条对应背包/改装/家园/撤离/设置等界面；Chat/Friend/RealTimeVoice/Mail/Activity/Season/Profile/Notify、custom_layout/operate_setting/setting_* 是外围模块。实际可达性、设置保存和服务器角色数据分别判断。

## 回放、资源、渲染与音频

BattleReplay/ProtocolReplayBridge 记录/派发协议和状态；发送门与入场分支已读，未运行回放/在线模拟。GameCore Res/Preload/DressableModel 管理句柄/预热/对象池。原生渲染 HCRH/SDF/GPU Skinning/TerrainVT 原字节保留，算法/编辑器转换有边界。FMOD、BGM/脚步/战斗/技能/人物/UI 音效配置可定位，事件混音与听感没有验证。

## 全量覆盖

这些类是阅读入口，全部方法/字段/调用及 namespace 计数见[覆盖](../evidence/deep-coverage.json)与 SQLite。按入口→调用/事件→模型→配置→对象/媒体阅读；反射和网络回包查 ProtoMap，不把静态边当作完整动态路径。
