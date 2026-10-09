---
name: ring-toss-asset-pipeline
description: 为套圈改造摊制作、整理和验收 Blender 三维场景与低模旧物、可选二维人物、UI 和音频资产，并准备 Unity 导入；素材替换不改变玩法判定。
---

# 套圈改造摊素材管线

本技能路由 Blender 三维低模旧物、二维人物/背景、UI 与音频素材工作；规格和资产数量以 [09 美术与素材生产](../../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md) 为准，功能标记和界面以 [05 视听与交互](../../../docs/design/first-batch/proto-013-ring-toss/full-game/05-art-audio-ux.md) 为准。角色语气按 [01 人物](../../../docs/design/first-batch/proto-013-ring-toss/full-game/01-world-story-characters.md)，Unity 接入按 [08](../../../docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)。

## 处理一次素材请求

用户已明确选择真实三维斜视地摊。当前场景、圈与奖品必须有三维体积；接触和表现共用 Rules3D 的接受圆盘、有效板面、风场与槽锚点。旧固定正交 2.5D 路线保留为历史，不继续限制新制作。Blender 根节点包围盒居中不能代替实际功能面/风头对齐；相机渲染证据和真实键鼠操作分别记录。

先读 `AGENTS.md` 与已有同类素材、清单和来源记录，确定用户要概念稿、生产候选、修改还是导入。沿当前授权完成工作，不把新增一项素材理解为增加机关、剧情分支或付费能力。

依据 [09](../../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md) 建立资产条目：稳定 ID、用途、尺寸与透明度、枢轴/挂点、功能层、来源与许可、原稿与输出、版本以及验收状态。先做能验证风格与可读性的样件，再扩同系列；保存原图与处理记录，不覆盖未经核实的同名文件。

用当前会话实际可用的工具生成或编辑。需要 AI 栅格图片时遵循可用的 imagegen 技能；矢量图标优先维护原矢量，音频按实际支持的合成或录音流程处理。工具或凭据缺失时继续完成资产规格、提示词和可制作部分，并记录具体缺口，不编造 MCP 能力、调用结果或生成文件。

用户已明确后续素材与开发由助手处理。现有 Blender CLI 和 FFmpeg 的基础探针见 [12 能力边界](../../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md)，这不是正式资产验收。音频按 09 第 13 节建立原创编排/合成源、逐资产随机种子、循环/混音与实际 Player 播放检查；宣传视频按第 14 节捕获真实游戏窗口并剪辑，不能把合成编码样片写成实机录像。发现单一桌面通道故障时验证现有替代通道，不默认要求安装新插件或让用户操作编辑器。

三维旧物用机器实际已有的 Blender CLI 生产，保留 `.blend` 源和 FBX 输出，按 08/09 核实单位、轴向、枢轴、材质与 Unity 往返。WoC 的压缩 GLB 须先核对来源及扩展/解码需求，不能当作 Unity 原生可直接导入的格式；优先按 09 获取合法原源并走已验证导出路径。

素材候选要实际查看；三维模型的轮廓、法线、轴向与镜头效果，以及二维素材的透明背景、边缘、枢轴、图标轮廓、文字安全区与最小显示尺寸，依据 09 核验。六种机关和三种圈的功能身份必须清楚；同类皮肤共用规则轮廓，不从网格、PNG 外边缘、动画摆动或自动碰撞生成器推导玩法判定。声音与文字事件对齐，静音或低动态显示仍保留必要信息。

将源文件、可导入文件和派生缓存分开。Unity 导入设置与图集选择读取 08/09，已有工程的 `.meta` 与引用关系要保留。重新生成派生物时同时更新来源、版本和映射；不手改另一份独立的资产清单冒充权威数据。

## 音频与字体路由

1. **WoC 候选筛选**：先读 [逐文件音频登记](../../../sources/references/woc-audio-reuse-register.md)，新候选再核对作者、原文件名、实际来源、许可、源快照与 SHA256。代码 MIT、生成器存在或用户希望复用不自动取得第三方媒体转授权；当前核查的项目限定、CC BY-NC 与未登记音频没有复制，角色台词不转给本游戏角色。
2. **P0 原生事件音**：本仓 [generate-prototype-audio.py](../../../scripts/generate-prototype-audio.py) 已制作 `launch/hit/bounce/cash/retain/miss/ui_confirm` 七个独立 seed 的原创 WAV，源为 `assets/proto-013-ring-toss/audio/original/`。生成与只读 `--check` 用法见 [scripts/README](../../../scripts/README.md)。这组候选只覆盖基础反馈，不等于 05 的五曲音乐、三环境循环和二十六事件音；当前 BGM与环境循环仍未接入。
3. **Unity 资源接入**：当前运行副本位于 `prototypes/proto-013-ring-toss/game/RingTossWorkshop/Assets/RingToss/Resources/Audio/`。从已清理源同字节复制，保留原文件名及已有 `.meta` 引用，再逐项核对源/副本 SHA256；转码属于新派生版本，另记来源、设置与哈希。文件复制和哈希通过不等于 Unity 引用、真实 Player 听感或事件同步通过。兑现/留场只随已提交事件，回放、恢复和重复确认不能伪造新的领奖提示。
4. **可选对白 TTS**：只在素材任务需要少量对白短句时，读取 [live-avatar 能力登记](../../../sources/references/live-avatar-audio-capability-register.md)，再按实际可用的 [minimax-tts 技能](C:/Users/Lenovo/.codex/skills/minimax-tts/SKILL.md) 路由到文件生产。核对真实工具、声音与输出使用条件、账户区域和已有授权，不臆造凭据或调用结果；本地 Windows TTS 可作为待验证预览替代。登记文本版本/行 ID、角色、实际服务/模型与 voice ID、参数、源/成品哈希，不写密钥。保留字幕与跳过路径，不扩大成全文配音，不把 TTS 当 BGM/SFX 工具，也不迁移 live-avatar 的实时数字人服务或参考声音样本。
5. **字体声明**：Noto 字体入库时保留未改字体及版权/OFL文本。当前工程 `Assets/StreamingAssets/ThirdPartyNotices/` 已有 `NotoSansCJK-Copyright.txt` 与 `NotoSansCJK-OFL-1.1.txt`；核对实际字体版本/哈希、完整条款和构建随包文件，再验证中文覆盖与小尺寸可读性。声明文件存在不代表 Player 打包或缺字检查通过。

Steam 商店素材请求读取 [11](../../../docs/design/first-batch/proto-013-ring-toss/full-game/11-steam-release-plan.md)，核对当时的官方规格；商店图不宣称未实现的内容，素材准备不自动发布。

## 交付

提供可查看的本地结果、来源记录、使用/导入方式与验收状态。明确区分概念稿、生产候选和已接入验证，不把生成成功等同于美术通过或可发行。阶段 QA、分工与接续记录见 [10](../../../docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md)。
