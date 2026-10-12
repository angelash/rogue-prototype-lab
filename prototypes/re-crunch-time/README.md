# Crunch Time! 逆向分析

用户指定 Steam 安装：`E:\SteamLibrary\steamapps\common\Crunch Time`。分析日期：2026-10-12。

- [项目合同](DEVELOPMENT.md)
- [来源与工具记录](sources/target-register.md)
- [完整分析报告](reports/analysis.md)
- [程序架构与状态机](reports/architecture.md) · [规则与全部内容](reports/rules-and-content.md) · [资产与场景](reports/assets.md) · [全部代码地图](reports/code-map.md)
- [可搜索的本地目录](local/explorer.html)：全部卡牌/项目/文本、资产预览、代码符号和场景对象。
- [复跑工具](scripts/README.md) · [19 项验证结果](evidence/verification.json)
- 本地副本：`local/build-copy/`；反编译与资产：`local/` 内相应目录。该目录由 Git 忽略，不上传原游戏代码和媒体。

本项目研究已安装版本的代码、内容与资源；派生 C# 不是原作者源码，静态场景与配置分析不是运行体验验证。

当前已完成本构建的全量静态分析：6 个托管程序集全部反编译；游戏自身 40 类型/663 方法/959 字段，1985 Unity 对象与 4443 引用；95 卡牌/28 项目/420 文本键；345 非空导出文件通过解码。原件和副本校验一致。确认项目 XML 的 Animation 不被加载器读取，实际 Testing 默认 2，16 项目受数值影响。

仓库备份的是原创报告、工具和索引；本地 `local/` 包含原安装、第三方代码/媒体和完整文本，不在 Git 上传范围。重新克隆仓库需使用本机合法安装运行工具生成 local 产物。
