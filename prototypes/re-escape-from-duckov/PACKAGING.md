# HybridUnityProject 分享与交接

2026-10-14。用户已明确确认权利方允许向本次接收方分发原游戏反编译代码、DLL 与资产。本次生成完整混合工程交接 ZIP，包含 Unity 源码工程、必要依赖与原生运行包；此授权记录对应本次接收方，不自动说明后续公开分发范围。

## 当前工程的必要组成

完整工程交接需要以下内容，并保留原 `.meta`、asmdef、包清单和锁文件：

- `local/HybridUnityProject/Assets`：十个反编译派生业务程序集源码、原创宿主/QA、插件及构建种子场景。
- `local/HybridUnityProject/Packages`：嵌入的 Burst、Input System、UGUI 及包清单；目前不是仅有一个 manifest 的在线依赖工程。
- `local/HybridUnityProject/ProjectSettings`：本地版本、产品、渲染、输入和构建设置。
- `local/HybridPlayer`：完整原生运行宿主。63 个游戏场景、res/shared/流文件在这里；只压缩 Unity 工程目录不能运行完整游戏。
- `local/build-copy/Duckov_Data/Managed`：原始托管引用，供恢复观察器编译和组件绑定审计使用。
- `local/HybridBuild/<程序集>/compile-report.json`：十个业务程序集的编译记录，现有封装脚本会读取它们。
- 原创构建、启动、核验脚本和交接说明。重建时必须处理基础运行包路径与编译器路径，不能直接套用制作者机器的默认 D/C 盘路径。

包内沿用这些相对路径；不包含第二份旧 `local/Player`，打包版构建脚本会使用包内 `local/HybridPlayer` 作为已有冻结资源基线，避免重复约 3 GB。原始托管引用只保留 Managed 目录，完整原始安装副本和反编译研究库不纳入。

## 应排除的文件

`Library`、`Temp`、`Logs`、`obj`、`UserSettings`、`.git`、IDE 工程/缓存、旧候选、原始完整研究库、AssetRipper 全导出、个人存档、账号日志和离线 Git bundle 均不作为最小交接内容。包内保留适用依赖许可；不通过删除必需依赖或 `.meta` 来缩小体积。

## 接收方启动方式

当前混合版使用 Windows PC、Unity 2022.3.62f3c1、Windows x64 Mono。

完整工程解压后应保留 `local/HybridUnityProject` 与 `local/HybridPlayer` 的相邻位置。用对应 Unity 版本打开 `local/HybridUnityProject`，等导入/编译完成，选择 `Duckov → 混合源码版 → 运行原生资源游戏`。Play 打开独立 Player；编辑器 Game/Hierarchy 尚未接入完整原生场景。`Probe.unity` 只是引擎构建种子。

修改业务源码后必须重新构建并把新 DLL 接回原生宿主；只点击 Play 会运行已有 Player。重建前关闭本工程的 Unity Editor 和游戏窗口。默认 Unity 路径和 Python 路径属于制作者机器，接收方需指定自己的路径；.NET SDK 9、Python 3.10、UnityPy 1.25.0 用于重建，Pillow/numpy 只用于图像核验。包内不安装这些工具。

从解压目录打开 PowerShell，示例：

```powershell
# 直接运行已有游戏，不需要先安装 Unity/Python/.NET SDK
& .\scripts\start-hybrid-project.ps1

# 打开 Unity 工程：将参数换成接收方实际安装路径
& .\scripts\start-hybrid-project.ps1 -Editor -Unity 'C:\实际安装路径\Editor\Unity.exe'

# 源码重建并运行
& .\scripts\build-hybrid-project.ps1 -Unity 'C:\实际安装路径\Editor\Unity.exe' -Python 'C:\实际安装路径\python.exe' -Launch
```

打包版 ABI 核验会从 `-Unity` 推导 Cecil 路径，Python 可指定路径或从 PATH 查找；这些适配只作用于压缩包内的脚本，原工作工程不改。

代表性游戏集成与同条件冻结标题样本已通过。Steam 初始化仍有错误，全游戏内容、真实操作和动态画面覆盖没有完成，不能向接收方描述为“100% 还原”。详见 `reports/hybrid-restoration.md`。包内不包含制作机器的个人存档和 Options，接收方首次窗口/画质设置以自己的独立保存目录为准。

## 压缩包验证

根目录的 `manifest.json` 登记每个文件的大小和 SHA256、源码与已测运行包的一致性、必要依赖及打包脚本适配。打包工具会重新读取全部 ZIP 条目，核验 CRC 与解压后 SHA256。该检查证明压缩文件完整；接收方另一台机器的 Unity 导入、平台条件和游玩仍需在实际环境复验。
