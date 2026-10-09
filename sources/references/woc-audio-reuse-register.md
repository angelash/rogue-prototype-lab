# WoC 音频复用审核与套圈原型原创音效登记

版本：v0.1；日期：2026-10-09（Asia/Shanghai）。本轮由用户授权优先检查 WoC 可用 BGM/音效，并为基础 Unity 原型准备音频反馈；素材工作由助手完成。本文件是来源与事实登记，不改变 05 的完整音频范围或 02/04 的判定。

**结论**：有限筛选的十四个实际 WoC 音频文件中，没有找到允许用于本独立 Steam 游戏的明确许可。本轮没有复制任何 WoC 音频、角色台词、曲目或录音片段。已在本仓独立制作七个原创程序合成短音；它们是原型候选，尚未完成 Unity Player 播放、事件同步和正式听感验收。缺少可复用 WoC 音频不阻塞游戏开工。

## 1. 参考快照与许可依据

1. 本地来源：`F:\workspace\github\world-of-claudecraft`；origin 为 `git@github.com:angelash/world-of-claudecraft.git`，对应 [GitHub 仓库](https://github.com/angelash/world-of-claudecraft)。HEAD：`cd961d32105186f4938afd2047adee943ff85ae8`；读取日期：2026-10-09。指定 `CREDITS.md`、`public/audio/` 和本轮读取生成脚本的定向 `git status --short` 为空，源仓相关路径未改动。
2. 媒体许可真值是 [CREDITS.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/CREDITS.md)，本地原件 [CREDITS.md](F:/workspace/github/world-of-claudecraft/CREDITS.md:3)。其 SHA256 为 `16734547f9217fe6a1a7bfe89644c7aba73dab818ad5b65553a1846cab498f47`。根 LICENSE 的 MIT 只覆盖代码，未登记媒体不因此获得授权。
3. 许可组 **P**：CREDITS 第 131/137 行分别将普通 `ui_*.mp3` 和 `public/audio/music/*.mp3` 登记为项目资产、仅随 WoC 或其 fork 使用。本游戏是独立作品，未据此获得抽取复用许可。作者登记为 World of ClaudeCraft；音乐由项目自身主题离线渲染后重新混音/母带，UI 目录由本地 FFmpeg 程序生成。生成方法不解除成品媒体的项目限定条款。
4. 许可组 **N**：CREDITS 第 159–185 行将 @jamiecypher 的移动、近战、投射、碰撞、任务、成就等音效登记为 CC BY-NC 4.0；WoC 的单独商业授权只给予 Levy Street，不随 fork 转移。原作者说明包含自身声音设计及 EastWest Composer Cloud、Epic Stock Media、Freesound CC0 来源；其中使用过 CC0 输入不使最终录音自动成为 CC0。
5. 许可组 **U**：没有在 CREDITS 查到所选文件的独立授权条目。保留为未清理候选；不因生成器中存在提示词、文件出现在 runtime pack 或源代码是 MIT 就将其标成可商用。
6. 读取工具：FFmpeg/ffprobe `8.0.1-full_build-www.gyan.dev`。本轮仅对候选运行只读 ffprobe 与 SHA256，不运行 WoC 的生成器、不查找/使用 API 凭据、不修改原仓。

## 2. 生成来源证据与边界

1. [scripts/render_music.mjs](F:/workspace/github/world-of-claudecraft/scripts/render_music.mjs:1)：通过项目 `src/game/music.ts` 的音符/音色、浏览器 OfflineAudioContext 渲染 WAV，成品 MP3 又经过项目方重混音和母带。脚本 SHA256：`baffad4c3ab261dc1d0af2fe66ffbf0ac76f79609a916963c22aa9add677dc4f`。不将程序主题或现有曲目搬来重渲染规避媒体许可。
2. [scripts/gen_ui_sfx.mjs](F:/workspace/github/world-of-claudecraft/scripts/gen_ui_sfx.mjs:1) 与 `scripts/sfx/ui_sfx.mjs`：确定性 FFmpeg 合成、无损中间源和统一音量/编码流程。前者 SHA256：`5d195e986144122b58493a60fb31250b74ba0b78a527fcae640cdf2499f26b20`。本轮只学习流程，没有复制其媒体、参数序列或代码到原型合成脚本。
3. [scripts/gen_sfx.mjs](F:/workspace/github/world-of-claudecraft/scripts/gen_sfx.mjs:1) 说明自然/世界音可通过 ElevenLabs Sound Effects API 生成，已有文件和自录素材有跳过/覆盖规则。脚本 SHA256：`30dd465a18b1e017c63aefa948220d40389ad442a8e8271ec14411e8929b7550`。**存在调用代码不是已核实当前音频来自该工具的证据**，也不证明本游戏具备 API 权限。
4. [scripts/sfx/sfx_prompts.mjs](F:/workspace/github/world-of-claudecraft/scripts/sfx/sfx_prompts.mjs:528) 中 `amb_water` 和 `amb_wind_vale` 有生成提示、循环和立体声规格，但没有所选成品文件的作者/许可清理链。该目录也能接受真实录音覆盖；故二者作者与实际生成工具保持未核实。此文件 SHA256：`71c1d0d68de1f631938f6f142c8cc02f7dd13a16ccc52259bdf373bfed400ee1`。
5. [docs/design/sound_effects.md](F:/workspace/github/world-of-claudecraft/docs/design/sound_effects.md:1) 是设计/生成方式与编码标准，不替代 CREDITS 的媒体许可。`public/audio/sfx/runtime-pack.json` 的文件哈希/播放参数也不授予版权。

## 3. 十四个实际候选的逐文件审核

本节保留原文件名与真实源哈希，没有把被拒候选拷贝进本仓。用途是助手依据原文件语义提出的筛选意图，**尚未试听并确认美术适配**。所有文件 ffprobe 均识别为 MP3；下述码率为音频流码率。

1. `public/audio/main-theme.mp3`：拟筛标题 BGM；作者/实际生成工具未核实，许可 **U**。`178.800000 s`，44100 Hz，双声道，128000 bit/s，2861914 B。SHA256 `fc3f0272d12ffa62068d845a296fdbb6d0bee17be4691f869837da2403c06d6c`。运行代码确实引用该文件，但 CREDITS 的 `public/audio/music/*.mp3` 条目不覆盖此路径；不采用。
2. `public/audio/music/town_eastbrook.mp3`：拟筛温和标题/桥脚 BGM；作者 World of ClaudeCraft，项目离线程序主题加重混音/母带，许可 **P**。`126.487438 s`，48000 Hz，双声道，192000 bit/s，3037491 B。SHA256 `251c46caf6ce591145da1f44feba6daae52e39c4b843e1ad90a4a2c27b5fbdec`；不采用。
3. `public/audio/music/town_fenbridge.mp3`：拟筛河港章节 BGM；作者/方法同上，许可 **P**。`87.952417 s`，48000 Hz，双声道，192000 bit/s，2112402 B。SHA256 `1a94215a28f863a44405b4a1dfa95ba5a678cea78105616b3858904a2f7b8898`；不采用。
4. `public/audio/sfx/amb_water.mp3`：拟筛桥脚/江心水面环境；目录生成规格指向 ElevenLabs 候选方式，但实际作者/成品来源未核实，许可 **U**。`6.000000 s`，44100 Hz，双声道，192000 bit/s，145492 B。SHA256 `f9c58940327c8100fa58c995c08ae57cdfdc69851e48804d2da8c0da65f2da8f`；不采用。
5. `public/audio/sfx/amb_wind_vale.mp3`：拟筛公开风况以外的轻环境底声；实际作者/成品来源未核实，许可 **U**。`8.000000 s`，44100 Hz，双声道，192000 bit/s，193767 B。SHA256 `7cf4c2f6ecc17e10c73a9f826dd4ee1f9713889a6bd5d0218b2dd9e485b8c81f`；不采用，环境音也不能暗示未登记的风况变化。
6. `public/audio/sfx/ui_fish_cast_1.mp3`：拟筛普通圈投掷；作者 World of ClaudeCraft，UI 生成目录的 FFmpeg 合成来源，许可 **P**。`0.500000 s`，44100 Hz，单声道，192000 bit/s，13835 B。SHA256 `e30ada933dc83b7e7dcbb409c82c34ac31f35b27d7fff80336435a7939126297`；不采用。
7. `public/audio/sfx/melee_swing_light_1.mp3`：拟筛短投掷掠过；作者 @jamiecypher，自录/声音设计成品，许可 **N**。`0.343741 s`，44100 Hz，单声道，192000 bit/s，10074 B。SHA256 `310dbef9d73bd2aaf2fa5e681caeadb3f3a84ee2e9512f61fd9ebd5c9977845b`；不采用。
8. `public/audio/sfx/impact_metal_1.mp3`：拟筛圈接触金属/有效命中；作者 @jamiecypher，自录/声音设计成品，许可 **N**。`0.843741 s`，44100 Hz，单声道，192000 bit/s，21985 B。SHA256 `fdfe938a68c823135e1f0401c7b9de7e72fbd41ae51d293b762a6a914d1baac4`；不采用。无编号的 `impact_metal.mp3` 实际不存在，不作为素材记录。
9. `public/audio/sfx/move_land_1.mp3`：拟筛落地/短碰撞；作者 @jamiecypher，自录/声音设计成品，许可 **N**。`0.531247 s`，44100 Hz，单声道，192000 bit/s，14462 B。SHA256 `7c02e556f35bada2a27e63acaa5bb24dd9f56e3b599754a1340face9dd4e974e`；不采用。
10. `public/audio/sfx/ui_coin_1.mp3`：拟筛兑奖兑现；作者 World of ClaudeCraft，FFmpeg 合成目录来源，许可 **P**。`0.390635 s`，44100 Hz，单声道，192000 bit/s，10701 B。SHA256 `33bb9906bfcb056aa3db5064ab533d19e4f39eafe4ada1643f90d501553107e1`；不采用。
11. `public/audio/sfx/ui_arena_loss.mp3`：拟筛未达标提示；作者 World of ClaudeCraft，FFmpeg 合成目录来源，许可 **P**。`1.312517 s`，44100 Hz，单声道，192000 bit/s，33270 B。SHA256 `188308687d6fb17a2fc446016a6b0791bcd77c4861850f2901e04d1f2265a1c3`；不采用，原型也不需要竞技失败式包装。
12. `public/audio/sfx/ui_click.mp3`：拟筛 UI 确认；作者 World of ClaudeCraft，FFmpeg 合成目录来源，许可 **P**。`0.093741 s`，44100 Hz，单声道，192000 bit/s，3804 B。SHA256 `dfbfbe59a48255e30f2c73d25f6917532b38d0f2b3053fc6e3c3c40ac78c58ee`；不采用。
13. `public/audio/sfx/ui_error.mp3`：拟筛非法调整/拒绝输入；作者 World of ClaudeCraft，FFmpeg 合成目录来源，许可 **P**。`0.796871 s`，44100 Hz，单声道，192000 bit/s，20732 B。SHA256 `2c47db40ec452b957e716ec6f5f08cb534b1576db9ccdbd5ccce2f85da6da545`；不采用。
14. `public/audio/sfx/ui_achievement.mp3`：拟筛达标/有效命中；作者 @jamiecypher，自录/声音设计成品，许可 **N**，是 `ui_*` 普通项目合成条目的明确例外。`2.687506 s`，44100 Hz，单声道，192000 bit/s，65871 B。SHA256 `cb5a7b55282072e6b1509eb573da1ad0cf052093fa3ac2b1a375ae28da86117a`；不采用。

此外，`temporal_clock.mp3` 明确只有 Levy Street 使用许可，排除而未做候选转用；`public/audio/voice/` 的角色台词没有复制、转写或用于新角色。本轮不是穷尽全部 WoC 音频的授权审计，后续若发现新来源需重新按文件核实。

## 4. 本项目七个原创合成原型音

1. 制作者：助手按本项目玩法独立编写合成与音色参数；工具为本机 Python `3.10.8` 的标准库 `math/random/wave/struct`，没有导入 WoC 音频、录音、乐谱、旋律、FFmpeg 生成参数或其他第三方采样。它们不是模型生成音频，无付费生成调用或隐藏模型 ID。
2. 制作源：[scripts/generate-prototype-audio.py](../../scripts/generate-prototype-audio.py)。源 SHA256：`cf7ef1e746592ff3d11f7261a308e99edc3ace2e6d1b5b7deb6a00b1bd382335`。每资产使用独立 seed；增删其他 cue 不改变当前 cue 的随机序列。已有同名但不同字节文件会拒绝覆盖。
3. 输出统一为 `assets/proto-013-ring-toss/audio/original/` 下的 48000 Hz、单声道、16-bit signed little-endian PCM WAV。下述事件名称是建议的集成语义，不冒称已有 Unity 事件类/音频映射已实现。
4. 使用边界：可先用于基础原型反馈；不替代 05 的全部二十六事件音、五曲 BGM 或三环境循环，不作正式音乐审美验收。它们的源与输出作为本项目制作成果保存，没有从 WoC 转入的媒体授权条件；工程整体的最终许可与发行审核另行登记。

逐资产记录：

1. `launch.wav` / `SFX-PROTOTYPE-LAUNCH`：seed `13001`，`0.240 s`，23084 B。滤波噪声与短下降滑音表现轻掠过；建议仅在一次投掷正式提交后播放，原型三圈暂共用。SHA256 `fb916fb1c41718ddfe51e10235f3b4b12f9fc9cd9537debf215484465a9cbdda`；实测峰值 `-6.021 dBFS`，RMS `-19.591 dBFS`。
2. `hit.wav` / `SFX-PROTOTYPE-HIT`：seed `13002`，`0.420 s`，40364 B。两组受控短泛音表示新奖品有效命中；不在预测线或侧撞上播放。SHA256 `1c32010244fd326010f5957c3a9531c57eaa6ff8ab0948ec1659c2694db3cd7c`；峰值 `-6.021 dBFS`，RMS `-20.144 dBFS`。
3. `bounce.wav` / `SFX-PROTOTYPE-BOUNCE`：seed `13003`，`0.220 s`，21164 B。短干接触与阻尼共振表示反弹板实际接触；按规则事件去重，不因贴面帧重复连发。SHA256 `bd63cb3a3bfbf9a74eef51ea7ef670b005f32be5232b458ec2394081b57711dc`；峰值 `-6.021 dBFS`，RMS `-23.663 dBFS`。
4. `cash.wav` / `SFX-PROTOTYPE-CASH`：seed `13004`，`0.480 s`，46124 B。三次轻金属式泛音表示兑现事务已提交；留场时不播放，阅览旧结算不重播事务音。SHA256 `ac3fa6f139f735c3a8dd1e294638ff5e2066df270b8544498c6b223e51e8289c`；峰值 `-6.021 dBFS`，RMS `-20.943 dBFS`。
5. `retain.wav` / `SFX-PROTOTYPE-RETAIN`：seed `13005`，`0.400 s`，38444 B。低于兑现音的温和双音表示旧物原槽留场已提交；不附现金数字或兑现音。SHA256 `eb5cb669566a268780dee3ba4e65b3022d0a50bf5dc91e9e6815d49c8cfe3a83`；峰值 `-6.021 dBFS`，RMS `-19.035 dBFS`。
6. `miss.wav` / `SFX-PROTOTYPE-MISS`：seed `13006`，`0.280 s`，26924 B。较低音量的短落点音表示本投已判失手；侧撞/落地/越界仍由文本区分，不能把此候选当作整局失败音或嘲讽包装。SHA256 `2608c71a2cb3aaf06edcde2a5d9e8417e94ceccf561d1f741cd6f5a63884f6b1`；峰值 `-12.396 dBFS`，RMS `-25.855 dBFS`。
7. `ui_confirm.wav` / `SFX-PROTOTYPE-UI_CONFIRM`：seed `13007`，`0.120 s`，11564 B。紧凑确认音，建议只用于被接收的 UI 操作，不替代投掷、命中或经济事件音。SHA256 `ae32245a531adaa3659ba96f90cb3fcd0ab22b4f6b33be4ea8eae034df9d0812`；峰值 `-9.898 dBFS`，RMS `-21.391 dBFS`。

## 5. 已验证与下一道验收

1. 已执行 `python scripts/generate-prototype-audio.py` 制作上述七个文件；再执行 `--check` 逐字节复现全部通过。原型 WAV 总计 `207668 B`；ffprobe 对七文件均返回 `pcm_s16le`、48000 Hz、单声道、16 bits、对应时长与字节数。每文件首尾 PCM 样本为 0，量化峰值不超过上述余量；这些是客观格式/幅度检查，不宣称听感优秀。
2. 下一步由开发代理在真实 Unity Player 验证：命中与失手原因、反弹重复接触、最后一圈处置、现金/留场互斥、暂停/回放/恢复与重复确认；经济事务只提交一次，音效不提前预测或重复表达新的奖励。
3. 实际混音需验证重叠播放、事件去重/限流、音量滑杆、静音与无声文字反馈；素材自身余量不等于重叠混音不会削波。BGM与环境声仍为空缺，原型可先采用安静背景，不借受限素材临时绕过登记。
4. 未完成：实际 Player 播放、设备试听、事件同步听审、正式音色/混音验收、完整 BGM/环境/二十六事件覆盖。未执行付费生成、未复制任何 WoC 媒体、未修改源仓、未写 Unity 工程文件。
