# 工程与资产版本管理

2026-10-12 用户要求工程、所有资产和测试用例纳入版本管理；结果和报告可以排除。当前完整制作分支为本仓 **codex/yokai-night-market-source**，Unity 工程路径不变。

完整分支提交以下内容：

- local/UnityProject/Assets 全部文件及 .meta，包括脚本、场景、18 个 prefab、材质、Sprite 网格、图片、字体、音频、XML、恢复清单和验证用例。
- Packages/manifest.json、packages-lock.json 与全部 ProjectSettings。
- fidelity/art 的生图原图、修订源、提示词与谱系；local/NightArt 的全部最终尺寸图片；旧包装制作源继续保留。
- release/ 的发行计划、权利来源清单、商店/问卷草稿、Steam宣发原图/提示及规格导出、模板与发行用例；对应 scripts/ 的生产、门禁和测试。私有授权文书、账号凭据与运行结果只在忽略的 local/steam-release/ 管理。
- scripts/ 的生产、验证和测试入口，fidelity/test-cases/ 的用例规格；re-crunch-time/local 的 decompiled、exports、typetrees 和必需规则/结构输入，供重建及测试使用。

Library、Temp、Obj、Logs、UserSettings、Windows 构建、压缩备份及 local 中的 QA 结果/报告排除。已提交的历史验收摘要保留，新运行输出不必提交。资产不是测试结果：生图原图、最终图片、参考夹具、字体/许可和 .meta 均保留。

现有 origin 是公开仓库。完整分支含原作恢复实现和参考音频等，只做本地 Git 提交和完整 Git bundle 备份，不推送公开远程；main 保留公开原创资料的历史。Git pre-push 在本机检查待推送历史，避免完整分支误推。hook 实现为仓库 scripts/check-private-recovery-push.py，安装在 .git/hooks/pre-push；克隆 Git 本身不会自动安装 hook。

检查完整版本覆盖：`python prototypes/proto-yokai-night-market/scripts/verify_version_control.py`。报告位于 local/source-version-control.json，核对每个必要文件已入索引、字节对应，并验证输出/缓存仍被忽略。

完整历史备份在根目录 local/git-backups/，可用 `git bundle verify <bundle>` 校验，或 `git clone -b codex/yokai-night-market-source <bundle> <目标目录>` 恢复。此前 v2.2 的源码 zip/Player zip 和结果保持历史原样；Git bundle 包括制作源、测试及参考输入，覆盖范围比旧源码 zip 更完整。具体提交与 bundle hash 见本机版本检查记录及本轮实际交付。

首次完整资产提交 e0b1c7ccfbe862d743191f8efc33d63a5e3acd1a；`local/git-backups/yokai-night-market-source-v2.3.bundle` 为 394,453,117 字节，SHA256 `673ad0a2c245f1a0a210516d6d8e568ba1d1247ba94a87cdaa777b9005c7be38`。已执行 bundle verify、克隆恢复和 git fsck --full；新增流程用例的后续提交另存 final bundle，保留首份备份。

2026-10-13 v2.4 将 game-copy-polish skill、来源摘要、copy.json、presentation.json、修订前生图与提示、当前生图和 Unity 导入副本、可读性导出工具及双语视觉测试一并纳入。报告和截图仍按既有输出规则排除。当前完整历史备份使用根 `local/git-backups/yokai-night-market-source-v2.4-reviewed.bundle`；对应提交、SHA256 与恢复核验登记在同目录 `yokai-night-market-v2.4-record.json`，不覆盖早期备份。

2026-10-13 v2.5 将无字提示底板、旧提示历史、31 键文案修订、声明的委托副说明字号、场景及 prefab 工作副本、导出与静态检查工具、NightPromptProbe 测试源/用例合同纳入完整分支。结果、截图、日志和 zip 保持忽略。本轮完整历史备份为根 `local/git-backups/yokai-night-market-source-v2.5-final.bundle`，提交、hash、恢复核验和实际版本覆盖登记在同目录 `yokai-night-market-v2.5-record.json`；公开原创文件推送记录为 `yokai-night-market-public-v2.5.json`。旧版 bundle 和恢复校验仓库保留。

2026-10-13 v2.6 将干预牌分类配色、实际材质与恢复输入、NightVisualProbe 的实际像素断言、运行检查入口及用例规格继续纳入完整分支。82 图片和文案本轮未改；报告、截图和 zip 保持忽略。完整历史备份使用根 `local/git-backups/yokai-night-market-source-v2.6-bands.bundle`，实际提交、hash 和恢复检查记录为 `yokai-night-market-v2.6-record.json`；公开原创资料推送记录为 `yokai-night-market-public-v2.6.json`。

2026-10-13 v2.7 将 26 张功能 UI 运行 PNG、对应制作规格、导出器、NightSurfaceProbe/运行入口和回归用例纳入完整分支，全部 82 份历史生成源保留。新增输入按 Git 索引核对，必需文件为 3,649；结果、截图、zip 和缓存仍忽略。完整历史备份使用根 `local/git-backups/yokai-night-market-source-v2.7-ui.bundle`，提交、hash 和裸仓恢复核验记录为 `yokai-night-market-v2.7-record.json`；公开原创工具与规格推送记录为 `yokai-night-market-public-v2.7.json`。旧版包/bundle 和失败记录保留。

2026-10-13 v2.8 包装集成把 38 个最终 UI PNG、正式工程副本、28 材质/35 组件/17 字色、14 对显示颜色替换、4 份生成底材与提示/谱系、旧 v2.7 规格、严格字段/尺寸/alpha 保护器及语言身份回归纳入完整本地分支。其余 44 图保持，所有历史生成输入继续保留。Git 索引检查为 3,710 个必需输入，其中新方向 57 文件；没有缺失、字节差异或已跟踪缓存/输出。截图、联系表、日志、独立试验工程、zip 和运行报告仍忽略；有限 hash/计数摘要沿既有 evidence 管理。

本轮独立完整历史备份使用根 local/git-backups/yokai-night-market-source-v2.8-style.bundle；实际提交、SHA256、字节数、Git 索引覆盖与裸仓恢复/fsck 结果登记于同目录 yokai-night-market-v2.8-style-record.json。公开原创资料推送记录为 yokai-night-market-public-v2.8-style.json。这些记录以工具真实输出为准；不覆盖此前 art-direction 方案 bundle 或旧源码/Player 包。

2026-10-14 Steam准备新增30个release输入和6个工具，必需版本覆盖为3,746个，索引字节核对通过；游戏工程967输入摘要仍与v2.8最终包一致。所有新增图片、提示、规格、模板和用例受Git管理，公开远程仅接收本轮原创准备资料。独立输入增量包为根local/git-backups/yokai-night-market-steam-preparation-20261014-inputs.zip，真实提交/远程核验、清单与完整读回摘要登记于同目录yokai-night-market-steam-preparation-20261014.json。此增量不是游戏包，也不替代既有完整研究工程bundle；私有授权、税务、账号及运行报告不公开。
