# Booster Pack Heroes 手机触控与微信包技术报告

2026-11-19 v1.0。以当前可玩还原工程和iteration-14 Windows候选为参考，重新审阅手机点击/拖动、界面、安全区、资产、WebGL编译、首包、AB/CDN、存档及平台生命周期。

- **[完整报告](REPORT.md)**
- **[离线交互预览](index.html)**：阅读完整报告，筛选25项整改、查看源文件证据和15项官方技术来源，可打印。
- [整改清单](findings.json)：8项P0、15项P1、2项P2；为建议，尚未实现。
- [工程证据索引](evidence-index.json)：23组依据，记录当前本地路径、行号与SHA256，源文件/原媒体不随报告上传。
- [资产与设置静态盘点](evidence/project-inventory.json)：Assets源文件统计、近似GUID闭包、场景组件/导入设置及三个音频探针。
- [官方来源说明](sources.md)与[结构化来源](evidence/official-sources.json)：2026-11-19重新读取，区分公开正文、页面日期与实际平台能力。
- [报告验证记录](evidence/report-verification.json)：验证文档、数据和离线预览，不能代替游戏/微信真机测试。

已有触摸绑定、点击预览、双指缩放和安全区基础可以保留。优先处理Steam版能力判断、战斗持续指针与UI手势互斥、Steam/WebGL编译边界、主场景/Resources首包依赖、微信持久化与广告暂停；随后用代表样件扩展资产优化。

本轮只读分析，没有改变游戏代码/资产、切目标、安装SDK、构建或运行微信。已有PC验收按原版本范围引用；手机操作、最终包体/内存、广告与账号权限均待实测。源大小和RGBA32估算不是实际包体/运行内存。

当前方向沿用[整体发行计划](../booster-pack-heroes-miniapp-release/README.md)：用户已确认微信先行、抖音随后、商业改编授权已取得而范围后补。技术报告采用更新后的工程状态，不覆写11月18日历史计划。

复跑只读盘点（输出必须在被审工程之外）：

```powershell
python -X utf8 docs/design/booster-pack-heroes-mobile-wechat-technical/audit_project.py --project prototypes/re-booster-pack-heroes/local/UnityProject --output .local/bph-mobile-audit-refresh
```

盘点输出的完整明细/代码命中仅供本地研究，后续更新应审阅和脱敏后归档聚合结论。改正文或JSON后重建预览：

```powershell
python -X utf8 docs/design/booster-pack-heroes-mobile-wechat-technical/render_report.py
```
