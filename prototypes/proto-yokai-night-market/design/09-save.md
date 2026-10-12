# 存档与事务

## 独立身份与字段

保存路径 `Application.persistentDataPath`，预期Windows身份为 `%USERPROFILE%/AppData/LocalLow/RoguePrototypeLab/YokaiNightMarket/`，以本项目实测实际路径为准，不硬编码用户目录。文件分为profile/settings/match三个逻辑存档，每个采用A/B双槽、generation和完整校验；日志独立，不能作为未提交奖励真值。

字段全集在catalog.saveFields。match含双方牌实例区域、工作/停工剩余、AP/钱/市望/目标/已完成、设施位置/库存/已卖、岗位锁、轮/先后/phaseToken、四玩法随机流状态/计数、检查待反应、命令去重账、结果和事务ID。profile含章节解锁、获胜委托、故事seen、教程步、排行、已应用resultID和摊名。settings含所有音量、字号/动态/加速/失焦、绑定/难度默认。

手牌、deck、discard等只保存实例ID，实例字典保存originOwner/controller/zone/remaining，重复指向或实例缺失视为损坏。设施每件stock/installed/retired恰一；derived服务/客人资格不作为真值，加载按规则重算并校验摘要。不保存动画进度为规则状态，恢复完成的事务直接展示snapshot。

## 提交协议

逻辑档 envelope：schemaVersion、generation、contentVersion/hash、payload、payloadSha256、commitMarker。先将新代次写非活跃槽 `.tmp` 并flush，完整回读payload/hash/结构，写已提交标记，替换非活跃槽的完整文件；旧活跃槽直到新文件校验可读取前不改。文件句柄Flush(true)在Windows支持时使用；不以调用名声称断电绝对安全。

读取同时检查A/B，选已标记且hash/结构/版本适配的最高generation；同代次但不同payload摘要视为冲突，保留两份并要求选择可显示的创建信息。索引仅加快读取，不成为唯一真值；`.tmp`和没有commitMarker的文件不能加载，损坏文件转带日期的隔离备份，不自动删除。

保存边界为每次成功玩法命令、BeginTurn整批工作/收入/补牌后、EndTurn/轮末后、进入Reaction时和结果事务准备时。未确认预览不保存；保存中遇新命令串行排队，失败仍可继续在内存玩，并显示持续“未保存”状态，回菜单/退出提供重试/不保存/取消。

## 跨档结算

MatchEnded先在match完整存入结果及 resultTransactionID=`matchID:result:1`，再向profile应用幂等奖励（只正式获胜解锁；全部正式终局记榜），profile持久化成功后标match奖励已应用。任何点退出，恢复时如果match已有结果但profile不含ID则重试应用；若profile已含则跳过，不再发解锁/添排行。排行resultID唯一。

BeginTurn与EndTurn各有phaseToken，保存恢复不能重复领钱、工作、补牌、减事件时长。去重账保留本局至终局的全部合法命令ID和精简结果，本局最多约千条，预算1MB；超过预算是缺陷，不静默裁掉会影响重放的部分。查看命令不入账。

## 版本与迁移

第一版格式1，不读取原Crunch Time PlayerPrefs。未来同规则内容修正文案/插画仅asset/presentation版本变化可直接读取；玩法值变更提升content/rules版本，须带旧内容快照或明确迁移函数/样本。无迁移函数时禁止静默用新目标替换旧目标，显示ui.contentMismatch，提供备份恢复/保留原档后新局。schemaVersion大于支持值显示error.saveNewer，保持原文件。

profile字段追加可定义向前兼容默认，但仍提升格式与写迁移备份。重置需独立确认，先保存完整本地备份再写空档；教学/复盘不改变正式局档，分别保存tutorialStep/临时复盘状态。离线始终可玩，无远程依赖。

## 故障验收

注入写拒绝、空间不足模拟、半写、提交标记前/后退出、唯一有效旧槽、双槽损坏、hash正确但实例重复、未知版本、内容摘要不同、结果profile已提交match未标记、BeginTurn提交后退出、Reaction中退出。使用隔离测试目录/模拟存储接口，不填满真实磁盘、不改真实玩家档。每项记录故障点、选择代次、最终状态和奖励次数。
