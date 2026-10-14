# 妖怪夜市开张 Steam 发行准备

2026-10-14。当前是发行准备包，尚不具备送审条件。游戏内资源与 v2.8 Player 保持原样。本目录保存可复用的来源清单、分工、商店草稿、独立宣发资产和 SteamPipe 模板；不是已完成 Steam 审核或已获商业授权的证明。

## 先处理商业来源

本机核对：当前工程30份恢复业务脚本、91个原作音频，91个 WAV 与原作导出哈希一致；场景、18个根 prefab、78个 Sprite/功能 alpha 和数据组织也来自恢复链。另有13份 Noto 字体与12份历史 TTF 输入，不能用 Noto 的 OFL 代替其它字体许可。具体分类见 [rights-register.json](rights-register.json)。Steam 要求发布者具备足够权利；换图或 AI 披露不会替代授权。[官方入驻规则](https://partner.steamgames.com/doc/gettingstarted/onboarding)

两条路线待确认：已有明确商业许可，则核对覆盖对象/范围；没有许可，则保留研究候选，另做独立发行实现、UI/几何/数据表达和原创或明确许可音频。只重写代码或把原文件改名都不能算来源清理完成。当前工具不会将研究包直接生成为上传配置。

## 从准备到上线

1. **来源与产品范围**：确认上述路线，冻结可兑现内容。我负责制作和验证，授权文书由实际权利主体确认。
2. **Steamworks 入驻**：你本人登录、验证身份、签署协议并填写税务/银行信息。可以以合适的个人或企业主体入驻；银行户名应与登记主体一致。敏感资料直接填官方后台，不发进聊天或 Git。[入驻说明](https://partner.steamgames.com/doc/gettingstarted/onboarding)
3. **独立应用**：你支付并激活应用名额，每应用100美元或等值，可能另有当地税；Steam钱包不能支付。达到1000美元 Adjusted Gross Revenue 后可在后续结算回收该费用，费用不是无条件退款。我再配置真实 AppID、Windows depot、启动项、测试分支与包。[费用说明](https://partner.steamgames.com/doc/gettingstarted/appfee)
4. **完成发行包**：我处理程序、素材、音频、完整原生流程、听感、最低配置/性能、干净安装及许可。当前748张渲染、40规则/15流程和14项代表原生检查是 v2.8 证据，不等于完整发行验收。
5. **准备商店**：我制作中英文介绍、胶囊、库图、图标、真实截图、预告片和问卷草稿。当前已备两份生成主视觉和9张原生排字候选，逐项尺寸已核对；真实截图/录像等可发行包完成再录。95牌/28委托来自当前研究版，若独立版变更，草稿必须同步。
6. **平台集成与实际下载验证**：Steamworks API 集成并非上架的统一前提；上传使用其工具。首发不为凑清单强加成就、Cloud或手柄。我完成 SteamPipe/Preview、测试分支、真实 Steam 安装/离线/更新与回滚；仅声明通过测试的功能。[SDK说明](https://partner.steamgames.com/doc/sdk)、[上传说明](https://partner.steamgames.com/doc/sdk/uploading)
7. **分开审核与公开**：先完成商店清单并送审，再处理构建审核；审核通常各3–5工作日，按官方建议至少预留7工作日处理修改。通过后另行公开 Coming Soon，不会因为审核通过就自动开售。我处理资料与反馈，公开动作按具体版本和你批准的范围执行。[审核流程](https://partner.steamgames.com/doc/store/review_process)、[发布流程](https://partner.steamgames.com/doc/store/releasing)
8. **时间与最后决定**：本次读取官方英文入驻页为缴费后21天，Coming Soon 至少公开两周；最终依应用后台实际截止日期。来源、质量、审核和价格也须就绪，不能只按日历倒数。你批准具体售价、地区、日期和最终包，我准备发布/回滚检查；身份、签署和最终商业决定由你作出。
9. **上线维护**：我继续处理问题复现、修复、回归、补丁、备份、公告和支持回复草稿。对外发送消息、重大商业承诺与额外费用按实际授权处理。

全球 Steam 商店与 Steam China/蒸汽平台是不同流程；Steam China 的政府批准/ISBN 等要求应另行处理，不把全球发行准备当成该资格。[Steam China说明](https://partner.steamgames.com/doc/store/china)

## 你当前只需补充的事实

- 是否已有 Crunch Time 商业改编/代码/音频等许可，以及实际范围；若无，确认独立原创路线。
- Steamworks 当前阶段和是否已有本游戏 AppID。AppID可提供；密码、验证码、银行与税号只在官方后台操作。
- 之后我会给具体名称、售价/地区/日期建议和候选包，你再作商业决定；不要求你制作图片、写代码、写文案或排查构建。

## 已准备的文件

- [授权咨询草稿](rights/license-request.zh-CN.txt) / [英文版](rights/license-request.en.txt)：待核实权利方后使用，未对外发送。
- [补丁与支持流程](support/operations.md) / [问题模板](support/bug-report.txt)。
- [分工与验收计划](plan.json)、[产品配置](product.json)、[来源与权利登记](rights-register.json)、[官方依据](official-sources.json)。
- [中英文商店草稿](store/store-copy.json)、[AI与内容问卷草稿](store/content-survey-draft.json)。当前 AI 用于预生成美术、部分文案和本地化；没有运行中生成式 AI。成熟内容和评级等最终全包检查后填写，不猜评级。[官方内容问卷](https://partner.steamgames.com/doc/gettingstarted/contentsurvey)
- [宣发规格与镜头清单](media/specs.json)、source/ 中的原图/提示、generated/ 中的9张候选，以及 [生成与导出清单](media/production-manifest.json)。标题只用本机 Noto 字形，Hero 无字，Logo透明；此处是宣传插画，不能作玩法截图。
- [App VDF模板](steam/app_build.vdf.template)、[Depot VDF模板](steam/depot_windows.vdf.template)：仅 Preview，不含密码、不设 SetLive，未上传。
- [发行验收用例](test-cases/release-acceptance.json)。结果与私有凭证放在忽略目录 local/steam-release/；源码、资产、模板和用例纳入版本管理。

## 可重复执行

在仓库根执行：

```powershell
python prototypes/proto-yokai-night-market/scripts/test_steam_release_gates.py
python prototypes/proto-yokai-night-market/scripts/verify_steam_media.py
python prototypes/proto-yokai-night-market/scripts/audit_steam_release.py --enforce
```

当前 `--enforce` 应退出2并列出未满足项，这是如实阻止误上传。`prepare_steam_preview.py` 只在权利、独立AppID、候选包全部文件与对应质量证据闭合后输出 Preview VDF，始终不登录/上传/发布。检测器验证证据完整性，不替代法律判断或真实后台状态。

新宣发图片用内置 imagegen 生成，原提示保存 source/；技术导出为单一等比采样与原生文字，脚本 `scripts/export_steam_media.ps1`。宽图实际2172×724，库Hero按规范技术放大到3840×1240，不冒称原生4K细节。缩略候选已人工查看，正式品牌定稿及整体商业权利检查仍待完成。
