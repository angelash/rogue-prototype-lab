# 游戏原型

每个原型在此目录下拥有独立子目录，编号沿用原方案库，名称使用稳定英文短名。项目 README 链接权威需求与设计；设计正文按项目独立保存在 docs/design/first-batch/。

制作准备、制作要求和当前落地分别归档在各项目根 `DEVELOPMENT.md`；所有当前与未来项目默认继承 [共同开发规范](../DEVELOPMENT_STANDARD.md)，新项目按 [模板](../DEVELOPMENT_TEMPLATE.md) 建立合同。开始或继续项目先读共同规范与专属合同，实际状态改变后同步更新，避免将其它项目的成果当成本项目验收。

2026-10-09 已按本轮指令整理 [第一批项目](../docs/design/first-batch/README.md)，共 6 项：

- [032 回转寿司工坊](proto-032-sushi-workshop/README.md)
- [013 套圈改造摊](proto-013-ring-toss/README.md)
- [051 回收保洁队](proto-051-recycling-cleaners/README.md)
- [027 贪吃蛇孵化场](proto-027-snake-hatchery/README.md)
- [121 磁铁拾荒者](proto-121-magnet-scavenger/README.md)
- [126 收割机自己铺路](proto-126-harvester-paths/README.md)

最初第一批归档完成目录、需求和设计文档。013保留既有真实三维P0、48项测试与Windows包及三维原生输入待补的历史边界。用户随后要求其余五项逐个开发，每项单独提交；助手按032 → 051 → 027 → 121 → 126推进各自首次可玩候选，实际状态和未执行验收见[逐项进度](../docs/development/first-batch-progress.md)。

032已建立独立Unity、规则、厨房样件、保存、候选音乐及30/30测试；Windows单机包与相机证据已形成，正常窗口操作尚被系统安全权限提示阻挡，不当作完整验收通过。其它四项待本项候选备份后逐个启动；任何外部条件仅延后依赖它的验证，不伪造成功。原名单/顺序归属见[决策记录](../docs/decisions/decision-log.md)。
