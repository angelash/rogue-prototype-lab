# 制作与检查工具

当前 v2 使用本机恢复夹具与 Unity 工程。先读项目 DEVELOPMENT.md，再执行对应生产入口；不能为换图重新恢复整个场景。

- `prepare_style_v28.py`：生成 38 层制作规格、28 材质、35 组件、17 字色及显式运行颜色迁移；保留 v2.7 规格历史。`create_fidelity_theme.py` 将规格、素材路径和文案合入生产输入；它们属于准备操作，不是渲染通过证据。
- `unity/NightAssetExport.cs`：原规格技术导出、共享纸纹等比采样、原生字形与实际 Unity 构建。`NightReadability.cs`：受保护的颜色/既有字号应用。公开工具与 canonical Assets/Editor 副本须一致。
- `verify_fidelity.py`：来源、规则、文字、字体、图像与真实 Resources 检查。`verify_style_contract.py`：对固定版本逐字段、逐文件和逐像素验证尺寸、alpha、Sprite/Prefab/输入几何锁定；仅允许声明主题颜色变化。
- `run_local_qa.ps1`：实际 QA Player 的 `-VisualOnly`、`-SurfacesOnly`、`-PromptsOnly`、`-RulesOnly`、`-FlowOnly`，可指定 `-EnglishSurvey`、`-EvidenceSuffix`；`-PlayerPath` 仅用于独立副本。输出在 local/QAResults，驱动不进入 Release。
- `package_local.py`：源码与完整 Windows 包、逐文件清单和完整读回。`finalize_style_v28.py`：核对当前源/运行包、12 组渲染、规则/流程、真实原生报告，再更新有限证据摘要；缺报告或 hash 不一致时停止。
- `verify_version_control.py`：全部制作输入、Unity/.meta、资产源与导出、参考夹具、测试源须在 Git 索引且内容对应；缓存、截图、日志和构建须忽略。

临时独立工程可设置 `NIGHT_MARKET_PROJECT_PATH`、`NIGHT_MARKET_ART_OUTPUT`、`NIGHT_MARKET_BUILD_OUTPUT`；最终交付默认 canonical 工程与完整 Release 目录。不能把样稿、离屏摆牌或历史结果记为当前原生通过。

## v1 设计工具（历史）

两个脚本只处理本项目的设计准备，不运行原作/Unity，不安装软件，不访问网络。

1. 修改 `../data/catalog.json`，保持规则正文与版本同步。
2. `python derive_design.py` 重建CSV、全文内容、150生成/150编辑提示词、替换清单、28设施路径及覆盖统计。
3. `python verify_design.py` 检查结构/语义、引用、完整路径与来源处置，输出evidence/design-verification.json，失败退出1。

全部运行依赖仅标准库；字体字形检查如本机有fontTools则读取已审核字体进行真实cmap检查，未找到字体/模块会显式记未执行，不能伪装通过。JSON Schema供后续Editor/外部校验器使用，脚本自身执行本版需要的类型/引用/数值检查。

派生输出不可手工维护，脚本不覆盖catalog和人工设计正文。产后检查与Unity规则测试是后续独立入口，本轮没有编造这些脚本存在或通过。
