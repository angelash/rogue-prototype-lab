# 音频与发行制作

## 原创声音

24项音频的用途、长度、循环、声道、采样、响度/峰值、seed、源与运行路径在catalog.audio，CSV在data/csv/audio.csv。四曲音乐、一段环境、十九项事件/UI声音；不用原游戏91音轨，不采用未审核参考库。BGM以原创木敲、竹笛音色合成与拨弦编排为主，保留MIDI/参数/生产脚本和独立seed，不能把三秒合成音扩循环冒充整曲编排。

采样48kHz，制作WAV PCM24bit；音乐/环境立体声，单次SFX/UI单声道。运行OGG：音乐quality5，环境4，SFX/UI6；短UI可采用PCM WAV以免解压延迟，但变更要记录新路径/hash，不能有两套未追踪成品。真峰值<=-1dBFS；音乐目标-18LUFS、环境-22LUFS；短SFX按峰值/主观相对响度验收，不强套短片LUFS。

总线 Master→Music/Ambience/SFX/UI，UI随SFX音量。音乐常态0.65、SFX0.8、环境0.45、Master0.8。Reaction背景音乐短暂乘0.7（0.2秒淡变），结果停环境并播结果声；正常MUS.prepare，最后五轮MUS.final。MUS.title只标题/章节，MUS.result用于结算背景。多个工作完成0.3秒内合并一次SFX.done，日志仍逐项；voice最多16，各事件最多3，关键失败/结果声优先，音乐只一曲并作0.5秒交叉淡变。

事件分配：CardPlayed→SFX.card，WorkerPlaced→SFX.worker，WorkerAdvanced→SFX.work，TaskCompleted→SFX.done，BlockAttached→SFX.block，BlockRepaired→SFX.repair，InspectionPending→SFX.inspect，InspectionCancelled→SFX.counter，FacilityBought/Moved/Sold→SFX.facility，ModeChanged→SFX.mode，CoinsChanged正收入→SFX.coin，TurnEnded→SFX.end，胜/负→SFX.win/lose，平局→SFX.done，焦点/确认/拒绝/暂停/保存→对应HUD五项。BlockExpired采用SFX.repair低增益0.5；CardTransferred采用SFX.card，PowerChanged仅视觉不叠额外响声。

循环须跨接点无咔哒、重音/调性连续，去直流，不剪掉attack；全静音仍有可见反馈。每项先解码/峰值检测，再Player实际播放、混合并发与听感检查，不把ffprobe通过等同好听。无配音/TTS依赖。

## 性能与构建

catalog.budgets为开工预算，尚不是实测成绩。参考负载是12张手牌×2、5帮工×2、5停工×2、5设施×2、当前两客人、完整日志和检查覆盖。目标60FPS，P95 CPU8ms/GPU12ms，常态无托管每帧分配，进程内存<=600MB，初次加载<=6秒，运行纹理<=128MB，压缩音频<=24MB。最低设备目标Windows10x64/四核CPU/8GB RAM/UHD630级，未实测之前不得在商店作为验证完成的最低配置。

按当前屏幕按需加载合同插画，不能同时载入28张大图。SpriteAtlas按portrait/events/facility/ui分族，上限2048，打包间隔4px、边缘扩展2px，非当前章合同按需加载。Runtime Resource路径有稳定资产ID索引，不能用中文文件名拼Resources.Load。UI对象复用，日志最多展示最近100条，全部本局事件仍在可保存记录。

Development包验证后再Release，Windows x64 Mono，排除Editor/test assemblies、源码私有分析、调试热键与凭据。整包清单、日志、测试XML、内容/源/资产摘要与Unity完整版本一起保存；包只执行当前源码对应结果。路径含中文/空格、非管理员、离线启动、干净机器与焦点/窗口均要检查。

## 发行材料定义

采用Steam目标但不假定AppID/后台/签署已具备。本版核心运行不接Steam SDK，平台成就/Cloud/联网排行不采用。真实身份、付款、税务、合同及上传发布属于依赖平台权限的阶段；开工设计不需要这些外部条件。

商店标题：妖怪夜市开张。短介绍：“鬼门打开前，把一车旧物摆成妖怪夜市。安排帮工、修补麻烦，让每件旧物找到新的用途，抢先点亮你的开张牌。”长介绍采用三个真实玩法点：三次行动安排五项筹备；十二件双用途旧物和相邻供电；五类客人的公开需求与禁忌。标注PC单机、键鼠、中文、无配音；上线时按实际实现删改，不许用概念图充实机截图。

主视觉母版KEY.cover为1920×1080、顶部留标题，原生排字。Steam各展示位尺寸与平台当前规则在发行材料生产时从实际Steamworks文档核对，不把此母版尺寸冒称全部商店胶囊规格。预定六张真实截图：初局委托、帮工推进、停工与修补、供电两邻格、变卖后服务丢失、轮末开张；演示视频45–60秒按这六个可操作状态录制，保留版本/输入/掉帧证据。

所有生成图片、原创声音、审核字体与软件依赖随包保留许可/制作来源。审核原件不可用则换合法来源，不能因为参考代码MIT而复制媒体。支持说明包括按键、存档位置/备份恢复、验证游戏文件、离线与错误报告；用户报告不默认上传个人数据。
