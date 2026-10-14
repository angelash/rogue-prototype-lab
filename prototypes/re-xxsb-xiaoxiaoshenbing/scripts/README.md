# 准备与复跑

本节保留准备阶段入口：建立校验副本、全 ZIP 条目大小/CRC/SHA256 和 Android badging/permissions 摘要。`prepare.py` 本身不解包 payload、不反编译、不运行或联网。授权后的完整流水线见后文。已存在副本不同则停止，不覆盖。

在仓库根运行：

```powershell
python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/prepare.py 'D:\download\a1-debug-0.1.3-x-202609032009_1782290212664330-1-4399--tt1.apk' --aapt 'D:\androidSdk\build-tools\34.0.0\aapt.exe'
```

输入固定为已登记 SHA256；其他版本需另建来源记录，不把它冒充当前 APK。

- `evidence/apk-files.json`：所有条目元数据/摘要，无内容导出。
- `evidence/preparation.json`：工具、命令、身份、引擎结构、覆盖和验证结果。
- `local/input/`：同名 APK 副本。
- `local/logs/`：aapt 实际命令输出。

后文已添加授权后的代码/内容/资产解析及恢复验证入口；本准备脚本不替代深入结果。

本地输入备份运行 `python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/backup.py`，创建独立无远程 `local/.git`，提交 APK、忽略和文件属性，校验 Git blob 的 SHA256 并执行 Git 对象检查。完整命令输出属于生成结果，未纳入本地输入提交。备份摘要写入 `evidence/backup.json`。

`python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/verify_preparation.py` 核对身份、文件/大小/哈希计数闭合、本地备份及文档链接，写入 `evidence/verification.json`。该检查不重新计算所有 CRC，也不验证尚未执行的游戏分析。

## 授权后的完整静态流水线

用户已确认本地分析授权。原件只读，完整输入与派生代码/配置/媒体仅 `local/` 保存。

依次运行以下脚本，前一步成功后再进行后一步：

1. `extract_apk.py`：完整校验解包。
2. `analyze_structure.py`、`analyze_manifest.py`：DEX/ELF 与清单；尚未恢复的嵌入包装先留原件。
3. `run_il2cpp.py`，再 `run_il2cpp.py --abi armeabi-v7a`：两 ABI 元数据配对与声明/地址。`compare_native_abis.py` 核对名称多重集合及可执行范围。
4. `extract_hotfixes.py`、`decompile_managed.py`：21 PE 原始字节、全 IL 索引与 C# 阅读工程。
5. `analyze_assets.py`、`repair_assets.py`：485 包与基础文件、全对象/类型树/TextAsset，修正 player 根对齐与生成器 string[] 派发。
6. `analyze_content.py`、`inspect_payloads.py`、`export_media.py`、`link_content.py`：全部配置池、载体、媒体、语言及资源关联。
7. `analyze_android.py`、`recover_embedded_dex.py`：35 原标准 DEX + 1 包装恢复，常规/fallback/SDK 三层证据；调用 `jadx.cli.JadxCLI`，不启用 GUI。
8. `disassemble_native.py`：ARM64 全可执行节与 IL2CPP 标签；ARMv7 的原件/声明/地址另存。
9. `build_catalog.py`：本地 SQLite 全文与对象/配置索引，可重复生成；使用 8 文件读取线程，数据库为派生结果。
10. 启动 `serve_catalog.py` 后运行 `verify_catalog.py`。当前 UI/元数据校验与真实游戏测试分开；实际浏览器观察需单独记录，不能由 API 脚本推定。
11. `backup_full.py`：独立无远程 local Git 的完整输入、代码、资产、工具和本项目文档快照；为已存在的长 Java 类名启用此库的 `core.longpaths`。
12. `verify_deep.py`：输入、计数、原字节/媒体、关联及当前文档链接的最终闭合校验。

命令形式均为 `python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/<脚本>`；脚本读取项目目录，不依赖当前 cwd。原 `prepare.py/backup.py` 是准备阶段入口，其历史证据不覆盖深入结果。

## 只读检索

`python prototypes/re-xxsb-xiaoxiaoshenbing/scripts/serve_catalog.py --port 8775`，访问本机 `http://127.0.0.1:8775/`。服务只绑定 127.0.0.1，数据库只读、所有接口为 GET，按记录 ID 读取已登记源/媒体，路径限制在本项目 local。

可检索 32,913 C#/Java/fallback 文件、85,028 托管方法/调用和 133,186 ARM64 native 地址，150 表/10,625 配置记录、59,312 对象及 92 载体。ARMv7 地址在 local/il2cpp/armv7 保存；界面不将 64-bit path ID 转成不精确的 JavaScript 数字，原 JSON 可按文本读取。

## 实际工具与来源

本机已有 Python 3.10 / UnityPy 1.25.0、Pillow、Android build-tools 34.0.0、JDK17、.NET。ILSpy 9.1.0.7988、Il2CppDumper 6.7.46、JADX 1.5.3、TypeTreeGeneratorAPI 0.0.10 与 Capstone 5.0.6 从既有本地工具缓存复制到本项目 `local/tools`，未安装新软件。MetadataIndex 工具源码保存在 `scripts/metadata-index`。版本/输入/工具摘要及命令在各证据中；只有工具数据被复用，没有混入其他游戏资料。

TypeTreeGenerator 仅读取声明，MetadataIndex 用 System.Reflection.Metadata 读 PE，未执行目标程序集。JADX 初次错误启用了 GUI，已经停止并改用 CLI，旧日志在 local/logs/jadx-normal-gui-history.txt 保留。所有商业代码、全文配置、媒体和 SDK 字符串留本机，公开资料不包含这些内容。
