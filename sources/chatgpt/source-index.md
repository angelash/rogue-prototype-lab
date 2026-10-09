# 来源索引

归档版本：2026-10-09 v2（补充本地工作簿与仓库备份状态；设计结论未改动）

## 已核实的来源

父任务已完整读取最终答复、全文与对话来源，确认无遗漏标记。本地执行器根据其随后提供的核实正文整理资料。下列链接保留原始消息定位，便于复查。

| 来源 | 链接 | 本归档中的用途 |
| --- | --- | --- |
| 原对话：解析小丑牌设计 | [完整对话](https://chatgpt.com/c/6ac781cd-a38c-83ee-919d-ad5b4904149a) | 整体讨论上下文 |
| 最终修订答复 | [最新消息](https://chatgpt.com/c/6ac781cd-a38c-83ee-919d-ad5b4904149a?messageId=0f782f82-bad3-59e7-8aa2-8dd30fae291b) | 2026-10-09 03:13:16 UTC 更新；核心结论、31 候选及原型建议 |
| 用户全面复审要求 | [用户消息](https://chatgpt.com/c/6ac781cd-a38c-83ee-919d-ad5b4904149a?messageId=797088f0-6484-488d-b7c9-1b4b9e34883e) | 用户明确要求的复审依据 |
| 历史 120 案 | [历史消息](https://chatgpt.com/c/6ac781cd-a38c-83ee-919d-ad5b4904149a?messageId=6ddc2b11-6e42-5e0f-a2f0-8aae25501d11) | 历史版本边界及筛选记录 |
| 完整原文粘贴 | [全文消息](https://chatgpt.com/c/6ac781cd-a38c-83ee-919d-ad5b4904149a?messageId=9cf6db39-b1d2-4c0c-b5f6-f2d09b710280) | 原文章全文来源 |
| Paranoia 原文章 | [从Balatro小丑牌的成功说起：浅谈rogue的核心体验与设计](https://zhuanlan.zhihu.com/p/688748483) | “建立系统，而不是定一个目标”的原文含义 |
| 原工作簿 | [Library 原文件](https://chatgpt.com/api/library/files/libfile_1d5fabfec0908191bbd34836c2e70204/download) | 全部 120 逐案理由与旧字段；本地同名文件已归档，检查范围见来源登记 |

原工作簿的稳定身份、原文件名和版本见 [source-register.md](source-register.md)。

## 本地内容与验证边界

本机已有经核实正文整理的设计原则、31 个候选总览、首批验证计划、版本调整和研发方法，入口为 [设计归档 README](../../docs/design/README.md)。

初次归档时，原 .xlsx 下载及一次受支持重试均失败。2026-10-09 仓库备份时，已在 `docs/熟悉与意外_120个游戏方案.xlsx` 发现同名文件，并在保留初始 Git 快照后移入 [sources/chatgpt/](熟悉与意外_120个游戏方案.xlsx)。其大小为 149077 字节，XLSX 包结构和四个工作表名称符合既有来源记录，SHA-256 见 [checksums.sha256](checksums.sha256)。

未重新复审单元格内容，未补造旧 120 案逐项明细，也未与云端逐字节比对。Library 下载链接继续保留为来源入口。

本归档没有原型实测数据；候选原创性仅做过少量相邻作品核对；用户尚未指定平台、商业模式和预算。
