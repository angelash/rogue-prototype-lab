# 126 收割机自己铺路

当前实施入口：v0.2，2026-10-10。用户已授权首批逐个开发，助手采用固定试验田/三轮连续订单的首次可玩候选范围。Prepare、39/39正式Unity测试（35规则+4存储）、普通Windows Mono构建及最终初态相机目检已完成，见[实际记录](../../docs/development/proto-126/2026-10-10-slice-01.md)；原生/动态/完整HUD/听感及HRV-H01–03仍待验。

核心问题：订单、工具和秸秆用途是否改变固定行动预算下的路线。

- [需求文档](../../docs/design/first-batch/proto-126-harvester-paths/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-126-harvester-paths/design.md)
- [开发要求与规范](DEVELOPMENT.md)：制作准备、独立玩法约束、首次纵切验收、当前缺口与接续步骤。
- [本轮实施合同](../../docs/development/proto-126/implementation-plan.md)：三轮地图/货物/燃油/行动账、单一参数源、基础黄金与保存。
- [当前操作与界面](../../docs/development/proto-126/input-and-ui.md)：已核对Runtime绑定，真实窗口仍待验。
- [实际开发记录](../../docs/development/proto-126/2026-10-10-slice-01.md)：正式XML、最终包清单/相机及欠据，单项备份由协调者登记。
- [Unity工程说明](game/HarvesterPaths/README.md)
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号126，原工作簿“后续候选”标签与批次解释保留。需求/设计v0.1及原始文件不改，当前制作以DEVELOPMENT和v0.2合同为准；顺序、具体地图/参数和三维样件是助手实施选择。

## 历史归档与当前边界

2026-10-09的v0.1当时只有需求/设计，未实现/实测/选定引擎；按重点观察方向并入首批，不是用户逐项指定排期。此前“仅文档、不启动开发”保留为历史，由后续逐项授权接续。

沿[仓库共同标准](../../DEVELOPMENT_STANDARD.md)采用已核实Unity2022.3.62f3c1、Windows PC单机与Steam目标准备，原二维离散方案保留。当前三维田地只是表现，Core拥有收割/铺路/共享容量/固定预算真值；三轮延续地图/货/油/钱，warehouseStraw只入库不取回。真实输入/听感、HRV-H01–03和完整发行待完成，其它项目实绩不归入126。
