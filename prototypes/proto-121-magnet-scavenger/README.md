# 121 磁铁拾荒者

当前实施入口：v0.2，2026-10-10。用户已授权首批逐个开发；本轮助手采用有限夜班废料箱、两订单与首次可玩纵切范围。独立工程、规则/Runtime/保存和原创场景已完成；34/34 Unity测试、普通Mono包0错0警告及初态目检已归档，原生/听感未过。

核心问题：形状与负载是否让玩家主动拆短，并改变货物与工具的选择。

- [需求文档](../../docs/design/first-batch/proto-121-magnet-scavenger/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-121-magnet-scavenger/design.md)
- [开发要求与规范](DEVELOPMENT.md)：制作准备、独立玩法约束、首次纵切验收、当前缺口与接续步骤。
- [本轮实施合同](../../docs/development/proto-121/implementation-plan.md)：唯一参数源、五ID归属、几何/负载、两订单黄金、完整保存与验收。
- [操作与界面](../../docs/development/proto-121/input-and-ui.md)
- [实际开发记录](../../docs/development/proto-121/2026-10-10-slice-01.md)：协调者补真实验证和Git备份。
- [Unity工程说明](game/MagnetScavenger/README.md)
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号121，原工作簿“后续候选”标签与批次解释保留。需求/设计v0.1及原始资料不改，当前制作以DEVELOPMENT和v0.2合同为准；参数、顺序与三维桌景是助手选择。

## 历史归档与当前边界

2026-10-09文档v0.1当时仅需求/设计准备，未实现、实测或选定引擎；按重点观察方向并入首批，不是用户逐项指定排期。此前“未启动开发”状态保留为历史，由后续逐项开发授权接续。

2026-10-09新增规范沿[仓库共同标准](../../DEVELOPMENT_STANDARD.md)默认使用已核实Unity2022.3.62f3c1、Windows PC单机与Steam目标准备。当前原创三维桌景表现离散网格，保留原二维方案；首件、接点/占格、负载与订单由Core判定，不是物理磁场。真实QA、听感、MAG-H01–03与完整发行仍待完成，其它项目成绩不归入121。
