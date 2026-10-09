# 051 回收保洁队

第一批项目，当前实施v0.2，2026-10-10。原需求/设计v0.1保留2026-10-09；用户授权逐个项目开发，本轮助手采用合同首次纵切范围，当前只制作051，单项备份后再下一项目。

核心问题：桶容量、材料用途和工具是否改变清洁路线及顺序。

- [需求文档](../../docs/design/first-batch/proto-051-recycling-cleaners/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-051-recycling-cleaners/design.md)
- [开发要求与规范](DEVELOPMENT.md)：制作准备、独立玩法约束、首次纵切验收、当前缺口与接续步骤。
- [实施方案](../../docs/development/proto-051/implementation-plan.md)：容量/布局/滤芯/刷头对照、初值与黄金动作、完整保存和制作顺序。
- [操作与界面合同](../../docs/development/proto-051/input-and-ui.md)
- [Unity工程README](game/RecyclingCleaners/README.md)
- [开发记录](../../docs/development/proto-051/2026-10-10-slice-01.md)：本项目实际准备/测试/构建/画面/原生操作和提交备份。
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号051容量与再利用版本。2026-10-09 v0.1归档时尚无实现/已选引擎；本轮以Unity2022.3.62f3c1、Windows x64普通Mono Strict/PC单机实施，三维斜俯视房间表现原二维单房间的离散网格规则。题材建议为“清晨回收小队：把废料留给下一次清洁”，不恢复元素战斗，不扩成长商店。

遵循 [共同规范](../../DEVELOPMENT_STANDARD.md)。首次可玩候选Core/表现/完整保存已落盘，Unity2022.3.62f3c1 Prepare成功，EditMode28/28通过（22规则+4实际RoundSnapshot故障存储+2Runtime帧边界）；最终普通Mono Strict构建成功、0错1警，见 [测试报告](../../docs/development/proto-051/evidence/test-results.xml) / [构建清单](../../docs/development/proto-051/evidence/build-manifest.json)。Standard shader剥离、标签重叠和恢复/重试同帧移动三项缺陷已闭环；[最终相机图](../../docs/development/proto-051/evidence/scene.png) 污物/残量可读，隔离诊断无Exception/fontTrue且未发玩法命令。九份资源同字节已核对，实际听感未验。既有系统安全权限modal目前阻挡正常窗口原生路径，见 [欠证记录](../../docs/development/proto-051/evidence/native-qa.json)；功能美术和玩法体验尚未验收，不称完整纵切/Steam完成，不借013/032成绩。已完成本项目独立提交推送。


## 2026-10-10 工程身份与备份复核

2026-10-10工程身份复核：productGUID改为独立UUID5 `4d31d65c78865e8e998124ac5584b97e`；最新普通Mono包86,881,541字节、0错误/1警告，UTC `2026-10-09T18:43:27.2086612Z`，日志 `.local/unity/RecyclingCleaners-20261010-024313-968-Build/unity.log`。最新相机初态已实际目检，PNG SHA `0906dbe62b56264b323d488c08b24a846b217ce76cad3481f2becf2702b60ee8`；完整原生/HUD/动态/听感仍未验。规则/API/初值未改，沿用本项目已通过的28项真实XML，未重复计次；032仅两文本换行归一，不改变游戏语义。详见[本批复核](../../docs/development/first-batch-backup-audit.md)。
