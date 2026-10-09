# live-avatar 音频生产能力参考登记

版本：v1.0；日期：2026-10-09（Asia/Shanghai）。状态：只读来源审查；关键文件哈希采集时间为 `2026-10-09T17:36:10+08:00`。本轮没有调用付费服务、安装软件、启动参考工程、读取密钥内容或复制其音频资产。

**用户明确方向**：参考本地 `F:\workspace\github\live-avatar` 的音频生产能力，为套圈改造摊后续开发提供依据。**本登记的助手建议**：借鉴 TTS 的生产与验证方法，把角色短句作为可选资产；音乐、环境与事件音继续执行本仓独立生产和逐项来源审核。完整角色配音不是必要条件。

本登记对应 [09 美术与资产生产](../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md)、[12 开发准备与缺口](../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md)、[13 内容生产与验证](../../docs/design/first-batch/proto-013-ring-toss/full-game/13-content-authoring-and-validation.md)。当前任务只新增来源记录，不将下面的接入建议写成已经实现或通过验收。

## 1. Git 与本地文件快照

- 原目录：[F:/workspace/github/live-avatar](F:/workspace/github/live-avatar)。origin 为 `git@github.com:angelash/live-avatar.git`，对应 [angelash/live-avatar](https://github.com/angelash/live-avatar)。
- HEAD：`fbf1111958fd718205b9a0ece040d84ebd11aab4`；[固定提交](https://github.com/angelash/live-avatar/commit/fbf1111958fd718205b9a0ece040d84ebd11aab4)。作者时间与提交时间均为 `2026-08-15T22:59:06+08:00`；标题为 `feat: default to warm girl voice`。
- 原仓 [AGENTS.md](F:/workspace/github/live-avatar/AGENTS.md) 将 [HeiXia2077/live-avatar](https://github.com/HeiXia2077/live-avatar) 记为上游来源。这里引用该来源说明，没有另行核实上游远端最新状态。
- 定向 `git status` 检查 AGENTS、README、DEPLOYMENT、architecture、`integrations/s2s/`、启动/验证脚本、MiniMax 测试和 LICENSE。仅 [README.md](F:/workspace/github/live-avatar/README.md) 与 [docs/DEPLOYMENT.md](F:/workspace/github/live-avatar/docs/DEPLOYMENT.md) 显示 ` M`；读取的是本地修改稿，其内容不归到上述 HEAD。
- 提交、时间和状态来自本地 Git；本轮未拉取远端，不宣称整个外部工作区干净或远端内容最新。以下 SHA256 对应实际本地文件，可用于以后重新读取前复核。

## 2. 先读的约定、文档与技能情况

1. [AGENTS.md](F:/workspace/github/live-avatar/AGENTS.md)：要求运行部署修改同步源码和 runtime，并用其托管重启与部署检查；这是参考工程的运行约定。只读审查不会启动或重启这套服务，套圈单机资产生产也无需采用四服务部署。
2. [README.md](F:/workspace/github/live-avatar/README.md) 与 [docs/DEPLOYMENT.md](F:/workspace/github/live-avatar/docs/DEPLOYMENT.md)：说明实时语音链路、Windows/Qwen3/MiniMax 选项、配置和验证入口。选择理由是能区分语音服务接入与文件资产制作，两文件均按工作区修改稿登记。
3. [docs/architecture.md](F:/workspace/github/live-avatar/docs/architecture.md)：用于理解 STT、LLM、TTS、LiveTalking 与浏览器的职责。该文仍含旧的 Qwen 单路线、浏览器双音频和会话路由说明，不能单独作为最新运行状态依据；实际选项应核对较新的 README、部署说明与执行代码。
4. 对包含隐藏目录的文件清单检索未找到项目 `SKILL.md`。没有发现可直接调用的 live-avatar 仓内音频技能，不把文档里的工具描述当作本会话已提供的 MCP 能力。
5. 另行读取了已安装的 [minimax-tts/SKILL.md](C:/Users/Lenovo/.codex/skills/minimax-tts/SKILL.md) 与 [batch-manifest.md](C:/Users/Lenovo/.codex/skills/minimax-tts/references/batch-manifest.md)，并检查其 [minimax_tts.py](C:/Users/Lenovo/.codex/skills/minimax-tts/scripts/minimax_tts.py) 的入口与关键处理。它属于全局技能，不在 live-avatar Git 快照内；用途为语音文件生成，不用于音乐生成。

## 3. 实际音频入口与边界

### 3.1 MiniMax 实时 TTS 适配器

原文件：[integrations/s2s/minimax_tts_handler.py](https://github.com/angelash/live-avatar/blob/fbf1111958fd718205b9a0ece040d84ebd11aab4/integrations/s2s/minimax_tts_handler.py)。已阅读全文；选择理由是包含真实请求、PCM 解码、分块、取消、错误与有限重试逻辑。

- 调用 MiniMax T2A v2；源码默认 endpoint 为 `https://api.minimaxi.com/v1/t2a_v2`、模型为 `speech-2.8-turbo`、声音为 `Chinese (Mandarin)_Warm_Girl`。这些是读取时的默认字符串，不代表核实了服务当前型号、价格或账户可用性。
- 依赖 `MINIMAX_API_KEY`，缺失时明确报错。请求为单声道、16 kHz PCM 的流式 SSE；解码后按样本块输出，末块补零，并处理取消或过期回合，主要供外部 speech-to-speech 与 LiveTalking 使用。
- 该 handler 输出音频块，没有独立的 WAV/MP3/OGG 文件导出命令；依赖外部 `speech_to_speech`、httpx/numpy 等。不能把文件存在写成单机游戏资产生产已就绪。
- 声音白名单、取消令牌和“输出音频后不重试”值得借鉴；输出前允许有限重试仍不能证明服务端未计费，不直接移植为付费文件批处理的重试策略。
- [scripts/start_s2s.bat](F:/workspace/github/live-avatar/scripts/start_s2s.bat) 的实际默认声音赋值是 Warm Girl，部分旧注释仍写 Gentle Senior；核对时以执行赋值为准。实际选择的 backend 未读取私有配置核实，不能据默认值推断正在使用 MiniMax。

### 3.2 Windows 本地 TTS 适配器

原文件：[integrations/s2s/windows_tts_handler.py](https://github.com/angelash/live-avatar/blob/fbf1111958fd718205b9a0ece040d84ebd11aab4/integrations/s2s/windows_tts_handler.py)。已阅读全文；选择理由是提供没有云 API 请求的本地预览替代。

- 使用 Windows PowerShell 与 `System.Speech`，源码默认声音为 `Microsoft Huihui Desktop`，生成 16 kHz、16-bit、单声道 PCM 后分块输出；整句先合成，不能与 MiniMax 网络流式首块延迟混同。
- 这条源码路线不依赖 MiniMax 密钥或 CUDA，但本轮没有检查已安装声音、实际合成效果或商用声音再分发依据；不把它标记为已通过生产验收。
- 它也不是独立文件导出 CLI。可借鉴为本地试听或占位语音，正式采用仍需本仓导出、文件检查与来源登记。

### 3.3 Qwen3-TTS 既有接入

入口：[scripts/start_s2s.bat](F:/workspace/github/live-avatar/scripts/start_s2s.bat)、README 与 DEPLOYMENT。启动代码的 `TTS_ENGINE` fallback 为 qwen3，模型名 fallback 为 `Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice`；文档另说明可选的 `Qwen/Qwen3-TTS-12Hz-1.7B-Base` 路线。

实际模型和 handler 位于外部 speech-to-speech 依赖，不因本仓启动脚本存在就认定模型已下载、推理已运行或声音克隆已授权。本轮未查询模型缓存或 GPU 服务，也未采用任何参考声音。完整数字人链路的硬件需求不能直接变成本游戏离线音频制作的最低配置要求。

### 3.4 已安装 MiniMax 文件生产技能

[minimax_tts.py](C:/Users/Lenovo/.codex/skills/minimax-tts/scripts/minimax_tts.py) 有 single/batch/a1 入口，single/batch 可将同步接口返回的十六进制音频写为 MP3/WAV/FLAC，检查文件签名，并在可用时用 ffprobe 核对时长。批量路线先完整暂存，再替换目标文件；manifest 支持稳定目标名、共享 defaults 及每项文本或文本文件。

- 技能脚本的源码 fallback endpoint 为国际区 `https://api.minimax.io/v1/t2a_v2`，与 live-avatar handler 的大陆区不同；默认文件参数也与实时 16 kHz PCM 不同。正式调用前确认账户区域与采用参数，不能自动混用 endpoint 和 key。
- 凭据可来自进程环境、显式配置、`MINIMAX_CONFIG_FILE`，脚本也有查找 live-avatar 本地配置的 fallback。本轮只读代码和示例，不执行这条解析路线、不读取实际 `config.local.bat`。
- 技能要求生成前 dry-run 核对任务数、字符量、路径和设置；但 dry-run 也可能解析本地凭据，本次只读审查没有运行。实际生产按具体任务和既有授权推进，服务额度、计费权限等外部条件单独核实。
- 新文件默认不覆盖；授权重做才针对具体输出使用 force。同步调用结果不明时停止并查明，不自动重复计费。游戏接入可用 single/batch，不需要其 A1 专用内容路线。
- 文件生成机制存在，尚无本轮生成或试听结果。TTS 文件写入成功不等于人物声音、情绪、响度或 Unity 混音已经合格。

### 3.5 BGM、环境与音效

通过文档、相关脚本与文件清单审查，未找到 live-avatar 独立的 BGM、环境循环或游戏短音效生成入口；已安装 minimax-tts 技能也明确不处理音乐生成。本会话工具元数据中未发现名称或描述包含 music/tts/audio/minimax 的服务工具；这仅说明本会话可见工具情况，不证明其他环境永远没有这些能力。

因此，live-avatar 可提供语音方法参考，不能补齐 05 的五音乐、三环境循环、二十六事件音。上述资源继续由助手按 09 的原创程序合成/编排或有商业发行依据的素材路线生产；WoC 音频候选由其独立来源登记决定，不能由本登记替它们授权。

## 4. 验证机制与已有产物证据

- [tests/test_minimax_tts_handler.py](https://github.com/angelash/live-avatar/blob/fbf1111958fd718205b9a0ece040d84ebd11aab4/tests/test_minimax_tts_handler.py)：用模拟响应和测试 key 检查 PCM 块、末块补零、错误、声音 fallback 与输出前重试。适合参考测试边界；本次未执行，不计为服务连通或音频成品证据。
- [scripts/smoke_test.py](https://github.com/angelash/live-avatar/blob/fbf1111958fd718205b9a0ece040d84ebd11aab4/scripts/smoke_test.py)：通过真实 WebSocket 会话统计语音响应、字节数及会话释放。输出 JSON 摘要，不保存音频，不验证游戏循环、响度或听感；本次未运行。
- [scripts/verify_deployment.ps1](F:/workspace/github/live-avatar/scripts/verify_deployment.ps1)：检查四服务及前端状态，`-Conversation` 可调用上述真实会话。会读取其私有运行配置，若当前 backend 为 MiniMax 可能计费；不用于本次只读审查，也不是本游戏资产验收入口。
- [scripts/install_minimax_tts.ps1](F:/workspace/github/live-avatar/scripts/install_minimax_tts.ps1)、[scripts/install_windows_tts.ps1](F:/workspace/github/live-avatar/scripts/install_windows_tts.ps1)：安装到外部 S2S runtime 并修改其接入。读取入口用于识别依赖，没有执行或把安装动作作为生成游戏文件的前置步骤。
- `git ls-files` 的音频后缀检索未找到已跟踪的 WAV/MP3/OGG/FLAC/M4A 或 SKILL 文件。包含 ignored 目录的文件名扫描只发现 `.codex-temp/s2s-binding-base/src/speech_to_speech/TTS/ref_audio.wav` 与 `.codex-temp/s2s-binding-check/src/speech_to_speech/TTS/ref_audio.wav` 两份参考音频；未打开、播放或复制，不把它们记为 MiniMax 输出或可用游戏声音。
- 本次没有找到可归属的已生成游戏音频文件或已保存成功报告。源码、测试和验证入口存在属于方法证据，不能表述为实际云服务、声音授权或完整生产链通过。

## 5. 凭据、许可与不直接迁移的内容

1. 只读了占位示例 [scripts/config.local.bat.example](F:/workspace/github/live-avatar/scripts/config.local.bat.example)，未读实际密钥配置、账号、余额或服务日志。凭据存在、账户区域、额度和当前授权未核实，不能写成缺失或可用。
2. [LICENSE](https://github.com/angelash/live-avatar/blob/fbf1111958fd718205b9a0ece040d84ebd11aab4/LICENSE) 为 MIT，版权记录为 live-avatar contributors，2026。复制或改写其许可范围内代码需保留相应许可；本轮只原创概括方法，不复制代码或全文。
3. MIT 不自动许可第三方模型、声音、克隆样本、服务输出或数字人媒体；正式语音采用前核对所选服务/模型的商业使用、输出和声音约束，不在当前审查里臆造许可结论。
4. 不移植 STT、LLM、wav2lip、LiveTalking、WebRTC、四端口部署或会话控制到离线单机游戏；它们解决数字人实时会话，与本游戏预制声音播放无直接必要关系。
5. 不复制参考人物声音样本，不做真人仿声，不让剧情可读性依赖完整配音。语音关闭或缺少可选语音时，原有对白、字幕和必需反馈仍完整。

## 6. 最小本仓接入建议与完成标准

下面是助手建议，尚未实施；不增设开发审批门槛，具体制作任务沿用户已有授权执行。

1. **先做必要无配音音频**：沿 09 交付一个章节曲、环境循环和代表事件音，用可重现作者源、WAV 源与 OGG 发行文件建立生产链。测试循环接缝、削波、并发、暂停恢复及真实 Player 混音；不等待云 TTS。
2. **短句可选**：一章纵切确有情绪收益时，仅选少量开场/章末短句，保留完整字幕，不扩大为六人物全文配音。优先复用已安装 minimax-tts 的 single/batch 路线；Windows 本地声音可作为待验证预览替代，不预先承诺正式商用声音。
3. **明确产物和来源**：以本仓稳定音频 ID 生成文件；记录文本版本/行 ID、角色、服务或模型、voice ID、区域、有效参数、生成日期、来源许可依据、源和最终 SHA256。不得把 API key、私有配置或完整服务响应写进资产清单和 Git。
4. **文件与导入验证**：先确认输出可解码、声道/采样率、峰值、时长、句末完整及无额外静音，再按 09 转成统一发行格式、入本仓 Audio 路径并核查 Unity 引用。TTS 的 16 kHz 实时分块需求不变成本游戏离线资产规格。
5. **听感与模式验证**：检查吐字、角色区分、字幕同步、音量总线、跳过/快进、中断与重播去重；必需信息仍可静音读取。最终来源登记闭合并有实际 Player 结果后才标该项完成。
6. **技能建议**：本仓现有 ring-toss-asset-pipeline 可后续增加“可选文件 TTS → 已安装 minimax-tts → 本仓资产 QA”的薄路由；不复制 live-avatar 的技能正文，也不新建重复 TTS 技能。若将来整理音乐/音效工具，技能应引用实际本仓脚本与合同，而不是臆造音乐 MCP。本轮没有修改技能或安装全局技能。

## 7. 关键文件 SHA256

以下相对原文件名以 `F:\workspace\github\live-avatar` 为根。README、DEPLOYMENT 两项为本地修改稿，其余是本轮选定路径中未显示修改的文件；哈希只用于版本复核。

- `README.md`：`F19039A706645ADC15418616EE3D9F7CBBACD85810CB4886F67F394807D836C9`。
- `docs/DEPLOYMENT.md`：`7EAEDC9D5F5C16174883025472A6D5767D7F997F9A5D82247015BCB67F4FC9AD`。
- `integrations/s2s/minimax_tts_handler.py`：`9AF56B3694806CE4E2E7AB612658F4B4471132C4FF46C6FE016A9BF944F6FB70`。
- `integrations/s2s/windows_tts_handler.py`：`976D28043AE82024B22D0F3181FC2117C1C3CA30BC156D167919EA468BA7EBF4`。
- `scripts/smoke_test.py`：`ADA3BDFC13A88D1B4B02A6487E798042B66C5869B4E3F27F91DF895CC73962D0`。
- `tests/test_minimax_tts_handler.py`：`71FA72EA61073AEE6113A32A33932E11569A558CCDF5A8E08C690BEA2CDB57BE`。
- `LICENSE`：`877848A35EFC22499ECD19E07883E6F88FC5AACE6B4FFA7D3C1178B149A91D2B`。

已安装全局技能另列，不归入上述 Git 提交：

- `C:\Users\Lenovo\.codex\skills\minimax-tts\SKILL.md`：`42FE736F4B9C68A7D72D82B44E4E92E88F8CD4990E53EDCE62830B7C986D63B4`。
- `C:\Users\Lenovo\.codex\skills\minimax-tts\scripts\minimax_tts.py`：`F86D65729EF0F04BB96A93B993FDB87A86B8D0A2F48978EA3AF0CCD8C27418B2`。

外部原始资料和参考音频留在原仓；本文件保存引用、筛选理由、适配建议与事实边界，不拷贝全文或资产。
