# 微信小游戏与Unity技术来源

2026-11-19重新读取官方公开正文。以下用于核对技术方案，不代表已经安装对应SDK、验证本机小版本或取得账号/平台权限。完整提取正文只保存在本地，公开归档摘要、短引和提取正文SHA256。

## 阅读方法与限制

通过无登录HTTP获取官方页面，解析正文；读取代码示例仅作为技术证据，未执行网页提供的代码。来源中的发布日期未知时保持null；页面编辑日期、执行日期与本轮观察日期分开，不把维护日期说成政策生效日。图片、全部子页面、后台条件不自动算已核验。

部分官方页面保留了不同年代引擎/SDK的说明，例如旧压缩纹理工具限制、已验证Unity小版本范围及存储默认配额。报告据此保留“先固定版本、再实际测试”的条件，不把文档示例推广成所有Unity/所有微信端的永久能力。

## 官方索引

| ID | 来源 | 本次用于核实 |
| --- | --- | --- |
| W01 | [Unity/团结引擎适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform.html) | Unity/Wasm路线、当前安装入口、AppID与后台快适配 |
| W02 | [推荐引擎版本](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/UnityVersion) | 年份支持不等于逐小版本验收，本机2022.3.62f3c1须实测 |
| W03 | [转换快速开始](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/Transform.html) | Loading场景、输出目录、首资源/CDN和预览路径 |
| W04 | [资源按需加载概述](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/ResourcesLoading.html) | AB/AA、远程异步与资源拆分 |
| W05 | [压缩纹理优化](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/CompressedTexture.html) | 原生压缩/微信分离工具、按设备格式、忽略劣化资源、SDK版本一致性 |
| W06 | [音频视频适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/AudioAndVideo.html) | UnityAudio、InnerAudio/WebAudio、长音轨/并发/循环/后台与格式兼容 |
| W07 | [屏幕适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/fixScreen.html) | WX窗口安全区、左上原点和pixelRatio转换 |
| W08 | [小游戏资源部署](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/DataCDN.html) | minigame/webgl、首资源、Assets/StreamingAssets、版本与HTTP压缩 |
| W09 | [小游戏资源缓存](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/FileCache.html) | Loader缓存/命名/LRU/默认空间和提升条件，旧版本资源保留 |
| W10 | [网络通信适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/UsingNetworking.html) | 异步请求、标准.NET网络限制和平台能力差异 |
| W11 | [输入法适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/InputAdaptation.html) | InputField与TMP/团结支持不同、自动适配默认关闭及必要API |
| W12 | [技术常见问题QA](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/DevelopmentQAList.html) | Unity不压缩、触摸桥接、字体、存档/Storage与用户手势要求 |
| W13 | [优化内存](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/OptimizationMemory.html) | 容器、Wasm、CPU/GPU、音频和文件系统副本的不同内存口径 |
| U01 | [Unity 2022.3 WebGL技术限制](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-technical-overview.html) | C#线程/动态代码/网络/音频的引擎基础限制；平台扩展另查 |
| U02 | [Unity 2022.3 WebGL内存](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-memory.html) | 堆增长、连续分配和桌面默认配置的移动调优边界 |

## 冲突与未完成的验证

- 官方推荐版本表并未列出本机准确的2022.3.62f3c1；仅记录其不在表中，不认定该版本不能用，也不擅自更换引擎。
- Input System 1.14.0、TMP输入框、WXTouchInputOverride与实际Loader的组合未真机验证。文档中的兼容补丁不能无测试批量加到所有EventSystem，避免重复输入与额外MainLoop开销。
- Unity的默认WebGL行为与微信SDK适配不完全相同。报告优先采用微信路径的具体存储/网络/音频说明，同时保留官方SDK实际实现复核。
- 20MB包内首资源条件、Storage10MB、默认文件缓存200MB/空间提升等均按当前文档路径理解，最终以锁定SDK、宿主/基础库和账号后台校验为准；不代替真包测量。
- 未登录后台核实快适配/广告/域名或iOS高性能模式权限，也未上传测试包；没有本品手机帧时、内存、加载、真实输入或广告结果。
- 当前随机包与商业审核边界沿[上一份发行计划](../booster-pack-heroes-miniapp-release/PLAN.md)处理；这份报告聚焦工程适配，不新增未经完整法规适用确认的法律结论。

当前代码依据另见[23组工程证据](evidence-index.json)。原游戏/派生源码和媒体的完整内容没有复制到这些官方来源记录中。
