# 013 套圈改造摊：开发要求与落地状态

归档版本：v0.6；日期：2026-10-09（Asia/Shanghai）。当前事实快照：提交 `d086cfc5d1b14659c5194fd0d3bd5c88a6ee5daa`，首个真实三维单摊 P0 已有代码、资源、规则测试与 Windows 开发构建，正常三维键鼠验收仍待完成。

通用开发、协作、质量、来源与备份要求统一遵守 [仓库开发规范](../../DEVELOPMENT_STANDARD.md)。本文件只登记 013 的产品约束、实施基线、证据和接续工作；后续事实更新写入 [开发记录](../../docs/development/proto-013/2026-10-09-p0.md)，参数与内容仍在下列权威入口维护。

## 用户决定与实施建议

- **用户已决定**：使用本机现有 Unity，制作 PC 单机游戏，目标 Steam；主体是真实三维场景与斜视镜头，玩家在镜头前向前抛圈，呈现地摊套圈的空间感。后续程序、关卡、剧情、资产、测试修复、构建和发行材料由助手处理。
- **本机已核实并已用于构建**：Unity `2022.3.62f3c1`，revision `1623fc0bbb97`；编辑器为 `D:\Program files\2022.3.62f3c1\Editor\Unity.exe`，已有 Windows x64 Mono 模块。当前工程在 [game/RingTossWorkshop](game/RingTossWorkshop/README.md)，不自行升级引擎或假定 IL2CPP 工具链可用。
- **助手当前实施建议**：固定后上方透视相机、地面两排三列六槽、近处持圈手、暖灯夜市、帆布摊地与条纹棚、轻覆盖 UI，不增加自由漫游。现有镜头位置 `(3.2,4.2,-5.8)`、看向 `(0,0.3,4.5)`、FOV `44°` 是待实投校准的参数，不能写成用户指定或已通过手感验收。
- **历史保留**：[原需求](../../docs/design/first-batch/proto-013-ring-toss/requirements.md) 与 [原设计](../../docs/design/first-batch/proto-013-ring-toss/design.md) v0.1 保留原案及来源。旧二维占位、单排 XY 几何与固定正交 2.5D 建议已被用户的新方向替换；旧二维回归结果继续保留，不能代替三维验收。

## 产品合同与权威入口

- 完整游戏范围、剧情与界面在 [full-game 入口](../../docs/design/first-batch/proto-013-ring-toss/full-game/README.md)；玩法和阶段权限读 [02](../../docs/design/first-batch/proto-013-ring-toss/full-game/02-gameplay-systems.md)，十二摊固定候选与解锁替换读 [03](../../docs/design/first-batch/proto-013-ring-toss/full-game/03-content-and-levels.md)。完整方案仍含建议和未验证假设，P0 不等于十二摊成品。
- 经济、价格、来源资格与标量参数唯一入口是 [04 数值经济 v0.2](../../docs/design/first-batch/proto-013-ring-toss/full-game/04-economy-and-balance.md)。当前三维方向是 [场景合同 v0.6](../../docs/development/proto-013/3d-scene-direction.md)，三维几何和输入参数是 [Core 三维说明／Rules3D 0.6.0](game/RingTossWorkshop/Assets/RingToss/Core/README-3D.md)。04 的旧二维几何只作历史；不在界面、模型或本归档另维护碰撞和经济参数。
- 正式投掷必须稳定可复现。当前权威为纯 C# `Flight3D`，固定步 `1/120s`；左右角、仰角和速度分别量化。无隐藏命中概率、吸附或临投随机变化；奖品接受圆盘、侧柱、机关作用区与表现共用三维锚点。Unity 网格、Collider、动画和相机不得发奖或替换规则碰撞。
- 一摊固定六槽；命中后兑现或留场互斥。`wallet` 和 `stageReceipts` 分账，旧钱与旧物卖出不自动抵达本摊目标。点击“开摊”确认冻结目标、新奖品和初始布置；此前合法机关调整免费，此后按成功动作扣预算。末圈先处理中奖与处分，再提供合法拆卸/补救窗口，最后结算。回放独立使用发射前快照，不改变正式圈、账、耐久或奖励；重复命令不重复消费事件。
- 跨摊与完整版接入时，沿 03/04 的固定候选、未解锁确定替换、0–2 件预装覆盖及获取价快照规则。按实际剩余新奖品生成并冻结目标，禁止卖空或重复处分刷收据。对应有限解锁/预装覆盖、黄金输入和内容准入严格采用 [13 内容验证合同 v0.6](../../docs/design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md)，不能用六槽样本冒充全部可达。
- 保存、事务恢复与局外奖励资格按 [06 v0.3](../../docs/design/first-batch/proto-013-ring-toss/full-game/06-technical-save-and-accessibility.md)；Steam 接口与整体检查点按 [11](../../docs/design/first-batch/proto-013-ring-toss/full-game/11-steam-release-plan.md)。本地离线流程先完成，平台回调不拥有经济状态，不用 Cloud 字段合并代替完整有效存档恢复。

## 工程与资产要求

- 分层沿 [08 Unity 技术方案](../../docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)：Core 提供规则与有序事件，Application 持有正式单摊状态，Presentation 读取并提交命令。二维/三维共用一个经济层；UI、音效与回放不能再建账目或重复派奖。操作、运行入口和实际接口见工程 README 与 [Application 范围](game/RingTossWorkshop/Assets/RingToss/Application/README.md)。
- 实际 [manifest](game/RingTossWorkshop/Packages/manifest.json) 与 [包锁](game/RingTossWorkshop/Packages/packages-lock.json) 使用 Test Framework `1.1.33` 及 audio/imageconversion/imgui/jsonserialize/screencapture/physics 内置模块。当前为 Built-in 表现、Legacy Input、IMGUI 与中文字体；08 的 Input System、URP、TMP 是未来候选，尚未导入或验收，不当作现有依赖。
- 制作按 [09 资产规格](../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md)、[Unity 技能](../../.agents/skills/unity-pc-prototype/SKILL.md) 与 [资产技能](../../.agents/skills/ring-toss-asset-pipeline/SKILL.md)。保留 Blender 源、FBX、预览、稳定资产 ID、哈希与 `.meta`；校准米制单位、Y-up 导入、底心枢轴和真实功能面。远排不因透视缩小暗中放大接受区，棚/手/前排道具不得遮关键路径。
- 当前 P0 模型、短音和字体实际在 `Assets/RingToss/Resources/Models|Audio|Fonts/`；完整资产分类目录仍是 08/09 的实施计划，迁移时同步引用与来源，不把规划目录写成现状。占位资源可服务原型；一章纵切需代表最终品质的样件，发行内容不得带缺失必需资源、未清理许可或冒充成品的占位。
- 本机 Blender、Python 与音视频编码链已有准备探针和首组生产证据，能力范围见 [准备审计](../../sources/references/development-capability-audit-2026-10-09.md)。ImageGen 可用于后续二维内容；付费生图、image-to-3D、语音服务和平台权限仍逐项核实，不假定已具备。
- `highschool`、`world-of-claudecraft`、`live-avatar` 保持只读，版本与筛选理由见 [参考登记](../../sources/references/local-project-reference-register.md)。只借鉴适用方法；媒体逐项审许可。WoC 本轮十四项音频未取得独立商业转用依据，未复制；live-avatar 是 TTS 方法参考，未提供可归属的 BGM/音效，不搬入其服务或凭据。

## 当前已落实的范围

以下是上述提交快照的实现事实，详细证据集中在 [2026-10-09 开发记录](../../docs/development/proto-013/2026-10-09-p0.md)，后续结果由记录续写。

- **工程与规则**：独立 Unity 工程、单场景启动和 Prepare/Test/Build 入口；真实三维六槽、普通圈、风扇/反弹板、单摊处分/结算、两本账、准备边界、两次开摊后调整、补圈、隔离回放。当前无风，初始三台扇/三板材总新奖品价值 210、目标 105、八圈；这些是初始建议，尚不是实测难度。
- **场景与模型**：程序制作摊布、棚、暖灯、河栏、远景、柜台和近处手；五个原创 Blender 模型为台扇、板材、鸭、杯和收音机，已保留 `.blend`/FBX/预览并接入 Resources。鸭、杯、收音机目前是柜台装饰；完整人物、十二皮肤与三章场景未完成。[模型及往返证据](../../sources/art/proto-013-ring-toss/3d-p0/README.md)
- **声音与字体**：七个原创程序短音已复现和接入，Player 载入日志为 `audio=7`；中文 Noto Sans CJK SC Version 2.004 已载入，OFL1.1 与 Adobe 版权声明进入 StreamingAssets 随包。波形/载入不证明听感或完整混音。[音频来源](../../sources/references/woc-audio-reuse-register.md)；[字体随包声明](game/RingTossWorkshop/Assets/StreamingAssets/ThirdPartyNotices/)
- **规则测试**：Unity EditMode **48/48 通过**，由旧二维 30 项和新增三维 18 项组成。新增覆盖六槽各自输入、XZ 圆盘边界/横向偏离、连续侧撞、有限板、风扇无 X 吸附、账目、耐久、末圈与回放样本；尚不是全解锁/预装或十二摊覆盖。[实际 XML](../../docs/development/proto-013/evidence/3d-editmode.xml)
- **构建与画面**：Windows x64 Mono Development 构建成功，BuildReport 为 **0 error、0 warning**；三维 Player 已实际启动并载入资源，无已记录运行时异常。[整包与源码哈希清单](../../docs/development/proto-013/evidence/build-manifest.json)；[相机渲染图](../../docs/development/proto-013/evidence/3d-scene.png) 用于场景检查，不含 IMGUI，也没有执行玩法输入。
- **尚未通过的验收**：当时桌面锁屏，正常三维键鼠、UI 完整单摊、功能机关画面对齐、实际听感仍待复测。`-ringCaptureOnce` 的 30 帧相机捕获/90 帧退出不发游戏命令；历史二维实际窗口 QA 和这张相机图均不能填补该缺口。F5 截图快捷键也未登记为通过。

## 下一阶段与完成标准

助手按 [12 缺口计划](../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md) 连续处理；阶段验收的产物如下，不要求用户代写或制作内容。

1. **完成三维 P0 实机闭环**：在可用正常桌面验证近/远及左右目标、三项瞄准、开摊/发射/处分、转向和作用区、末圈/补救/结束、暂停/回放、焦点与退出。首次开摊保留八圈，正式首投才变七圈；回放不改账与耐久。核对 720p 与代表宽高比可读性、手/棚遮挡、命中和机关位置、耳机/扬声器反馈；保存真实窗口画面、输入与事件记录及构建版本。当前锁屏仅是实机条件，不能通过相机图将此项勾为完成。
2. **建立保存与一章纵切**：实现 06 的整体检查点、幂等提交、备份恢复与错误 UI；故障注入涵盖写入/校验/提交和进程中断，不损坏真实存档或唯一有效备份。再连首章四摊、跨摊携带、准备商店/修理、笔记解锁入口与冻结快照、完整章节文字和代表资产；交付按 13 关联的内容摘要、普通圈可达证据及纵切实际开放组合覆盖，并离线从新局走到章结算/失败恢复。
3. **补齐完整游戏与发行候选**：其余四机关、两特殊圈、十二摊标准/有限变体、练习/挑战、完整剧情/图鉴、正式资产、BGM/环境/混音、设置/重映射/手柄、保存迁移、低配性能与打包检查目前未实现或未验收。按 13 完成全部合法解锁/预装边界与内容 lint，按 11 完成真实 Windows 离线全局流程和商店材料。Steam SDK、AppID、客户端/Cloud/成就及商店提交没有接入或发布；实际身份、签署、付款和平台权限按真实外部条件记录，功能只有实测后才对外承诺。

投掷学习成本、兑现/留场取舍、两次调整的充分性、局长与音乐耐听性继续是体验假设。代码、账目核算、构建或一次画面检查只能提供各自范围的证据；需记录实际试玩观察后再修订权威版本。
