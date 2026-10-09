# 开发能力核查与临时探针证据

版本：v1.0；核查日期：2026-10-09（Asia/Shanghai）。基准提交为 `7ff7335941c1a05f5d4eb7e37f62a8dfaaa03554`，之后追加本轮审计。用户要求由助手承担后续开发，并现在整理内容及能力缺口。

本轮查阅既有 00–11、两项仓库技能、本机工具元数据与指定参考工程。新增准备检查脚本，运行隔离的制作工具探针；没有创建正式 Unity 工程、启动 Unity、安装软件、制作发行资产、录制其他应用或查询账户凭据。原参考文件和工作簿未修改。

## 1. 本机与命令通道

- Unity 与模块证据沿用 [本地参考登记](local-project-reference-register.md)：2022.3.62f3c1、revision `1623fc0bbb97`，Windows x64 Mono 开发/非开发文件存在。当前准备脚本再次读取同一 ProductVersion；启动、许可、包解析与本游戏构建未验证。
- Blender CLI：5.2.2 LTS，`D:\SteamLibrary\steamapps\common\Blender\blender.exe`。本轮不只查询版本，实际建模/导出/离线渲染见第 3 节。
- 现有 PATH/指定路径实查：Git、rg、PowerShell 7.6.5、Python 3.10.8、Node 24.19.0、FFmpeg/ffprobe 8.0.1 full build。
- `C:\Program Files\dotnet\dotnet.exe --list-sdks` 返回 9.0.309；Unity 内置 Mono 版本查询返回 6.13.0，`Editor/Data/DotNetSdkRoslyn/csc.dll` 存在。全局 PATH 未定位 csc/mcs/mono/msbuild/nunit3-console 不等于无法编译；后续使用实际 Unity 编译/测试入口。
- 本机 Python 可导入 PIL、numpy、pytest、cv2、yaml；本轮两个新准备脚本不要求安装 Python 包。没有将 pyautogui 作为必要依赖。
- [check-development-readiness.ps1](../../scripts/check-development-readiness.ps1) 已实际运行，报告为文件/命令清单而非全流程通过；计划工程的 Assets、manifest、ProjectVersion 均未出现。原始 JSON 在受忽略的 `.local/readiness-2026-10-09/`。

硬件/系统只记录开发相关字段：Windows 11 家庭版 Insider Preview `10.0.26220`、64 位；i9-13900HX（24 核/32 线程）、RAM 63.75 GiB；Intel UHD 与 RTX 4060 Laptop GPU。NVIDIA 查询返回 8188 MiB、驱动 616.92；WMI AdapterRAM 不作为显存容量依据。显示记录包含 1920×1080 和 2560×1440，也存在虚拟显示适配器。该机器的配置不能当成最低配置或目标 Player 性能实测。

当次 NTFS 剩余空间约 C 58.7 GiB、D 102.6 GiB、F 85.0 GiB，均为会变化的快照。没有核实第二台机器、正式版 Windows 11 或 Windows 10 测试环境。HID 名称筛选未确认手柄，不据此断言没有设备；Steam 客户端 exe 存在不等于本产品有 AppID/后台资格。

## 2. 桌面自动化：可见接口与实际结果

- 根代理和子代理工具元数据均可见 Windows MCP 的 State/App/Click/Drag/Move/Scroll/Shortcut/Type 等方法；没有 Unity 或 Blender 专用 MCP。浏览器截图和 Unreal MCP 不计为 Unity 控制能力。
- 子代理实际调用 `mcp__windows_mcp__Powershell_Tool` 成功，Status Code 0、isError=false；同通道确认本仓 AGENTS 与指定 Unity.exe 存在。其 AGENTS SHA256 与 exec 通道一致：`E5124B971A18B3DF25F57505100FF20CE8412A83B309AD796ECF1BED0F672A9C`。这是修改本轮 AGENTS 前的同工作区证据，后续文件修改会改变该哈希。
- Windows MCP 的窗口截图/输入、对本游戏 Player 的定位、焦点与操作尚未实测；PowerShell 成功只证明该服务的命令通道。
- 按 [computer-use 技能](C:/Users/Lenovo/.codex/plugins/cache/openai-bundled/computer-use/26.1002.52244/skills/computer-use/SKILL.md) 尝试在 `mcp__node_repl__js` 初始化 `@oai/sky`，两次均在 kernel 启动前退出，错误为 `windows sandbox failed: helper_unknown_error: setup refresh had errors`。未枚举其他应用、截图或输入。这是当前通道启动故障，不是自动审批拒绝，也不能推出插件不存在。
- CUA 自身的 native API 禁用只描述 CUA 通道，不覆盖另一个 Windows MCP。后续先核对实际可用 API，再针对本游戏唯一窗口验证；开发包的场景/日志/截图驱动是补充证据，不能替代真实输入检查。

## 3. Blender 基础制作与候选加载探针

探针运行于独立 background/factory-startup 进程，输出仅在 `.local/readiness-2026-10-09/`。工作规格：生成尺寸 `[1,1,1] m` 的立方体、显式 FBX/GLB、正交相机、Cycles CPU 8 samples、128×128 RGBA 预览，保存 `.blend`。命令退出码 0，各导出文件非空，预览已实际查看；PIL 检查 mode=RGBA、alpha 范围 0–255。

- `one-meter-cube.blend`：97135 B，SHA256 `df9b1d01426fcb20641aa759b102deb35960df5e0b75e6f1b8628ed653aa3933`。
- `one-meter-cube.fbx`：15308 B，SHA256 `0c2cd4b41d3720330b6a83e9351a3eb1d796eb5ceaf6005bd20ac256c80a3e5f`。
- `one-meter-cube.glb`：1996 B，SHA256 `85b130df9ab939dedea783467921dba28846778f60e478b08b637d60fe571433`。
- `one-meter-cube.png`：13365 B，SHA256 `060329f2c89119dbdea2566cbe7bdccb3f98fe4d2a99bdff9212bd5e91c45828`。

单项候选加载：将 [09 已登记 CC0 候选](../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md) 中的 `F:\workspace\github\world-of-claudecraft\public\models\props\dock_platform.glb` 加载到空 Blender，operator 返回 FINISHED，1 个 Mesh、616 顶点、1 材质。原文件 SHA256 `e8ddba57f493afb762d770c7808c45208300ed3949f724b88a9451987370c386`，操作后不变；没有保存或转移该候选。

这证明已安装 Blender 的基础建模/导出/渲染可运行，且该压缩 GLB 样件可载入；不能推广到所有 WoC 文件或材质正确性。未验证模型与作者外观的完整匹配、Unity FBX 往返、顶点色材质、正式镜头或游戏判定。上述方块不是游戏资产或美术定稿。

## 4. 音频与视频编码探针

- Python 标准库生成 1 秒、48000 Hz、单声道 16-bit PCM WAV（440 Hz 合成音与短淡入淡出）。WAV SHA256 `369c4ddfb5fc88f7e799ced6579989242aa3338f1668425c97c833d377184c28`。
- FFmpeg 将其编码为 Vorbis OGG，ffprobe 实查 codec=vorbis、48000 Hz、1 channel、1.000000 s。
- FFmpeg 将 1 秒合成灰色帧编码为 libx264 MP4，ffprobe 实查 codec=h264、320×180、30/1 fps、1.000000 s。
- 两项编码命令退出码 0。未生产曲目、环境循环或正式事件音，未证明音乐审美、循环接缝、混音、Unity 播放、Player 录屏及音画同步通过。

只读参考文件：[highschool/tools/generate_audio.py](F:/workspace/highschool/tools/generate_audio.py)、[tools/update_asset_provenance.py](F:/workspace/highschool/tools/update_asset_provenance.py)。前者含旧工程路径与工作目录清理，循环时长/接缝/全局随机源不直接满足本项目；后者按目录自动填“通过”且日期固定，不作为本游戏许可审核器。本轮没有执行这些外部脚本或复制曲目。

## 5. 核查用途与重做边界

来源方案与取舍见 [12 缺口登记](../../docs/design/first-batch/proto-013-ring-toss/full-game/12-development-readiness-and-gaps.md)。临时探针与日志受 ignore 排除；本登记备份实际规格、结果和哈希，正式生产时另建版本化源码/资源/导出与审核记录。首次开工仍必须用本游戏的 Unity 工程、测试与实际 Player 补证据，不能把这些工具探针记成游戏验收。

## 6. 准备检查的实际验证

两项仓库技能更新路由后再次执行 `quick_validate.py`，均为 `Skill is valid!`；根会话实际清单已列出两项并用于本轮审计，正式生产效果仍未验证。`check-docs.py` 的默认模式和本机 `--check-external` 模式均实际运行，Markdown 非空/UTF-8、本地文件引用和原工作簿 SHA256 无错误；默认模式确实跳过仓外绝对引用，不要求换机具备指定参考仓。

独立审阅发现并修复两处脚本问题：相对报告路径改为按仓库根解析；校验登记要求至少一项有效源文件。实际从 `F:\workspace` 调准备脚本，相对报告成功写入本仓 `.local/readiness-2026-10-09/readiness-path-regression.json`，没有写到同名仓外路径。文档检查的隔离样本“空登记、非法哈希、错误哈希”均返回 1，“正确哈希”返回 0。这些是准备工具的定向验证，不是游戏或存档故障验收。
