# 全部代码的阅读地图

游戏程序集 `Assembly-CSharp.dll` SHA256 以 [build-files.json](../evidence/build-files.json) 为准。完整派生文件在 `local/decompiled/`，原始 DLL 在 `local/build-copy/CrunchTime_Data/Managed/`，完整 IL 在 [Assembly-CSharp.il](../local/il/Assembly-CSharp.il)。

## 符号覆盖与区别

40 个元数据类型中：30 个顶层业务/辅助类型；2 个嵌套业务容器；5 个委托类型；`<Module>`、编译器私有实现类型及固定数组辅助类型各 1。663 是方法定义总数，含属性访问器、构造函数和委托成员，不是 663 个独立玩家功能。959 字段包含常量和编译器字段。所有类型与方法 token/签名/调用/字段引用在 [code-index.json](../evidence/code-index.json)；完整字符串引用只在本地 metadata 中保留。

下表覆盖全部 30 个顶层业务类型。M/F 是原始元数据方法/字段计数；没有把嵌套类的方法和字段再次加到外层计数。C# 由 ILSpy 产生，局部变量名和语法形式可能是推断，token 和 IL 是复核行为的基准。

| 类型 / 完整本地代码 | M/F | 职责与关键连接 |
| --- | ---: | --- |
| [Loader](../local/decompiled/Loader.cs) | 6/4 | 启动服务，等待场景，读取项目/卡牌 XML、实例化模板、驱动状态 |
| [hGameState](../local/decompiled/hGameState.cs) | 41/47 | 18 状态、双人周次、暂停/重开/退出、回合结算、自动结束、按钮锁定 |
| [hGameVars](../local/decompiled/hGameVars.cs) | 47/148 | 常量、部门、全局服务引用、配置、公司名、宽高比、箭头/快进图标 |
| [CardManager](../local/decompiled/CardManager.cs) | 88/103 | 双方五个牌区、抽牌、项目选择、合法性、悬停/选择、反制与目标交互 |
| [PlayCard](../local/decompiled/PlayCard.cs) | 130/85 | 卡牌数据、各类型效果、员工/事件回合、阻断、折叠、选择/灯效/粒子、动作 |
| [ProjectCard](../local/decompiled/ProjectCard.cs) | 77/116 | 五部门完成量/目标量、胜利、预算、任务条、曲线便签、部门按钮/盖章 |
| [AI](../local/decompiled/AI.cs) | 25/28 | 候选分析、出牌优先级、任务权重、员工目标、谈判反制、展示延时 |
| [Action](../local/decompiled/Action.cs) | 16/27 | 变换/延时/回调/谓词等待的六种动作，开始/结束及插值 |
| [ActionManager](../local/decompiled/ActionManager.cs) | 11/3 | 运行时动作列表、游标、待执行计数和阻塞/并行推进 |
| [ObjectToMove](../local/decompiled/ObjectToMove.cs) | 11/3 | 对象当前位置/目标位置包装，统计/布局插值的辅助模型 |
| [Progressive](../local/decompiled/Progressive.cs) | 8/5 | 最大/最小速度与渐变参数，卡片折叠/移动等数值推进 |
| [hLanguage](../local/decompiled/hLanguage.cs) | 11/7 | 三 XML 字典、两语言、文字重载、换行与预算格式 |
| [TextInfo](../local/decompiled/TextInfo.cs) | 2/4 | TextMesh 与语言键绑定、可大写/换行，语言切换重载 |
| [TextOutline](../local/decompiled/TextOutline.cs) | 6/8 | 自动生成八方向 TextMesh 轮廓，按像素/分辨率调整偏移 |
| [Button](../local/decompiled/Button.cs) | 22/29 | 通用文字按钮、碰撞检测、输入轴、锁定/文本还原、callback |
| [ButtonProject](../local/decompiled/ButtonProject.cs) | 9/20 | 项目部门按钮、可选状态、鼠标高亮、目标任务操作 |
| [Continue](../local/decompiled/Continue.cs) | 16/23 | AI 展牌继续、教程确认、总部谈判的超时选择与提示 |
| [Options](../local/decompiled/Options.cs) | 10/15 | 四个开关、两种语言、打开/关闭、文本刷新 |
| [Option](../local/decompiled/Option.cs) | 6/6 | 单个配置项的开关状态、按钮/文字表现与回调 |
| [OptionFlags](../local/decompiled/OptionFlags.cs) | 7/2 | 语言旗帜选择、点击与高亮，调用语言切换 |
| [Tutorial](../local/decompiled/Tutorial.cs) | 8/24 | 两页教程、页内对象渐显、标题和继续按钮控制 |
| [TitleManager](../local/decompiled/TitleManager.cs) | 14/44 | 项目/准备/回合/赢家/新记录标题、颜色和出现/消失动画 |
| [ScoreCard](../local/decompiled/ScoreCard.cs) | 8/31 | 十名排行榜插入/显示/重置、项目名称截断、预算和排名色 |
| [Fader](../local/decompiled/Fader.cs) | 9/7 | 主/副/选项覆盖层透明度淡入淡出和完成谓词 |
| [Rumble](../local/decompiled/Rumble.cs) | 7/21 | 卡片视觉摇摆、力度/角度/速度；不是手柄震动模块 |
| [Stamp](../local/decompiled/Stamp.cs) | 9/28 | 阻断/完成/取消/发布印章、声效、缩放与淡出 |
| [TaskIcon](../local/decompiled/TaskIcon.cs) | 8/21 | 部门无任务/进行中/完成三状态图标、旋转/透明/色彩 |
| [MusicManager](../local/decompiled/MusicManager.cs) | 18/34 | 回合循环切换、项目/胜败/暂停/谈判环境音、淡出与切入位置 |
| [SoundManager](../local/decompiled/SoundManager.cs) | 9/55 | 常量索引、主音效/发牌/whoosh 数组、节流与随机变体 |
| [Statistics](../local/decompiled/Statistics.cs) | 2/0 | 遗留统计文件输出；发布的 SaveStatistics 入口为空 |

嵌套 `ProjectCard+TextUpdate` 为文本更新条目（1 方法/2 字段）；`CardManager+PlayersCards` 为每方牌区结构（0/7）。`Action` 的四个委托与 `Button` 的一个委托各有四个无 IL 的运行时成员。编译器私有实现用于常量数组初始化，保留在 IL/metadata 中，不虚构为独立玩法系统。

## 关键行为的 token 定位

- 加载原始项目并补默认：`Loader.LoadProjectCards` `06000117`；加载 95 卡：`LoadPlayCards` `06000118`。
- 状态进入与跳转：`hGameState.OnEnter` `06000136`；`NextState` `06000138`。
- 无放回随机抽牌：`CardManager.PickNewCard` `060001B2`；补牌阈值：`GetNewCards` `060001B3`；出牌原因码：`iCanIPlay` `060001C1`。
- 使用类型分派与预算：`PlayCard.Use` `0600006F`。
- 完成/重开/减目标/胜利：`ProjectCard.CompleteTask` `060000C4`、`ReopenTask` `060000C5`、`RemoveTask` `060000C7`、`CheckProjectFinished` `060000C9`。
- AI 优先级：`AI.UpdateLogic` `06000106`。
- 配置写入：`hGameVars.SaveConfig` `06000178`；空统计入口：`SaveStatistics` `0600016C`。
- 赢家插榜：`ScoreCard.UpdateRecords` `060000F7`；音乐回合切换：`MusicManager.PlayTurn` `0600020E`。

token 在本安装程序集内稳定，与反编译器重排的源代码行号相比更适合复核。方法重载通过 token/签名区分，调用图中的同名方法不可仅凭字符串认为同一个方法。

## 其它五个程序集

均在 `local/decompiled-support/<程序集>/` 有完整 C# 项目，在 `local/support-metadata/` 有每个方法体的符号/指令引用索引，原始 DLL 全部保留：

- Mono.Security：222 类型 / 1,824 方法 / 891 字段，Mono 安全/证书等基础实现。
- mscorlib：1,809 / 16,978 / 6,798，基础对象、集合、字符串、IO、线程等。
- System：851 / 7,447 / 5,744，通用库和框架功能。
- System.Xml：747 / 7,743 / 3,120，本游戏用 XmlDocument 读取内容。
- UnityEngine：547 / 6,665 / 2,087，Unity 托管接口与包装；很多调用最终进入原生引擎，extern/internalcall 没有托管 IL。

完整支持库的存在说明能力载体，不代表每个功能均被游戏使用。游戏直接程序集引用只有 UnityEngine、mscorlib、System.Xml。原生 CrunchTime.exe/Mono 仅做结构/导入索引，不声称取得其原始 C/C++ 源码。
