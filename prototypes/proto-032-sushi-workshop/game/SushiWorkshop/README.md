# 回转寿司工坊：Unity首次可玩纵切

版本：v0.2；日期：2026-10-10（Asia/Shanghai）；任务开始：2026-10-09。工程使用Unity2022.3.62f3c1，目标Windows x64普通Mono Strict构建/PC单机。当前为首次可玩候选实现：Prepare成功，30/30规则/存储测试通过，已完成构建0错误/1警告；最终包与初态相机图已归档，原生操作尚待补，未完成完整纵切验收。实际分项结果见 [开发记录](../../../../docs/development/proto-032/2026-10-10-slice-01.md)。

## 范围与权威入口

用户授权逐个项目开发，本轮助手采用项目合同的首次纵切范围。固定斜俯视三维小厨房呈现六槽离散环、六料理状态、四工站和两种互斥订单。每轮选择整份或快速小份之一，准备、推进、圈边界调整、交付/保留、清洗、补救和结算；重试另一订单使用同样初态。三维料理/运盘动画不增加真实切菜、自由走动或任意布线。

- [项目开发规范](../../DEVELOPMENT.md)、[实施方案](../../../../docs/development/proto-032/implementation-plan.md) 与 [操作设计](../../../../docs/development/proto-032/input-and-ui.md)。
- 规则/参数由 [Rules.cs](Assets/SushiWorkshop/Core/Rules.cs) / [Session.cs](Assets/SushiWorkshop/Core/Session.cs) 独占，[Core README](Assets/SushiWorkshop/Core/README.md) 负责说明；已核对规则版本 `0.2.0`、快照格式 `1`。初值是助手v0.2建议，UI、存档与测试不另建一套。
- 原始依据：[需求v0.1](../../../../docs/design/first-batch/proto-032-sushi-workshop/requirements.md)、[设计v0.1](../../../../docs/design/first-batch/proto-032-sushi-workshop/design.md)。

## 准备、测试与构建

从仓库根目录在PowerShell执行，脚本使用已安装的本地Unity，不安装引擎或依赖：

```powershell
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-032-sushi-workshop/game/SushiWorkshop -Mode Prepare
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-032-sushi-workshop/game/SushiWorkshop -Mode Test
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-032-sushi-workshop/game/SushiWorkshop -Mode Build
```

入口为 `PrototypeBuild.Editor.ProjectBuilder.Prepare` / `BuildWindows`；Prepare只在不存在时创建 `Assets/SushiWorkshop/Scenes/Bootstrap.unity`，保留已有场景文件并设置本工程构建清单。Test执行本工程EditMode测试；报告/日志写仓库 `.local/unity/SushiWorkshop-<时间>-<模式>/`。Build采用 `BuildOptions.StrictMode` 普通Mono包，已去掉Development调试包选项以避免调试网络，写 `Builds/Windows64/SushiWorkshop.exe`、配套文件及 `build-summary.json`；真实结果以开发记录和BuildReport为准。

Player启动需携带完整构建目录；不能只复制exe。相机诊断的 `-captureOnce` / `-captureDirectory <目录>` 只捕获镜头，不提交玩法命令，也不证明UI、真实输入或声音通过。无须用该模式游玩。

实际测试/构建和相机证据可由 `scripts/record-project-evidence.py --project 032 --tests <真实XML路径> --capture <真实相机图路径>` 归档为开发记录目录的 `evidence/test-results.xml`、`scene.png` 和 `build-manifest.json`；该脚本检查测试/构建结果并记录来源/运行哈希，不替代真实窗口QA。首次工程初始化工具为 `scripts/create-first-batch-project.py --project 032`，已存在工程不为重试而重新生成。

最终测试 [XML](../../../../docs/development/proto-032/evidence/test-results.xml) 已核对30项通过/0失败：22项规则覆盖SUS-A01–A05、整份30/Quick27黄金输入，8项存储覆盖恢复/故障/有效备份与非法新Save拒绝。最终普通Mono Strict包86,842,449字节、0错误/1警告；完整输出与源码哈希见开发记录。

## 控制合同

已只读核对 [SushiRuntime.cs](Assets/SushiWorkshop/Presentation/SushiRuntime.cs) 的绑定，真实键鼠验收单独记录：

- 数字1–6或场景鼠标：选择槽/盘（Core槽0–5对应画面1–6）。鼠标按钮：装料、交付/保留、基础出售、清洗、转卖/购站、弃单；I投料、C洗一盘。
- 搬站：选源槽 → 点“搬站” → 选目标；确认合法后才扣费。购置/移动/转卖、交付/基础出售标记和弃单只在准备/圈边界窗口；付费装料/清洗可在未结束的合法单步间使用，连续先暂停。
- Space：推进一步；Enter：开始/续圈；P切换连续/暂停。圈边界停在准备窗口，运行不开放改站/调标。
- S保存；L读取完整检查点；R立即重试当前订单；H关键事件复盘；Tab帮助；M音乐开关；F5完整窗口截图，保存为 `Application.persistentDataPath/player-window.png`。Esc先关闭帮助/复盘，否则退出确认，退出不直接删除保存文件。

H显示最近十条加工、切分、切分阻塞、拒收、离席、交付，逐行列步骤/盘/槽/原因；打开暂停，H或Esc关闭，关闭后主动续跑。复盘只读已提交事件，不重新加工、发奖或收款；真实面板可读性与输入隔离见开发记录。

订单卡显示当前需求、份量、目标和等待；界面成本读Core。“整份对照/小份对照”与R立即重置，当前没有事前确认；新轮第一次合法操作前可L读取旧检查点，此后自动保存更新它。十八步统一结算，快速订单十二步离席后仍可在合法窗口做基础补救；订单完成不提前结束整轮。资金、盘/入口容量或阶段不满足时拒绝整个动作并提示原因。一次键鼠输入只提交一个命令；单步与连续共享同一Step。

## 完整保存与未实现内容

Core安全点快照包括订单、等待、盘/料理/份量/标记/归属、入口队列/库存、工站/调整预算、资金/收入、步数/阶段及事件/命令记录；每次成功领域命令后自动保存，S可手动保存。磁盘目录为 `Application.persistentDataPath/checkpoints`，使用 `PrototypeKit.CheckpointStore<T>`：Save先校验新领域状态，再将 `session.json` 完整payload/格式版本/SHA256写入pending并替换。仅当旧主档经摘要/领域校验合法时才更新 `.bak`，坏主档不能覆盖唯一有效备份；读取验证后回退。恢复不重新抽单或返还消费。自动保存失败原因优先保留，“保存并退出”失败不关窗口，允许返回/重试；存储定向回归已通过，实际窗口保存/退出待补，不承诺硬件断电绝对安全。

字体现用资源、七项本仓原创程序短音与原创BGM共九份源/运行副本逐一SHA256一致，两份字体声明哈希匹配，见 [运行资源登记](../../../../sources/art/proto-032-sushi-workshop/runtime-resource-register.json)。真实相机文字尺寸已正常；最终包与初态相机图已归档，实际听感未验。普通Player窗口启动遭Windows防火墙安全权限弹窗阻挡，工具政策不能代理代操作，已请求用户手动取消但仍未关闭，所有原生操作待补，不能称完整UI/QA通过。

本轮不是完整Steam游戏。多环、员工/餐厅经营、随机局外成长、真实切菜、完整音乐/配音/剧情、完整手柄与低配优化/Steam上传均未随本纵切完成。SUS-H01–H03策略与乐趣仍需玩家证据；自动测试数量、构建和相机图各自只证明相应范围。

当前候选已实测30/30规则/存储测试与Windows构建0错误1警告，构建86,842,449字节；初态场景图已目检，原生键鼠/听感受系统安全权限弹窗阻挡而待补。运行本机Builds/Windows64/SushiWorkshop.exe，需保留完整同目录Player文件；Git保存源码/源资产/证据，按上述Build命令可重建。


## 2026-10-10 工程身份与备份复核

2026-10-10工程身份复核：productGUID改为独立UUID5 `0886d11784ca5aa68a5a556f89533227`；最新普通Mono包86,842,449字节、0错误/1警告，UTC `2026-10-09T18:42:23.3657002Z`，日志 `.local/unity/SushiWorkshop-20261010-024207-652-Build/unity.log`。最新相机初态已实际目检，PNG SHA `f5e4e1bc93fd368840e078bc331142728f241feab4703b251d4f1265722555bb`；完整原生/HUD/动态/听感仍未验。规则/API/初值未改，沿用本项目已通过的30项真实XML，未重复计次；032仅两文本换行归一，不改变游戏语义。详见[本批复核](../../../../docs/development/first-batch-backup-audit.md)。
