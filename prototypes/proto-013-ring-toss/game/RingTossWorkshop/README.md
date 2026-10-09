# 套圈改造摊 Unity P0 工程

文档版本：v0.6，2026-10-09；工程产品版本沿现有配置 `0.1.0-prototype`。用户已授权 013 的 Unity 实现、素材接入、测试和 Windows 构建，并已明确真实三维斜视地摊方向。本工程使用现有 Unity **2022.3.62f3c1**，编辑器路径为 `D:\Program files\2022.3.62f3c1\Editor\Unity.exe`，构建目标为 Windows x64 Mono。

本轮事实、测试报告和后续接续点统一见 [2026-10-09 P0 开发记录](../../../../docs/development/proto-013/2026-10-09-p0.md)。48/48 EditMode 通过（旧二维30项＋新增三维18项），Windows x64 Mono 已构建；构建精确大小、版本和后续结果只在该记录维护。旧二维窗口 QA 保留为历史；当前桌面观察到锁屏，三维正常键鼠 QA 尚未通过，自动镜头图不代替真实输入/画面/声音验收。

## 当前方向与迭代范围

- 无风单摊、普通圈、六槽；目标回款 105，基础八圈、两次调整。
- 真实三维场景与固定后上方斜视透视镜头；玩家近处持圈向前抛，六奖品在地面两排三列排列，无自由漫游。
- 左右方位、仰角和力度独立控制；固定步三维轨迹、奖品接受体、风扇/反弹板接触与视图共用锚点，没有隐藏二维中奖平面。
- 套中奖品后的兑现或留场，留场后成为原槽机关；移动/交换、转向、开关与拆卸经同一规则层处理。
- 一次补救购买、阶段结算、上一投回放、暂停、0.5 倍速度和截图入口。
- 首组采用暖灯、条纹棚、帆布摊地、暖色低模旧物与轻覆盖 UI；主场景真实三维，二维人物头像可留给后续对白。
- 中文字体与七个原创程序音效：`launch`、`hit`、`bounce`、`cash`、`retain`、`miss`、`ui_confirm`，音频副本位于 `Assets/RingToss/Resources/Audio/`。首组真实三维模型已由 Blender 生成并接入 `Resources/Models`；源/FBX/采用项见 [原创素材登记](../../../../sources/art/proto-013-ring-toss/3d-p0/README.md)，实际画面/轴向和质量结果见开发记录。

以上是首组单摊迭代范围，不预填已经全部完成或验收成功。方向详见 [三维场景合同](../../../../docs/development/proto-013/3d-scene-direction.md)，用户决定的是三维斜视前抛；具体相机参数、造型和几何实现仍为助手建议。旧固定正交 2.5D 路线已替换。本轮不含完整正式皮肤映射/人物与三章资产、完整关卡/剧情、存档恢复、跨摊或局外成长、其他圈与机关、手柄完整支持、Steam SDK 或上架材料，完整计划见 [设计入口](../../../../docs/design/first-batch/proto-013-ring-toss/full-game/README.md)。

world-of-claudecraft 的 14 项音频未确认可商用，未复制进本工程，见 [复用登记](../../../../sources/references/woc-audio-reuse-register.md)。live-avatar 仅作为 TTS 架构参考，未把接口存在或本机能力推定为配音生产已通过，见 [能力登记](../../../../sources/references/live-avatar-audio-capability-register.md)。

## 准备、测试与构建

在仓库根目录 `F:\workspace\rogue-prototype-lab` 运行以下已有脚本。脚本核对编辑器版本，分别为每次调用创建 `.local/unity/<时间>-<模式>/unity.log`；这些命令是复现入口，实际运行结果以开发记录为准。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-unity-prototype.ps1 -Mode Build
```

`Prepare` 调用 `RingToss.Editor.ProjectBuilder.Prepare`，仅在缺失时建立 `Assets/RingToss/Scenes/Bootstrap.unity`，设置构建场景、窗口尺寸和 Mono。`Test` 运行 Unity EditMode 测试，检查退出码与生成的 `tests.xml`；未生成报告、没有测试或报告非通过状态均会失败。`Build` 调用 `BuildWindows`，先执行 Prepare，再产生开发构建，检查 BuildReport 并写入 `Builds/Windows64/build-summary.json`。

构建成功后，Player 入口是 `Builds/Windows64/RingTossWorkshop.exe`，默认窗口为 1280×720。工程也可用指定编辑器打开，执行菜单 `Ring Toss > Prepare Prototype` 后打开 Bootstrap 场景进入 Play。不要同时让多个 Unity 进程写入同一工程。

源码按职责放在 `Assets/RingToss/Core/`、`Application/`、`Presentation/`、`Editor/` 与 `Tests/EditMode/`。P0 的三维视图读取 Core 位置和几何锚点，经济状态由 Application 驱动；Unity 的画面帧率、装饰模型和界面动画不替代判定。

## 操作

以下映射来自本轮现有输入源码；实际 Player 的完整输入验收仍见开发记录。

侧栏默认折叠，Tab或“选槽 / 改造”按钮展开；选择物件可打开工具，中奖时显示处分。侧栏中的补圈、结束与回放等动作可展开后操作。

1. 点击开摊按钮或按 Enter 开摊。鼠标滑条分别设置左右方位、仰角和力度；←/→调整左右方位，↑/↓调整仰角，Q/E减小/增加力度。点击抛圈按钮或按 Space 向前投掷。
2. 套中后点击“兑现”或按 C，或点击“留场”或按 R；先完成处分再投下一圈。
3. 点击场中物件/槽位或按 1–6 选槽。选中留场机关后，Shift+1–6 移到空槽或与目标槽的另一件机关交换；T 或转向按钮打开朝向预选框，X 拆卸。机关开关使用按钮，E 现在是力度增加键。
4. 朝向预选框内，风扇可选 -30°、0°、30°、150°、180°、210°；反弹板用滑条选择 15°–165°，每 5° 一档。点击“确认转向”才提交一次转向，点击“取消”不提交；预选本身不消耗调整，框内没有 Enter/Esc 确认或取消快捷键。
5. “补2圈 / 18”按钮购买本摊一次补救圈；“结束本摊”按钮或开摊后的 Enter 请求结算，由阶段与回款规则决定是否允许。
6. “上一投回放”复现已完成投掷，不发奖、不扣圈或耐久；P/暂停按钮切换暂停，“速度 1×/0.5×”按钮改变展示速度。F2/“重开测试”在正式投掷飞行阶段以外重开单摊。

转向框打开时其他 UI 操作禁用。投掷、待处分、调整预算与结算的合法性由规则层校验，错误操作会显示原因。

F5 或“保存画面”按钮截图，默认写到 `UnityEngine.Application.persistentDataPath/Screenshots/`。启动 Player 时可传 `-ringEvidence <绝对目录>` 指定截图目录；日志输出 `RING_SCREENSHOT` 路径。截图只记录画面，不代表该输入路径、三维判定或音频 QA 已通过。

开发视觉检查可额外传 `-ringCaptureOnce`：该入口不发开摊/投掷等玩法命令，用相机与 RenderTexture 输出隔离场景图。它不包含 IMGUI 覆盖层，也不检查正常桌面键鼠。隔离相机图已生成并目检，采用图和版本见开发记录；首次锁屏下 ScreenCapture 黑屏仍保留其失败结论，不能把后续镜头图称为真实输入验收。
