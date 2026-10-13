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

032/051/027/121/126已建立各自独立Unity、规则、场景样件、保存、候选音乐及30/30、28/28、32/32、34/34、39/39测试；Windows单机包与初态相机证据已形成，正常窗口操作尚被系统安全权限提示阻挡，不当作完整验收通过。126在121候选单独备份后建立；任何外部条件仅延后依赖它的验证，不伪造成功。原名单/顺序归属见[决策记录](../docs/decisions/decision-log.md)。

2026-10-12 用户另行指定本机 Crunch Time! 安装，已建立独立 [Crunch Time! 逆向分析项目](re-crunch-time/README.md)，完成当前构建的全量托管代码、内容、Unity 对象与可解码资产静态分析。该项目不属于第一批原型排期；第三方构建/源码/媒体仅本机 local 保存，仓库保存原创报告、工具和索引。

2026-10-13 用户指定 Android 《代号：新生（测试版）》0.9.0，建立独立[研究项目](re-t1-codename-rebirth/README.md)。已完成 2051 条目、22162 地址/1017 文件清单、28 DEX/50 ELF、可读基础 Unity 和系统/内容地图。完整业务方法、数值与 981 自定义资源包尚未解析，权限范围与真实游玩分别待验；完整原包和索引仅本机保存。

2026-10-12 用户选定“妖怪夜市开张”包装，建立独立 [完整设计准备项目](proto-yokai-night-market/README.md)。其合同、数值、95卡/28委托、设施/客人/故事、全部替换索引、美术规范与150条生成/150条修订提示词自包含归档；本轮为设计准备，不改变第一批实现排期，未宣称已生成素材或完成新游戏。
