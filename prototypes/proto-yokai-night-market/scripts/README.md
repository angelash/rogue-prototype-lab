# 设计工具

两个脚本只处理本项目的设计准备，不运行原作/Unity，不安装软件，不访问网络。

1. 修改 `../data/catalog.json`，保持规则正文与版本同步。
2. `python derive_design.py` 重建CSV、全文内容、150生成/150编辑提示词、替换清单、28设施路径及覆盖统计。
3. `python verify_design.py` 检查结构/语义、引用、完整路径与来源处置，输出evidence/design-verification.json，失败退出1。

全部运行依赖仅标准库；字体字形检查如本机有fontTools则读取已审核字体进行真实cmap检查，未找到字体/模块会显式记未执行，不能伪装通过。JSON Schema供后续Editor/外部校验器使用，脚本自身执行本版需要的类型/引用/数值检查。

派生输出不可手工维护，脚本不覆盖catalog和人工设计正文。产后检查与Unity规则测试是后续独立入口，本轮没有编造这些脚本存在或通过。
