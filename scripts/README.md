# 开发检查与原型工具

版本：v0.9；日期：2026-10-10。已有准备检查与已授权的素材生成、Unity 准备/测试/构建入口分别说明。013 已有三维 P0，用户后续授权其余首批项目按规范逐个开发、每项单独提交；这些脚本不安装软件、不创建远程仓库或操作发行账户。

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

## 4. 其余首批项目的独立工程入口

[create-first-batch-project.py](create-first-batch-project.py) 每次只建立一个明确编号的独立 Unity 工程，核对已有文件字节，不覆盖不同内容；仅沿本仓已核实的编辑器/模块、已清理设置、原创短音和字体声明，不复制其它游戏规则、缓存、账号或导入 GUID。稳定产品标识在 [配置](create_first_batch_config.py)，通用构建与薄保存/UI源在 [模板目录](unity-templates/)。模板不是所有游戏共用的玩法引擎，规则仍归各项目 Core。

[run-unity-project.ps1](run-unity-project.ps1) 接收仓库内 `-ProjectPath`，验证路径在 prototypes 内及准确编辑器版本，支持 Prepare/Test/Build；实际项目需具备生成的 `PrototypeBuild.Editor.ProjectBuilder`。此入口不适用于仍使用独立 RingToss Builder 的013。

此入口的 Windows 构建采用 Mono + StrictMode 普通单机包，关闭 Development 调试监听，避免原型开发包的网络调试请求；日志仍可用 `-logFile` 指定。它仍是纵切候选，不是Steam发行验收。每次实际错误、警告和包大小读取对应 BuildReport。

051 首次实际 Player 暴露空场景的程序材质会被 shader 剥离。模板现由 Prepare 明确建立 Resources/RuntimeDefault.mat 引用 Standard，并打印 BuildReport 的具体警告；既有工程按需要定向同步，不能仅凭编译通过认定渲染可用。无图形批处理的 AmbientProbe 警告与实际 Player 异常分开记录。

```powershell
& 'C:\Python310\python.exe' -X utf8 './scripts/create-first-batch-project.py' --project 032
& './scripts/run-unity-project.ps1' -ProjectPath 'prototypes/proto-032-sushi-workshop/game/SushiWorkshop' -Mode Test
& './scripts/run-unity-project.ps1' -ProjectPath 'prototypes/proto-032-sushi-workshop/game/SushiWorkshop' -Mode Build
```

新工程复制完成后，不在已修改的工程上强行重跑脚手架；按明确源码/模板版本维护，保留现有 `.meta`。其它编号仅在轮到该项目时生成，不以脚本支持其编号声称已有实现。

产品GUID使用仓库URL与独立项目目录推导的稳定UUID5，不能沿用013的productGUID。五项已核对各自新身份与对应Windows包；前三工程复制身份/032两文本换行已修，独立重建及证据见[备份复核](../docs/development/first-batch-backup-audit.md)。改变已构建源码时更新对应构建清单，不用旧包证明新设置。

[generate-first-batch-music.py](generate-first-batch-music.py) 每次为一个项目生产原创固定谱/seed的短循环 WAV，保留独立 `music-register.json` 与同字节 Resources 副本；是候选音乐，不代表正式混音或实际听检通过。[record-project-evidence.py](record-project-evidence.py) 必须接收实际通过的 `--tests` XML，并读取成功的 BuildReport摘要，保存各项目独立测试、整包/源码哈希及可选 `--capture` 相机图。相机证据固定标为 cameraOnly，不能自动填入真实输入 QA。

实际游戏按F5生成的完整窗口图可用 `--window-capture` 归档；`--input-log` 仅抽取本游戏的规则命令、检查点和准备日志。正常操作的输入步骤/结果与未执行项另存 `evidence/native-qa.json`，由实际观察填写，脚本不自动宣布原生操作通过。

[register-project-resources.py](register-project-resources.py) 接收单个 `--project`，核对九份实际音频/字体源与运行副本同字节，登记来源、适用许可和本项目随包字体声明。事件采用范围以运行代码/开发记录为准；音频哈希不能替代实际听检。

[capture-project-scene.ps1](capture-project-scene.ps1) 对已构建的仓库内独立工程运行明确的 `-captureOnce` 相机诊断，三十秒内退出，异常或缺少准备/出图日志报错。只启动/超时停止本次自己的Player；返回相机图和本游戏日志路径，仍须实际目检，不能自动宣称完整HUD、键鼠输入或听感通过。

[verify-project-evidence.py](verify-project-evidence.py)只读检查完整源码/包路径集合与byte/SHA、XML/BuildReport、登记媒体和字体声明随包、生成器/相机/可选完整窗口图、独立UUID5。单项用 --project 032，全部用 --all，提交后加 --git-ref HEAD 核对Git原始blob，暂存用 --git-ref :。历史productionTools可以对应旧版本；不能以该检查宣布native/听感/真实玩家通过。
