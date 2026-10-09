# 027 贪吃蛇孵化场

用户明确追加到第一批，当前实施v0.2，2026-10-10。原需求/设计v0.1保留2026-10-09；用户后续授权逐个项目开发，本轮助手采用合同首次纵切范围，051已独立备份，当前只推进027。

核心问题：身体能力、空间占用和分身成本是否促使玩家主动保持短蛇或回收分身。

- [需求文档](../../docs/design/first-batch/proto-027-snake-hatchery/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-027-snake-hatchery/design.md)
- [开发要求与规范](DEVELOPMENT.md)：制作准备、独立玩法约束、首次纵切验收、当前缺口与接续步骤。
- [实施方案](../../docs/development/proto-027/implementation-plan.md)：主蛇/身体能力、公开切尾/分身/回收、物料守恒、完整保存与验收矩阵。
- [操作与界面合同](../../docs/development/proto-027/input-and-ui.md)
- [Unity工程README](game/SnakeHatchery/README.md)
- [开发记录](../../docs/development/proto-027/2026-10-10-slice-01.md)：本项目实际制作、验证、欠证与提交备份。
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号027。原工作簿的“后续候选”标记作为历史保留，本轮批次登记以新汇总为准。2026-10-09 v0.1归档时没有实现/实测/已选引擎；当前Unity2022.3.62f3c1脚手架已建立，按Windows x64普通Mono Strict/PC单机制作，Steam仍为后续目标。

遵循 [仓库共同标准](../../DEVELOPMENT_STANDARD.md)。本轮助手采用一图/三类节/两简单行为/至多两分身和三维生态孵化盘；离散规则保持独立，原二维方案不改写。实际Core/Runtime/场景/存档、测试与构建按开发记录同步，本项目32/32 Unity测试、Windows构建及初态目检已通过，原生与听感仍待验。既有系统安全权限modal仅阻正常桌面原生阶段；独立候选制作继续，原生/听感欠证保留，不称完整纵切或Steam完成，不借013/032/051成绩。


## 2026-10-10 工程身份与备份复核

2026-10-10工程身份复核：productGUID改为独立UUID5 `bef4ab206ac850c39b9c186ac1924ec1`；最新普通Mono包86,888,473字节、0错误/1警告，UTC `2026-10-09T18:44:53.4373616Z`，日志 `.local/unity/SnakeHatchery-20261010-024439-982-Build/unity.log`。最新相机初态已实际目检，PNG SHA `dc6763560ae004eb5f2741d58103fa5c92e860bc445497eafcb4968e60d4a979`；完整原生/HUD/动态/听感仍未验。规则/API/初值未改，沿用本项目已通过的32项真实XML，未重复计次；032仅两文本换行归一，不改变游戏语义。详见[本批复核](../../docs/development/first-batch-backup-audit.md)。
