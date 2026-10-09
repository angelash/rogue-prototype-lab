# 032 回转寿司工坊

第一批项目，当前实施版本v0.2，2026-10-10。需求/设计v0.1保留2026-10-09原稿；用户已授权逐个项目开发，本轮助手采用各合同首次纵切范围推进，当前只实施032，完成并提交后再下一项。

当前为首次可玩候选实现：Unity2022.3.62f3c1 Prepare成功，30/30规则/存储测试通过，已完成普通Mono Strict构建0错误/0警告；最终包与初态相机图已归档。Player启动被Windows防火墙安全权限弹窗阻挡，已请求用户手动取消但仍未关闭，原生操作/实际听感待补，尚未完成完整纵切QA验收。

核心问题：相同加工站面对不同订单，是否值得改变配置与交付时机。

- [需求文档](../../docs/design/first-batch/proto-032-sushi-workshop/requirements.md)
- [设计文档](../../docs/design/first-batch/proto-032-sushi-workshop/design.md)
- [开发要求与规范](DEVELOPMENT.md)：制作准备、独立玩法约束、首次纵切验收、当前缺口与接续步骤。
- [本轮实施方案](../../docs/development/proto-032/implementation-plan.md)：两订单、初期参数、完整规则和安全保存合同。
- [操作与界面设计](../../docs/development/proto-032/input-and-ui.md)
- [Unity工程README](game/SushiWorkshop/README.md)：Unity版本、Prepare/Test/Build和控制方式。
- [本轮开发记录](../../docs/development/proto-032/2026-10-10-slice-01.md)：编译/测试/构建/画面/真实输入与提交备份的实际证据。
- [第一批项目汇总](../../docs/design/first-batch/README.md)
- [来源与筛选登记](../../sources/chatgpt/first-batch-selection.md)

沿用原方案编号032。2026-10-09需求/设计v0.1归档时尚无实现、实测或已选引擎；该历史状态不代表本轮。当前采用Unity2022.3.62f3c1、Windows x64 Mono/PC单机，原二维单环离散规则由首组三维斜俯视厨房呈现，固定六槽/六料理状态/四工站，两类订单独立开轮对照。

本轮遵循 [仓库共同标准](../../DEVELOPMENT_STANDARD.md)，制作有限可玩候选和单轮安全保存/恢复；Steam仍是后续目标准备，不是已接入/发行。初值与三维镜头为助手实施建议，规则通过不等于策略/乐趣或原生操作通过。随机成长、多环/员工/真实切菜、全量音画剧情与完整Steam发行不属于本轮，具体结果见开发记录。

H可看最近十条关键事件，打开暂停、H/Esc关闭；保存包含完整领域状态、摘要校验与有效备份保护，相关存储定向回归通过。九份运行资源副本/两份声明哈希已核对，音画来源有独立登记；真实面板、键鼠与听感仍待补。
