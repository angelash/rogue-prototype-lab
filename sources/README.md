# 原始资料

sources/ 保存设计方案的可追溯来源，整理后的结论放到 docs/design/。

| 文件 | 用途 |
| --- | --- |
| [chatgpt/source-index.md](chatgpt/source-index.md) | 原对话、消息定位、文章与原工作簿入口 |
| [chatgpt/source-register.md](chatgpt/source-register.md) | 原工作簿身份、版本、时间及本地归档状态 |
| [chatgpt/熟悉与意外_120个游戏方案.xlsx](chatgpt/熟悉与意外_120个游戏方案.xlsx) | 保留原文件名和内容的本地工作簿 |
| [chatgpt/checksums.sha256](chatgpt/checksums.sha256) | 本地工作簿的 SHA-256 |

“熟悉与意外_120个游戏方案.xlsx”保留原文件名，按既有核实记录，当前方案库实际是 31 个修订主候选。文件名中的 120 是历史命名。2026-10-09 仓库备份时已发现本地同名文件并归档；文件大小、包结构和工作表名称已核对，未重新复审单元格内容或与云端逐字节比对。

本地执行器已按父任务完整读取后提供的核实正文完成中文归档；没有修改原工作簿。

2026-10-09 后续为第一批项目只读核对了六个候选及相关优先级单元格，定位与筛选理由见 [第一批来源登记](chatgpt/first-batch-selection.md)。该局部读取不代表已复审全部旧方案；原文件及校验值不变。

初始快照保留工作簿原来位于 `docs/` 的版本。新增资料的版本保存、上传及恢复约定见 [仓库与备份管理](../docs/version-control.md)。

2026-10-09 技术与制作补充的 [本地工程参考登记](references/local-project-reference-register.md) 保存 highschool、world-of-claudecraft 的原路径/文件名、Git 快照、校验和筛选理由，以及现有 Unity/Blender 能力证据。外部原文件和媒体保持只读，没有复制整个参考仓或将候选素材入库；整理方案见 013 的 [完整设计入口](../docs/design/first-batch/proto-013-ring-toss/full-game/README.md)。
