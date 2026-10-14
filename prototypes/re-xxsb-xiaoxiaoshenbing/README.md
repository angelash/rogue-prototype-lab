# 小小神兵 APK 逆向研究

目标为用户指定 Android APK，产品名 **小小神兵**，包名 `com.gnml.xxsb.tt1`，版本 `0.1.3` / `1000300`。2026-10-14 建立独立项目。

- [项目合同](DEVELOPMENT.md) · [来源与权限状态](sources/target-register.md)
- [总分析报告](reports/analysis.md) · [逐系统与玩法](reports/systems.md)
- [代码分层](reports/code-map.md) · [内容与资产](reports/content-and-assets.md) · [恢复边界](reports/recovery-scope.md)
- [完整容器清单](evidence/apk-files.json) · [校验与工具证据](evidence/preparation.json)
- [准备脚本/复跑](scripts/README.md) · [本地备份](evidence/backup.json)
- [准备交叉校验](evidence/verification.json) · [Canvas 语法与 SDK 导出检查](evidence/canvas-check.json)

用户已确认本地分析授权。已全量解析包内 21 托管镜像、36 DEX（含恢复嵌入包装）、两 ABI 的 IL2CPP 声明/地址、485 bundle、150 配置项及 59,312 对象；生成 7,319 C#、8,183 常规 Java 和 17,411 fallback 文件。核心系统已实际阅读，原作者工程/服务器与 native 高级源码分别留边界。

本地只读检索：运行 `python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/serve_catalog.py` 后打开 http://127.0.0.1:8775/，可查看源码、方法/调用、中文配置、对象/媒体及原载体。当前检索数据库包含全部 32,913 恢复源文件、85,028 托管方法与 133,186 ARM64 native 地址。ARMv7 地址另保留文件索引。原始 path ID 与 JSON 字节保持精确。

- [深入静态校验](evidence/deep-verification.json) · [检索目录](evidence/catalog.json)
- [完整本地备份](evidence/deep-backup.json) · [检索 QA](evidence/explorer-qa.json)

APK 副本在 `local/input/`。第三方原件及后续派生代码/媒体只在本地保存；项目说明、原创工具及结构摘要与其分开。

完整本地备份的 `project-snapshot/` 保存脚本、报告、来源、证据与项目合同。恢复时将这份快照复制回本项目根，保留 `local/` 的原相对位置，再按脚本说明重建 SQLite；Git 检出需启用该库的 `core.longpaths`。快照中的备份证据描述已完成的上一次提交，项目根的 `evidence/deep-backup.json` 是最后生成的当前记录。
