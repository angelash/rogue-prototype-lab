# 分析工具

`intake.py`：创建 APK 工作副本、SHA256 比对、逐 ZIP 条目哈希和 CRC 校验、导出到隔离目录；保留原文件名，不覆盖不一致的既有副本。

```powershell
python prototypes/re-t1-codename-rebirth/scripts/intake.py --source 'D:\download\t1_compat_debug-0.9.0-x-202610131448_1782199009743830-1-4399--yf_test.apk'
```

静态分析不会运行 APK，不与其服务端通信。实际范围与错误见 evidence，原始诊断留在 local/logs。

依次执行 `analyze_manifest.py`、`analyze_structure.py`、`analyze_readable_assets.py`、`build_catalog.py`、`verify_analysis.py`。这些脚本需要 Python 3.10、UnityPy 1.25.0、Pillow、FFprobe，以及本机 Android SDK build-tools 34.0.0 的 aapt/dexdump；不自动安装依赖。入口为 [run-analysis.ps1](run-analysis.ps1)。普通清单解析是原创 Python 实现，字段顺序依据 [YooAsset 1.4.6-preview](https://github.com/tuyoogame/YooAsset/tree/1.4.6-preview/Assets/YooAsset/Runtime/PatchSystem)；未复制第三方代码进入公开工具。

- manifest：精确消费普通/扩展清单，地址与文件/依赖存在性；扩展整数只保留，不猜语义。
- structure：完整标准 DEX 类/方法/字段和 ELF 头/节/符号/动态依赖，敏感字符串留在本机，不扫描凭据。
- readable_assets：仅基础 Unity 与一个标准 firstbundle，完整/基类/失败区分，原生媒体格式核验。
- catalog：真实 manifest 身份、脚本/配置表名和离线检索页，不联网读取游戏内容。
- verify：原副本/全部解包字节、清单 MD5/CRC、SDK 独立 DEX 校验、计数闭合与链接。网页 14 项实际检查独立记录，复跑不自动冒称重新做了浏览器测试。

目录：`local/input` 原名 APK；`local/apk` 全字节；`local/dex` / `elf` 元数据；`local/unity-trees` 可读结构；`local/manifest-*.json` 全地址/包；`local/code-symbols.json` 脚本身份；`local/explorer.html` 自包含搜索页。它可直接在浏览器打开，无需服务器；本文工具不解析 981 自定义封装或 QXGM。

本地版本库位于 `local/.git`，没有远程；完整输入/解包与本机索引受版本管理。公开主仓只纳入本项目原创报告/脚本和必要摘要。
