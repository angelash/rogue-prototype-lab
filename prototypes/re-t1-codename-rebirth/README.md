# 代号：新生（测试版）逆向研究

用户指定 Android APK，分析日期 2026-10-13，包名 com.gnml.t1yfnbcs，版本 0.9.0。用户已确认分析授权。

- [项目合同](DEVELOPMENT.md) · [来源](sources/target-register.md)
- [总报告](reports/analysis.md) · [架构](reports/architecture-v0.4.md) · [逐系统分析](reports/systems.md)
- [代码](reports/code-map.md) · [内容与资产](reports/content-and-assets.md) · [恢复边界](reports/recovery-scope.md)
- [工具/复跑](scripts/README.md) · [深层校验：25/25](evidence/deep-verification.json) · [检索 QA：15/15](evidence/deep-explorer-qa.json)
- [完整本地备份](evidence/deep-backup.json) · [原创资料远程备份](evidence/deep-publication.json)
- [本地检索服务器](scripts/serve_catalog.py) · [原离线地址页](local/explorer.html)

已解码全部 38 托管镜像、981 资源包；恢复 208 表/30676 记录、191092 资源对象，生成 17872 个 C# 文件，导出纹理/网格/音频与碰撞数据。AOT 的类型声明和机器代码、实际热更新 IL、反编译文本分别保存；没有宣称原作者工程/服务端或真实游玩已还原。

运行 `python prototypes/re-t1-codename-rebirth/scripts/serve_catalog.py` 后访问本机 http://127.0.0.1:8769/，可搜索源码、方法/调用、中文配置、对象与媒体。服务绑定 127.0.0.1，只读本机数据。

原件、副本、第三方源码/配置全文/媒体在 local/ 无远程版本库中保留，不公开上传。完整提交 433faff 纳入 71554 文件 / 8.22 GB；C 盘独立 Git 备份全对象与原 APK 哈希校验通过。local/.git 借用 C 库对象，保存版本历史时须保留 C 库；具体位置见备份证据。原创报告、工具和汇总证据独立备份。
