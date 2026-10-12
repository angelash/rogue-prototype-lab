# 验收和本机交付 v2.3

2026-10-13。当前交付是 Unity 可运行恢复候选；完整原生交互、原速体验、听感和跨引擎逐像素等价仍未完成。状态、包大小、全 SHA256 和各项实际报告以 [delivery.json](evidence/delivery.json) 为准。原 v2.2 包与验收结果保留，新增内容和适用版本分别登记。

## 运行与编辑

本机完整目录：`../local/Builds/Release/`，运行 `YokaiNightMarket.exe`，移动时须保留同目录 Data、MonoBleedingEdge、UnityPlayer.dll 与字体声明。该目录是 Windows x64 Mono 非开发包，构建报告为 0 错误、7 警告。警告包括原粒子过时属性和原业务自赋值；保留原规则行为，没有为清警告改写业务。

独立 Unity 工程：`../local/UnityProject/`；Unity **2022.3.62f3c1**；主场景 `Assets/Recovery/Scenes/NightMarket.unity`；18 个 prefab 在 `Assets/Recovery/Prefabs/`；完整恢复业务源码在 `Assets/Recovery/Scripts/`。QA 驱动在 `Assets/Recovery/Validation/`，仅额外编译符号 `NIGHT_MARKET_VALIDATION` 下存在。正式 Assembly-CSharp 元数据确认没有四个 QA 驱动类型。

正式产品身份为 RoguePrototypeLab / 妖怪夜市开张，设置与成绩独立；验证产品身份为妖怪夜市开张 QA Visual。QA 会关闭逐牌停顿，教程调查显式启用教学，整局测试额外设置快进及 timeScale=3，全部运行使用 QA 驱动内的 60 FPS 限制。正式包保持源运行设置。

首次两页教程按任意键推进；鼠标悬停手牌查看展开信息，点击当前手牌使用；选目标继续用鼠标。Space/右键交班，Tab 切换快进，Esc 持续约三秒退出、提前松开取消。四选项、双语言、改名、十条成绩和再开一摊沿原流程。原作没有局内保存。

## 已覆盖的证据

- [静态校验](evidence/fidelity-verification.json)：20 项，覆盖 30 业务脚本声明差异、95 卡原参数、28 有效委托目标、420 双语键、五份生产 XML 与实际 Resources 副本、字体、82 图规格/谱系/工作副本、结构、prefab、质量和产品身份。
- [规则](evidence/rules.json)：40 个原生产方法用例，覆盖容量、同 ID 冲突、岗位、干预、修补、调停、巡街、计分和成绩边界。它们使用隔离对象并还原成绩配置，不能解释为 95 张牌均已真人使用。
- [16:9 启动回合](evidence/smoke16x9.json)与 [4:3 启动回合](evidence/smoke4x3.json)：真实 Player 自动启动，95 卡与 28 委托加载，双方各五张起手牌，经过玩家与 AI 回合状态，无脚本错误。实际分辨率分别 1920×1080、1440×1080。
- [16:9 渲染](evidence/visual16x9.json)与 [4:3 渲染](evidence/visual4x3.json)：每画幅 128 张实际相机图，包含标题、两页教程、两语言选项、95 张展开卡与 28 张委托。逐张防空画面检查，人工查看功能类别、长说明、教程、选项与委托样本。相机摆放调查不等于鼠标选牌实测，也不代表全部 256 图逐像素对比。
- [整局](evidence/match.json)：原 AI 行动，QA 人类侧持续交班，经过原生产回调到状态 50；五类完成数均达到当前目标，等待原 SetReloadButton 回调使“再开一摊”就绪后捕获结算画面。QA 加速与人类不出牌不证明原速策略体验或所有目标操作。
- v2.3 [流程用例源](test-cases/flow-and-native.md) / `../local/QAResults/flow-v2.3/report.json`：15 项断言、一次场景重载、无脚本错误；原选项与重开回调、六项设置和成绩保留、新局分数/工作重置。通过 CompleteTask 制作短流程胜利夹具；timeScale 保持 1，不作为完整 AI 对战或原生输入证据。
- 原生 Release：13 项部分检查、29 张本游戏窗口截图，报告在 `../local/QAResults/native-v2.3/report.json`。实际验证逐牌停顿、两名料理帮工及闹场出牌/计分、岗位拒绝保牌、Space/右键交班、菜单暂停恢复、中英槽位、声音/停顿指示、Tab、按键改名、短 Esc 取消，以及两次进程启动间名称与设置保存。没有听感或完整目标/反制流程结论。

截图和原始日志保存在 `../local/QAResults/` 及 `../local/*-final.log`，未公开原游戏媒体。公开报告只有计数、原创文字、测试结果和 hash。规则与两画幅启动回合使用标题底板提亮前的同规则版本；随后只修改该图片及 QA 结算等待条件，复验两画幅 128 图与整局。正式业务源码、数值、Resources XML 与输入结构没有因此改变，不把此前报告冒称为最终纹理版本截图。

## 发现、修复和保留的失败

1. 初始 TextMesh 先翻译导致 TextInfo 缓存失去查表键，改为保留源键；六个教程静态示例姓名按 ID 单独替换。
2. Unity 假空值组件检测、旧 ScriptableObject 槽缺少有效独立 MonoScript、Loader/ProjectCard 执行顺序分别导致初始化或 Player 错误，现已按恢复合同处理；七个槽不增加规则对象。
3. Sprite.Create/OverrideGeometry 输出 UV 与源紧网格不同，改用源 78 Sprite / 970 顶点、indices 与 UV 序列化修复；桌面处于源远裁剪边界，通过 0.001 兼容偏移恢复。
4. 早期展开牌相机图为空，旧报告的 passed 未能反映这个缺陷。原报告保留在 `../local/QAResults/failures/visual-before-empty-capture-check.json`；修复模板激活与分层相机，并加入像素明暗范围检查。不能把早期报告作为视觉通过证据。
5. 教程测试曾每帧重置推进比例，阻断生产过渡；改为每页等待时只推进一次。后续观察到无帧率限制批处理下文字淡入长时间停顿，最终 QA 限制为 60 FPS。未做 Unity 4 对照，未确定跨版本原因，正式包没有为此改变源帧率配置。
6. 中性亮度 stencil 曾带回牌背/选牌背景/成绩板水印和点数标记旧字；缩小亮度保留白名单，其余保留 alpha 并使用自己的新图 RGB。成绩板与共享标题底板各作一次只引用自有原图的生图修订，修复深色成绩/商号标题的对比度，82 图随后完整重导出。
7. Sound 共用键造成声音选项显示为招徕；只给该开关显示“声音”/Sound，保持岗位键、检测和保存逻辑。六个教程示例与说明换成夜市名和紧凑文案。
8. 中文说明换行已写进 Media，但运行 Resources 副本曾仍为旧文案。已让包装生成器同步已有 Resources、保留 .meta，并新增第五类 XML 副本字节一致检查；最终图确认长中文说明在原区域内换行。委托摆放调查补调用原 SetPlayer 初始化，避免展示编辑模板默认商号。
9. 早期整局上限 1,500 秒超时，原失败报告保留在 `../local/QAResults/failures/match-timeout.json`。后续使用明确 QA 加速和更长上限，不能删掉失败后宣称所有运行都成功。
10. PRJ_No 生图两次未返回有效图，没有算为完成项；改写普通取消图标提示后成功，最终只登记有效原图。82 项各自有提示、真实生成尺寸、输出 hash 和最终规格。

## 完整本机备份与公开备份

`../local/Backups/YokaiNightMarket-UnitySource-v2.3.zip`：完整 Assets、Packages、ProjectSettings，含 .meta、包锁、场景、prefab、业务源码、媒体、OFL 和新增流程验证驱动。排除 Library/Temp/Logs 等缓存。

`../local/Backups/YokaiNightMarket-Windows-v2.3.zip`：完整非开发 Windows Player 与说明，排除调试符号及 Unity 的不发货备份目录。两包逐文件读回校验，不只验证压缩目录；完整文件清单在同目录 `.files.json`。较早 v2 / v2.1 / v2.2 包保留，不能用它代替最终 v2.3 包。

交付登记将当前工程三目录字节清单与源包逐条比较，将当前 Player 与运行包清单逐条比较，再登记总 hash。源码采用 Git 一致 LF，完整输入清单留在本机，公开仅总计与摘要。原安装的 25 个登记文件再次核对 SHA256，保持未修改。

完整 Unity 工程、资产制作源/运行副本、测试用例与参考输入已纳入本仓本地分支 `codex/yokai-night-market-source`；首个完整提交为 `e0b1c7ccfbe862d743191f8efc33d63a5e3acd1a`。完整 Git bundle 首次备份已克隆恢复并通过 fsck，覆盖比 Unity 源码 zip 更广，见[版本管理](../VERSION_CONTROL.md)。公开 Git 包含原创工具、夜市数据、提示词和独立生成原图；恢复业务实现、原音频和功能 stencil 派生 PNG 保持本地历史，输出包与结果忽略。

## 尚未完成的放行条件

早期开发包曾出现 Windows 防火墙提示，当时按电脑操作技能保留待用户处理，没有代操作权限请求。2026-10-13 复查原生窗口列表时该窗口已不在；通过正式 Release 启动并执行部分键鼠检查，没有新的安全提示。它不再作为当前全部原生 QA 未执行的理由。

仍需原生目标选择、调停反制、两页教程、长按 Esc、失焦、完整胜负/重开及听感。当前技能提供单次按键而非按住接口，长按退出保持未验。需要用原速原作对照，而非沿用 QA 加速成绩。

还需比较 Unity 4 与 2022 的同 seed Random、输入底层、字体光栅、粒子和音频混合。原 Unity 4 启动分辨率/质量选择弹窗未重建；18 根 prefab 是源场景恢复导出，不含 Player 中不存在的 Editor 连接信息。91 音频维持本机原事件引用，没有夜市原创音轨或声音等价证明。干净机、设备性能和 Steam 发行条件也没有本轮证据。

因此不能把当前候选登记为“100% 像素级或体验一致”或“已可发行”。助手继续负责剩余制作与验收。

## 本轮冻结结果

正式构建：610,454,063 字节，0 错误、7 警告；不含 QA 类型。源包 961 文件、354,877,575 字节；运行包 129 文件、375,640,390 字节，均已读回。

源包 SHA256：`5c144f09c399a48064459e65c4a9f83adec48d28daf73e13ecd0fb0c94941512`。

运行包 SHA256：`aab04090214227716ff4cf160664eee09c29a0ded6b6a8a495167b53a1dda977`。

最终自动整局：第 43 轮，胜方 1，状态 50，原重开按钮就绪 True，脚本错误 0；五类 done=[4, 5, 2, 6, 3] / goal=[1, 3, 2, 4, 2]（内部顺序招徕、料理、备货、灯饰、护阵）。

三目录完整输入摘要与 961 文件清单以 delivery.json / `../local/source-inputs.json` 为准；原安装 25 文件 SHA256 未变。新 Release 元数据为 47 类型、667 方法、957 字段、0 IL 解码失败；与 v2.2 比较时排除地址后的类型/成员签名、IL 长度、指令计数及调用引用一致，未以此声称原始 IL 字节完全相同。
