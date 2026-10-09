# 开发检查与原型工具

版本：v0.5；日期：2026-10-09。已有准备检查与本次授权新增的素材生成、Unity 准备/测试/构建入口分别说明。用户现已授权 013 游戏实现、必要素材接入、测试与 Windows 构建；这些脚本不安装软件、不创建远程仓库或操作发行账户。

## 1. 现状检查

- [check-development-readiness.ps1](check-development-readiness.ps1)：核对已配置的 Unity/Mono、Blender 文件、命令路径、计划工程是否存在及基础主机信息。文件存在与编译/导入/运行通过分别报告。可通过 `-UnityEditorPath`、`-BlenderPath` 覆盖机器路径；相对 `-OutputPath` 按仓库根解析，报告可写到受忽略的 `.local/`。
- [check-docs.py](check-docs.py)：Python 标准库检查中文 Markdown 的 UTF-8/非空、本地文件链接及来源登记 SHA-256；同时要求共同规范、模板、持续规则和所有 `proto-*` 项目根 `DEVELOPMENT.md` 存在，项目 README 链接专属合同、合同链接共同规范。默认跳过仓外绝对引用，换电脑无需具备参考仓；`--check-external` 可核对本机所有来源路径。它不验证规范已实际执行、网页内容、Markdown 锚点、排版、游戏规则或玩法可达性。

在仓库根目录执行：

```powershell
& './scripts/check-development-readiness.ps1' -OutputPath '.local/readiness-inventory.json'
& 'C:\Python310\python.exe' -X utf8 './scripts/check-docs.py' --check-external
```

Python 路径按当前可用运行时选择，上述检查无需 pip 安装。现状检查不启动 Unity，也不把文件存在、基础工具探针或文档检查当作导入、测试与构建成功。

## 2. 原创原型事件音

[generate-prototype-audio.py](generate-prototype-audio.py) 使用 Python 标准库生成七个独立固定 seed 的短音：`launch`、`hit`、`bounce`、`cash`、`retain`、`miss`、`ui_confirm`。输出为 `assets/proto-013-ring-toss/audio/original/*.wav`，48000 Hz、单声道、PCM16；同名不同字节的已有文件会拒绝覆盖。来源、事件建议和逐文件哈希见 [音频登记](../sources/references/woc-audio-reuse-register.md)。

在仓库根目录执行：

```powershell
& 'C:\Python310\python.exe' -X utf8 './scripts/generate-prototype-audio.py'
& 'C:\Python310\python.exe' -X utf8 './scripts/generate-prototype-audio.py' --check
```

生成模式会创建缺少的 WAV；`--check` 只在内存重算并逐字节核对已有文件，不写入资源。当前七个源文件已通过本机 `--check`，与 Unity 工程 `Assets/RingToss/Resources/Audio/` 中的同名 WAV 逐项 SHA256 相同。它们是 P0 音频候选，不是完整音频包；BGM与环境循环仍未接入，实际 Player 播放、混音及事件同步还需验收。

## 3. Unity 原型入口

原创三维模型的 Blender 制作与同步入口为 [build-stall-models.py](build-stall-models.py)，五件素材、预览与尺寸/哈希见 [源登记](../sources/art/proto-013-ring-toss/3d-p0/README.md)。已有输出默认拒绝覆盖；明确已核对制作源后可使用 --overwrite。Unity运行时先用 --source-only 在源目录制作，退出后用 --sync-only 同步已登记FBX，保留.meta；不让生成器与Unity同时写入同一资源目录。

[run-unity-prototype.ps1](run-unity-prototype.ps1) 面向既有工程 `prototypes/proto-013-ring-toss/game/RingTossWorkshop`，使用已核实的 Unity `2022.3.62f3c1`。默认编辑器为 `D:\Program files\2022.3.62f3c1\Editor\Unity.exe`，可用 `-UnityEditorPath` 指定同版本路径；脚本会检查版本，不安装或下载编辑器。

在仓库根目录执行，按需选择模式：

```powershell
& './scripts/run-unity-prototype.ps1' -Mode Prepare
& './scripts/run-unity-prototype.ps1' -Mode Test
& './scripts/run-unity-prototype.ps1' -Mode Build
```

1. `Prepare`：隐藏窗口批处理调用 `RingToss.Editor.ProjectBuilder.Prepare`，在缺少时建立 `Assets/RingToss/Scenes/Bootstrap.unity`，配置场景、文本序列化、产品信息、窗口和 Mono 参数。它会修改本工程准备状态，不是只读探针。
2. `Test`（默认）：运行 EditMode 测试，将结果写入当次日志目录的 `tests.xml`。脚本仅在 Unity 返回 0、报告存在、测试总数至少一项、无失败且结果为 `Passed` 时输出 `UNITY_TEST_PASS`；实际通过结论须引用当次报告，入口存在不算测试完成。
3. `Build`：调用 `RingToss.Editor.ProjectBuilder.BuildWindows`，先执行 Prepare，再以 StrictMode + Development 构建 Windows x64。输出位于工程 `Builds/Windows64/`，包括 `RingTossWorkshop.exe`、完整 Player 目录和 `build-summary.json`；这是开发构建，不是 Steam 正式发行包。

每次运行创建独立 `.local/unity/<时间戳>-<模式>/` 日志目录，包含 `unity.log`，Test 模式另含结果 XML；失败以非零退出或脚本错误报告。三种模式均启动 Unity，并可能导入/更新工程；与第一节的现状检查分开使用。

当前测试与构建的事实以运行日志和结果文件为准，本 README 不预先声明已通过。内容覆盖、故障注入、实际 Player 输入/听感/性能、Blender 正式资产及发行打包仍按 [缺口与推进计划](../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md) 验收。
