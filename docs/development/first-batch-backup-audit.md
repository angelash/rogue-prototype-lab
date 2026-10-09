# 首批候选工程身份与备份复核

版本：v0.1；日期：2026-10-10（Asia/Shanghai）。本记录接续五项候选各自独立提交推送，不改变原需求v0.1、原工作簿或013既有工程。产品身份、源码字节与最新构建有对应证据；不是原生输入/听感或发行验收。

## 发现与修复

121制作时发现脚手架继承013的productGUID。121和后建126使用仓库URL/独立项目目录推导的稳定UUID5；本批五项备份后，逐项只读审计确认032/051/027仍为同一旧值`88d75b1a8132f604ab61b992783bc98d`。公司/产品名与Windows保存目录原本已独立，本次仅修产品元数据，不改名称或保存合同。

032两文件SushiRuntime.cs/PersistenceTests.cs的工作字节是CRLF、Git原始对象为LF，归一后内容相同；原manifest记录工作字节，影响恢复时的精确hash复核。已将两源码归一UTF8/LF，GUID元数据一并更新。051/027源码工作与Git原始字节原本全匹配。未改Core行为、API、地图、数值或样件源；沿用30/28/32的已通过正式XML，不新增/重计测试成绩。

三工程均在Unity退出后修改，按032→051→027分别普通Mono Strict重建、真实Player相机出图并目检，再归档新整包/完整源码清单。最新三包各1条无图形批处理AmbientProbe/ReflectionProbe未更新警告，非运行异常；没有隐写0。原Feature提交仍保存之前候选。

## 最新对应包

- **032 SushiWorkshop**：UUID `0886d11784ca5aa68a5a556f89533227`；86,842,449字节、0错/1警告，UTC `2026-10-09T18:42:23.3657002Z`；日志 `.local/unity/SushiWorkshop-20261010-024207-652-Build/unity.log`。沿用30项真实Passed XML（UTC 2026-10-09 16:19:04Z），[本项目记录](proto-032/2026-10-10-slice-01.md)/[清单](proto-032/evidence/build-manifest.json)。最终相机 SHA `f5e4e1bc93fd368840e078bc331142728f241feab4703b251d4f1265722555bb`，已实际目检。
- **051 RecyclingCleaners**：UUID `4d31d65c78865e8e998124ac5584b97e`；86,881,541字节、0错/1警告，UTC `2026-10-09T18:43:27.2086612Z`；日志 `.local/unity/RecyclingCleaners-20261010-024313-968-Build/unity.log`。沿用28项真实Passed XML（UTC 2026-10-09 17:05:22Z），[本项目记录](proto-051/2026-10-10-slice-01.md)/[清单](proto-051/evidence/build-manifest.json)。最终相机 SHA `0906dbe62b56264b323d488c08b24a846b217ce76cad3481f2becf2702b60ee8`，已实际目检。
- **027 SnakeHatchery**：UUID `bef4ab206ac850c39b9c186ac1924ec1`；86,888,473字节、0错/1警告，UTC `2026-10-09T18:44:53.4373616Z`；日志 `.local/unity/SnakeHatchery-20261010-024439-982-Build/unity.log`。沿用32项真实Passed XML（UTC 2026-10-09 17:29:27Z），[本项目记录](proto-027/2026-10-10-slice-01.md)/[清单](proto-027/evidence/build-manifest.json)。最终相机 SHA `dc6763560ae004eb5f2741d58103fa5c92e860bc445497eafcb4968e60d4a979`，已实际目检。

121最终UUID`ff3a0f5dafd85ca6884d32f8aa47ec63`，126最终UUID`271da73f4ed55d6c9f8431275154ca23`；对应已有独立包和来源清单未改。013旧UUID保持其历史，五项新身份互异且不继承013。

## 可重复检查与后续边界

[verify-project-evidence.py](../../scripts/verify-project-evidence.py)按本项目完整Assets/Packages/ProjectSettings与Windows64文件集合检查登记完整性/去重、逐文件byte/SHA、真实XML与BuildReport摘要、资源源/副本、字体声明随包、源码生成器/相机或实际窗口图摘要、独立UUID5。可用`--project 032`或`--all`，提交后加`--git-ref HEAD`比较Git原始blob；stage时`--git-ref :`比较索引。不改变文件、不安装或运行玩法。脚本在修前032实际发现两源码字节差异和身份错误；定向审阅补齐漏登记/新增文件、空清单、摘要和字体声明验证。

productionTools记录每次证据归档时工具文件的实际摘要，历史项目可能对应不同版本/换行；它不是要求所有历史报告与最新工具一致的缓存。projectSources和files必须与当前对应包/源码完整闭合；新工具本身在Git管理。换行或元数据改变已构建源码时重新构建/归档，不能用旧包宣称新源已验证。

现有Windows安全权限modal仍使正常窗口键鼠、完整HUD/动态和听感未验；各native-qa仍为false。相机RenderTexture不含完整IMGUI且未发玩法命令，真实玩家假设/性能/设备/完整内容/Steam发行继续按各项目合同补证。最新单项提交和共同复核备份见[总进度](first-batch-progress.md)。

最后环境只读复核：2026-10-10 02:50:52+08:00（UTC2026-10-09 18:50:52），仍为No active app、Windows安全中心的取消/允许modal；未操作权限控件。待用户手动取消后，按各项目input-and-ui的正常窗口路径补验，不能自动把现有native-qa=false改为true。
