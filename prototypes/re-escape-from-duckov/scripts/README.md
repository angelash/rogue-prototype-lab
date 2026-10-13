# 分析工具与复跑

输入固定为用户指定的 Steam 2.3.30，Unity 容器模式为 2022.3.62f2。工具只在副本上分析，原安装和真实存档不写入。全量操作会产生数 GB 本机数据，C# 部分转换可能较慢；失败日志保留，工具不自动安装软件。

## 工具要求

已有 Python 3.10.8、UnityPy 1.25.0、Pillow、texture2ddecoder 1.0.6、fmod_toolkit 0.1.3 / pyfmodex、TypeTreeGeneratorAPI 0.0.10；.NET SDK 9.0.309 和 ILSpy 9.1.0.7988。具体实测版本与工具来源见 [来源记录](../sources/target-register.md)。ILSpy 与生成器已从仓库本机既有工具缓存复制到 `local/tools/`，未安装新软件；源码/媒体未从其它游戏混入。

## 常用入口

从项目根或任意路径运行 `scripts/start-explorer.ps1`，可用 `-Python` 指定解释器、`-Port` 指定端口。服务仅监听 127.0.0.1，资产 SQLite 只读，媒体只从 exports 子树提供；打开 [本地研究目录](http://127.0.0.1:8784)。关闭启动终端即可结束前台服务。

独立核验：`python scripts/verify.py --project .`。源安装升级后哈希核验会失败；保留本轮副本，不覆盖旧分析目录冒称同一输入。

完整复跑：`scripts/run-analysis.ps1`，可指定 `-Source`、`-Python`、`-Ilspy`、`-Generator`，已有代码输出时可使用 `-SkipCode`。复制阶段遇已有副本与输入不一致会拒绝，不覆盖。对于别的游戏版本须另建输入快照并核对模式，不能直接套本轮断言。

## 各阶段

prepare.py 复制和核对全文件；analyze_code.py 调用元数据读器和 ILSpy，保存 C#/IL/类型/方法/字段/调用索引；metadata-index/ 是原创 .NET 元数据工具，不执行目标 DLL。

analyze_assets.py 全容器/对象/引用索引；repair_mono.py 重读全部 MonoBehaviour，修正生成模式基础对齐与列表包装；export_media.py 导出支持类型，保留无法展开的 Shader 原载荷及 BC6H 原压缩数据。

analyze_audio.py 使用 copied FMOD DLL 的 NOSOUND 描述读取和 FSB 样本解码；analyze_content.py 从已解析登记库连接内容与本地化；native_index.py 读取 PE 头、导入/导出；verify.py 独立检查哈希、对象字节、引用与媒体结构。

serve_explorer.py / explorer.html 提供搜索、分页、配置详情、代码方法、对象双向引用和本地媒体预览。data JSON 均视为数据，界面使用 HTML 转义；不执行资产脚本、游戏 Mods 或程序集入口。

## 复跑边界

当前 NodeCanvas 的 C# 项目转换仍记录 failed，完整 IL 成功；额外恢复的生成类保存于 `local/decompiled-repairs/NodeCanvas/recover-0.cs`。两类对象仅基础字段、七个 Shader raw-only、十七个空载荷对象不是脚本异常丢弃；复跑后应重新阅读覆盖摘要。结构化树中的大二进制/数组为紧凑摘要，原数据从安装副本读取。

本机 local 是独立无远程 Git 仓库，外层忽略 local；不要给内部仓库配置公开远程。[backup_local.py](backup_local.py)核对全部研究文件入库、安装文件 Git blob 与工作字节、Git 完整性，再在指定的新路径生成独立 bundle，不覆盖既有备份。全量快照恢复后，依赖工具仍需要相应 runtime；分析包不是可运行的作者 Unity 工程。

## Unity 原生恢复

现使用本机 Unity 2022.3.62f3c1；独立宿主为 `local/UnityProject`，全部派生代码、原资产与构建仅本机保存。进度和已通过/未通过范围见 [还原记录](../reports/restoration.md)，不要把可加载资源当成完整游戏验收。

`prepare_runtime.py` 按登记拷贝原匹配运行 DLL；`scripts/unity/` 是原创宿主、构建和实测代码。Unity Editor 对工程执行 `DuckovRecoveryEditor.BuildNativeProbe` 生成独立 Windows Mono 宿主。需要稳定完成构建后再生成 Player，避免在 Unity 写构建文件期间读取。

`prepare_native_player.py --project <本项目> --output <新的专属目录>` 使用本地宿主引擎、构建/许可设置与独立产品身份，保留原 Resources/shared 文件布局和原脚本登记，逐容器核验原对象载荷不变。`--game-scenes` 同时接回原 63 个场景和编号；Steam SDK 使用原合法 AppID 进行正常开发启动，原 Steam 检查保持。

资源实测参数：`--duckov-runtime-probe --duckov-report-root <证据目录>`，只做资源检查后退出。游戏集成证据参数：`--duckov-game-qa <证据目录>`，等待原启动流程、记录场景/异常并尝试截图后退出；不代表真实输入、主观听音或全内容覆盖。

当前正常窗口候选：`local/Player/DuckovRestored.exe`。`start-restored-game.ps1` 从所在工程解析默认 Player 目录，核对本机宿主引擎哈希后启动，保存独立新日志，不改游戏 Options；原分辨率逻辑会覆盖命令行设置。无 batchmode 的标题画面已实测，batchmode 空键盘/黑截图失败独立保留。

已构建宿主另存 `local/runtime-host-build/Player`，纳入本地源码/输入快照；从备份复跑可传 `--host <该 Player 目录>`。所有容器来源/输出哈希清单为 `local/native-player-inputs-game.json`。缓存链接不纳入完整快照，不把缓存存在视为备份完成。

`native_bundle.py`、`build_native_runtime.py`、`build_native_scenes.py`、`merge_native_assets.py` 及其它 binding/schema 工具是 AB 恢复实验，部分 Editor 成功、全量 Player 仍失败，不能作为正式运行入口。它们保留原载荷、显式记录指针/脚本元数据适配；源码派生编辑工程 `local/AssetRipperExport/ExportedProject` 还需要独立导入验收。Library、NativeProbe 和失败试验缓存已迁回 F 盘 `local/`；缓存不纳入完整源码快照，D 原件保留。

`migrate_storage.py` 是本次已核对路径的单次迁移工具：新阶段复制、逐文件 SHA256、检查源未变化、再切换目录；不删除 D 原目录。重复运行会拒绝覆盖已有目标，迁移记录与逐文件清单保存于本机 storage-migration 子树。[汇总证据](../evidence/storage-migration.json)可公开备份。
