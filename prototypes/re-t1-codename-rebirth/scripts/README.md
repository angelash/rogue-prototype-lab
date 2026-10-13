# 分析工具与复跑

所有第三方输入/派生资料写入 local/；APK 不安装、不执行，工具不调用游戏在线服务。先用 run-analysis.ps1 建立第一层输入/清单/DEX/ELF与历史可读对象，再执行 run-deep-analysis.ps1。深层步骤已分别实际运行，组合入口通过语法检查；未声称再执行了一遍耗时媒体流水线。

## 依赖与固定目标

Python 3.10.8、UnityPy 1.25.0、NumPy/Pillow/cryptography、FFprobe、.NET 9、Android build-tools 34.0.0；本机局部 tools/python 有 Capstone 5.0.6、pyelftools 0.32、TypeTreeGeneratorAPI 0.0.10。工具不隐式安装依赖。

Il2CppDumper 6.7.46 从[官方发行](https://github.com/Perfare/Il2CppDumper/releases/tag/v6.7.46)获取，zip SHA256 c7f365347ce6bd816cdf774830e44ac46aea59c391c94f388605667394d6aba2，位于 local/tools/il2cppdumper，RequireAnyKey=false。ILSpy CLI 9.1.0.7988 使用本机既有工具，可用 -IlspyExe 指定。JADX 1.5.3 [官方发行](https://github.com/skylot/jadx/releases/tag/v1.5.3)，zip SHA256 8280f3799c0273fe797a2bcd90258c943e451fd195f13d05400de5e6451d15ec，使用 JDK17；PATH 的旧 JRE8 不兼容。TypeTreeGenerator 按[官方接口](https://github.com/UnityPy-Org/TypeTreeGeneratorAPI)仅读取声明，不执行游戏程序集。

封装解码脚本针对来源登记的单一 APK/ARM64 布局。构建参数从原 ELF 读取，不在公开工具中写入密钥；其它版本不能直接套用固定 native 地址。所有输出与 sourceSHA 应按本版本证据核对。

## 深层步骤

- decode_qxgm.py：页流、逐页/表摘要和头 HMAC；额外 QGSG 签名未独立验证。
- Il2CppDumper：标准 metadata + ARM64 库导出 AOT 声明、地址/签名；无原生 C# 方法体恢复承诺。
- decode_bundles.py / inspect_bundles.py：ResourcePayloadMixer、全 981 容器、全对象树和无损 TextAsset；大数组语义 JSON 为摘要，原包保留完整值。
- decode_hotfix.py：38 清单/208 分片/163613 块的 SHA与 AES-GCM，恢复完整 PE；外层 QXTL 签名未独立验证。
- metadata-index：原创 .NET PE/IL 解析，完整类型/字段/方法/token、指令计数、调用与字面量。ILSpy 对两 profile 全 38 镜像生成 C#；业务引用完整 native 声明，AOT 补充镜像只展开携带的 IL。
- repair_base_trees.py：优先复用同构建内嵌节点，其次全 native 声明生成；补齐 967/970 组件，失败精确保留。
- decode_config.py / link_content.py：QCFG v2全池/列/行/索引验证，真实 LangMgr double 哈希、图标映射及中文检索关联。
- inspect_payloads.py：JSON、碰撞、RIFF；HCRH/SDF/QXTM/QFGS 等明确保留原生语义边界。两个 PNG 修复只另存首字节派生副本。
- export_media.py / repair_texture_exports.py：全 Texture2D/Mesh/AudioClip；浮点贴图 RAW/NPY全值与归一化 PNG预览，空字体图集分类。OBJ 不保留 Unity 蒙皮/材质/变换，原序列化完整保留。
- export_fmod_samples.py：RIFF SND 中 FSB5 的 333 样本，保存 FSB/WAV；不声称事件混音/听感验证。
- run-android-analysis.ps1 / collect_android_evidence.py：JADX常规与逐 DEX fallback、SDK全指令；15常规错误和空 DEX 分别登记，计数与独立解析器核对。
- build_deep_catalog.py / serve_catalog.py：SQLite 与绑定 127.0.0.1 的只读检索，类型/源码、方法/调用、配置中文、对象与媒体。
- verify_deep.py：重新读取 SHA、PE/metadata、PNG/WAV/OBJ bounds、NPY与RAW、计数/SQLite/链接；不将历史证据或 QA 当作游戏运行。
- verify_local_backup.py：检查本地 Git 全对象、原 APK blob、完整恢复目录计数、状态和无远程条件；仅输出汇总证据。

## 本机入口

```powershell
python prototypes/re-t1-codename-rebirth/scripts/serve_catalog.py --port 8769
python prototypes/re-t1-codename-rebirth/scripts/verify_deep.py
```

浏览器打开 http://127.0.0.1:8769/；Ctrl+C 停止。原 local/explorer.html 是自包含初查地址页。深层反编译/全文/媒体均在本机，公开 clone 不包含这些第三方资料。

## 保存与范围

local/.git 无远程，源码/输入/资产全字节保留，tools/logs 是缓存；可追溯摘要与诊断另存。完整提交 433faff 的 71554 文件 / 8.22 GB 另保存在项目内 `local/.git/private-backup` 的独立 Git 库，此前的 C 盘临时库已按用户授权搬回。此库无 alternate 依赖、全对象校验通过；工作库采用 `../private-backup/objects` 相对引用，全部对象位于项目内。保存版本历史时保留整个 local/.git。[备份证据](../evidence/deep-backup.json)、[迁移记录](../evidence/storage-migration.json)。verify_local_backup.py 支持解析 Git 的相对 alternate 路径，核对两库与外部依赖，无联网行为；历史 C 盘证据保留在 storage-before-f-return.json。

公开仓库仅原创工具/报告和汇总证据，[远程记录](../evidence/deep-publication.json)。当前完整静态交付与原作者工程、所有 native 高级伪代码、服务器及实际游玩验证分开登记。
