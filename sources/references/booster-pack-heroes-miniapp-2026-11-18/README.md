# Booster Pack Heroes 小游戏计划调研来源

观察日期：2026-11-18；市场：中国大陆；平台：微信小游戏、抖音小游戏。计划入口：[计划书](../../../docs/design/booster-pack-heroes-miniapp-release/PLAN.md)。证据目录只归档短引、研究摘要、URL、阅读级别和提取正文 SHA256；完整页面提取文本仅供本地研究，不在公开仓库复制长篇文档。

## 事实与决策

用户在本轮明确选择微信先行、抖音随后，并确认已取得商业改编授权、范围后续补充。计划按已获授权的移植路线，不推断合同地域、期限、转授权、素材或分成内容。

本地读取项目合同 v1.2、README、系统/数值分析、顺序还原进度与证据。其记载的 27 批、47 个源文件、162 项托管检查仅属特定还原快照，完整可玩、现场整合、真实保存、输入/听感与移动端未由本轮重验。原作公司字符串、程序集或安装位置不等同于法律权利人或商业授权文件。

归档期间另只读核对新增的倒序还原报告；它仍在更新，27批/47文件不表示实时全局总量。本计划不保存动态队列副本或干扰其它还原任务，执行时重新冻结并审计。

本轮只读检查 Unity 2022.3.62f3c1 的 WebGLSupport 目录存在；不声明已编译 WebGL 或跑过小游戏。原作可搜索系统和数值用于形成假设，不作为真实玩家数据。

## 证据分层

- `full`：Browser Runtime 文章提取覆盖 complete，已读当前页面正文；不等于已遍历其全部子链接或后台。
- `text`：无登录公开 HTTP 页面中提取到并阅读相关正文；图片、折叠组件和子链接没有自动升级为已读。
- `limited_images / limited_text`：正文较短或关键说明在图片/链接，明确限制结论范围。
- `navigation_only`：只有目录导航，不用于具体 SDK/资质条款结论。
- `search_snippet_only`：仅定位原文，不把摘要当完整法规或“近期新规”。

同一机构的开发与介绍页面只是交叉核对，不虚增为多家独立证据。`publishedAt`、`lastEditedAt`、`effectiveAt`、`observedAt` 分开记录，未知为 null。页面修改日志不等于法律颁布日期。

## 官方来源索引

| ID | 官方页面 | 阅读范围与用途 |
| --- | --- | --- |
| S01 | [Unity/团结引擎适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform.html) | full；2026-07-04 编辑；引擎、安装入口、快适配能力 |
| S02 | [转换快速开始](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/Transform.html) | full；2026-07-04 编辑；Loading/CDN/AppID/首资源和预览 |
| S03 | [抖音 Unity 小游戏接入指南](https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/develop/guide/game-engine/rd-to-SCgame/unity-game-access/install-connect/sc_access_guide) | full；Android 原生与双端 WebGL、独立 SDK、引擎选择、视频分发 |
| S04 | [微信广告开发指引](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/ad.html) | full；2026-04-09 编辑；三条开通路径，非已核实账号资格 |
| S05 | [微信广告开通](https://developers.weixin.qq.com/minigame/introduction/commercialization/ad/) | full；2026-04-01 编辑；对照三条路径、资格刷新 |
| S06 | [wx.createRewardedVideoAd](https://developers.weixin.qq.com/minigame/dev/api/ad/wx.createRewardedVideoAd.html) | full；创建/兼容/单例设置；未读完成回调子页 |
| S07 | [小游戏备案](https://developers.weixin.qq.com/minigame/introduction/guide/nrjs.html) | text；IAA前置审批、已有版号分路、单机联机、名称/主体一致性 |
| S08 | [商业激励概要](https://developers.weixin.qq.com/minigame/introduction/commercialization/) | text；变更日志2026-10-23；现金/广告金与结算 |
| S09 | [小游戏运营规范](https://developers.weixin.qq.com/minigame/product/) | text；随机投入条款、授权主体、内容/分享/隐私/未成年人/停运 |
| S10 | [小程序备案](https://developers.weixin.qq.com/minigame/introduction/guide/xcxba.html) | limited_images；可见IAA/IAP分路和参考时效，流程图未目检 |
| S11 | [中宣部实名认证](https://developers.weixin.qq.com/minigame/introduction/guide/zxbsm.html) | text；微信无虚拟支付时平台协助接入，跨渠道另验 |
| S12 | [用户隐私保护](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/privacy.html) | text；标准/非标准API披露、处理前授权与拒绝兼容 |
| S13 | [抖音小游戏自审自查事项](https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/operation1/norms/specifications) | text；辅助清单，非本作完整准入结论 |
| S14 | [抖音 Unity C# SDK 入口](https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/develop/guide/game-engine/rd-to-SCgame/unity-game-access/sc_stark_sdk) | navigation_only；接口正文未读；广告完成/服务端验证能力待查 |
| S15 | [微信广告变现激励政策](https://developers.weixin.qq.com/minigame/introduction/commercialization/guide/ad-monetization.html) | text；2026-08-20执行说明、2026-10-23变更日志；渠道/时长/30或180天广告金 |
| S16 | [抖音注册入驻](https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/register) | limited_text；主体认证和对公验证摘要，详情未读 |
| S17 | [微信备案常见问答](https://developers.weixin.qq.com/minigame/product/record/record_faq.html) | text；短信、信息一致、变更与注销，实际审批为准 |
| S18 | [常见拒绝情形](https://developers.weixin.qq.com/minigame/product/reject.html) | text；辅助审核清单，小游戏特别条款优先 |

以上为官方平台规则/指引或可选激励；必须结合具体主体、游戏、渠道和当时后台条件适用。政策适用确认和正式法务复核仍是任务，不将公开页面等同于本游戏已获批。

S19 [防止未成年人沉迷网络游戏通知](https://www.nppa.gov.cn/xxfb/tzgs/202108/t20210830_666285.html)与 S20 [工信部APP备案通知](https://www.gov.cn/zhengce/zhengceku/202308/content_6897341.htm)本轮仅通过搜索定位，未读完整原文，不依据这些摘要扩张强制义务。微信实际接入工作主要依据已读平台 S07/S10/S11/S17。过去30天观察窗口为2026-10-20至2026-11-18；S08/S15有窗口内更新，其它是基线，不伪装为近期事件。

## 本轮工具与覆盖缺口

1. Browser Runtime 读取文档后访问公开页面；一次抖音资质页导航返回 `BROWSER_ACTION_OUTCOME_UNKNOWN`，按 `stop-and-report` 停止本轮后续导航。继续读取此前已开页面；不通过别的入口重试该页面。其它独立公开页面使用无账号 HTTP 正文读取。
2. hatch-cli 为0.7.2，服务端要求0.8.x；`auth status/doctor` 返回 `CLI_UPGRADE_REQUIRED`；`upgrade` 返回 `UPGRADE_REQUIRES_INSTALLED_BINARY`。没有安装新工具，也没有获得孵化库结果。`doctor` 顶层成功不掩盖认证/版本子项失败。
3. 独立HTTP搜索返回的结果与查询不符，全部弃用；[public-searches.json](public-searches.json)仅保留无效检索记录，不作事实证据。
4. 部分路径只返回导航、空正文或404。没有把HTTP200作为完整读取，也没有把旧GitHub入口停用认定为整个Unity适配方案不可用；现行官方安装入口见S01。
5. 未核实任何登录后台的AppID、流量主资格、实际分成合同、广告金、创意/优选/CPS、广告主权限、抖音锚点/达人计划、用户数据、费用或审核进度。
6. 未取得真实广告收益、买量报价、竞争排名、竞品后台数据、真人留存或移动性能。本计划场景及KPI是设计假设，无市场规模和收益保证。

待补读：监管机关原文及授权境外作品适用路径；抖音资质规范完整正文与当前SDK广告回调/鉴真能力；微信奖励回调子页；具体账户规则、素材规格和官方投放归因文档。未补齐前对应动作保持检查点，不影响此计划的归档。

## 归档文件

[official-excerpts.json](official-excerpts.json)、[additional-public-pages.json](additional-public-pages.json)、[wechat-requirements.json](wechat-requirements.json)、[channel-requirements.json](channel-requirements.json)保存短引与摘要。完整21项来源记录及本地记录路径在 [plan-data.json](../../../docs/design/booster-pack-heroes-miniapp-release/plan-data.json)；提取正文哈希用于识别研究版本，不声称以后重新抓到同一网页必然逐字节一致。

不包含授权合同原件、个人身份/联系方式、账号密钥、原游戏资产或派生源码。法律/平台规则的适用、签署和提交由具备资格主体核实；本材料不是法律意见。
