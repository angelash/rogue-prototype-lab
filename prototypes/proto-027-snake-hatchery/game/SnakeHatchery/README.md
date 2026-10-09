# 贪吃蛇孵化场：Unity首次可玩候选

版本：v0.2；日期：2026-10-10（Asia/Shanghai）。Unity2022.3.62f3c1，目标Windows x64普通Mono Strict/PC单机。当前Core/Runtime、保存适配与场景样件已完成，32/32 Unity测试通过，实际验证见 [开发记录](../../../../docs/development/proto-027/2026-10-10-slice-01.md)，不借其它项目成绩。

## 范围与权威

用户明确将027加入第一批，随后授权逐个开发；本轮助手采用首次可玩纵切，一图、一主蛇、三种身体节、两种公开简单分身行为、至多两分身。三维斜俯生态孵化盘为表现选择，离散占格/成长/能力/物料/冲突/行动与交付归Core。无分身需能完成，不扩成长科技树、敌群、软体或自主寻路。

- [项目规范](../../DEVELOPMENT.md)、[实施方案](../../../../docs/development/proto-027/implementation-plan.md)、[操作合同](../../../../docs/development/proto-027/input-and-ui.md)。
- 原依据：[需求v0.1](../../../../docs/design/first-batch/proto-027-snake-hatchery/requirements.md)、[设计v0.1](../../../../docs/design/first-batch/proto-027-snake-hatchery/design.md)，2026-10-09原稿及来源工作簿保持原样。
- [Rules.cs](Assets/SnakeHatchery/Core/Rules.cs) / [Session.cs](Assets/SnakeHatchery/Core/Session.cs) 已核对版本0.2.0/格式1、8×6图、80行动、总模块7/总货物8、三节/有限路线及完整API/事务。[Core README](Assets/SnakeHatchery/Core/README.md) 记录短/长同输入无分身52行动、Long容量优化28行动和分身/回收独立黄金预期，实际32/32 Unity报告已归档。表现/存储不能另建近似规则或复制货物/模块。

## 本地入口

在仓库根PowerShell使用已安装Unity：

```powershell
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-027-snake-hatchery/game/SnakeHatchery -Mode Prepare
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-027-snake-hatchery/game/SnakeHatchery -Mode Test
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-027-snake-hatchery/game/SnakeHatchery -Mode Build
```

Editor入口 `PrototypeBuild.Editor.ProjectBuilder.Prepare` / `BuildWindows` 已存在；Prepare只在不存在时创建Bootstrap场景，不覆盖已有场景，并确保 `Resources/RuntimeDefault.mat` 的Standard shader引用。构建使用Mono/StrictMode、无Development调试选项；完整Player目录位于 `Builds/Windows64/`，不是仅复制 `SnakeHatchery.exe`。

日志/XML位于仓库 `.local/unity/SnakeHatchery-<时间>-<模式>/`；Prepare成功，32/32 Unity测试通过，最终普通Windows构建86,888,473字节0错0警告。实际证据可由 `scripts/record-project-evidence.py --project 027 --tests <真实XML> --capture <真实图>` 归档，当前真实报告见开发记录的证据链接。隔离相机诊断只证明镜头，不证明正常窗口/HUD/键鼠/听感。

## 操作、保存与资产

已只读核对 [SnakeRuntime.cs](Assets/SnakeHatchery/Presentation/SnakeRuntime.cs) 实际绑定，原生验收未执行：

- WASD/箭头单步移动；Space等待，E采当前格货，Enter交付；移动会吃地面模块成长，撞墙/身体结束本轮。
- Z/X选保留节数，C巡线/V往返打开切尾预告，Enter/按钮确认、Esc取消；鼠标点身体后段选切点。
- 1/2按稳定ID排序选分身，鼠标点分身头选择，Q打开回收去向预告，Enter/按钮确认、Esc取消。
- G取相邻/自身地面模块进有限库存，按距离/ID选；B放库存最低ID在头格，不免费装身。
- F6保存/F9恢复/R同预置重试，H最近事件（Y含路线）、Tab帮助、M音乐、F5窗口截图、Esc确认保存退出。

无自动步进；纯UI选择不收费或推进分身，每个有效输入最多提交单个事务。C/V不直接断尾、Q不直接回收；帮助/复盘/切尾/回收确认/退出面板阻挡普通命令，F9/Rreturn且状态替换共用帧门禁。四向MovePreview已提示可走/成长/碰撞，主蛇直接反向含长度2均碰撞失败；其它旧尾格不成长时可入，成长时不腾位。实际焦点、连按、菜单、动态预告及保存边界仍待测试/原生核对。

完整安全点包括有序主蛇/分身节、各自位置/方向/能力归属、路线/任务进度/货物、地面模块/货物、库存、行动/交付/阶段和命令/事件去重。Core `ExportSnapshot` / 静态 `TryRestore` / `ValidateSnapshot` 已实现范围/守恒与完整生产命令重放比较，恢复LastEvents清空不额外采货/孵化/交付；与磁盘payload/SHA/合法备份分开。坏档不覆盖唯一合法旧档，Save失败保持内存/窗口。本项目4项RoundSnapshot磁盘故障已随32项Unity测试通过，不借051结果。

三维生态盘采用原创C#程序样件，[SnakeSceneView.cs](Assets/SnakeHatchery/Presentation/SnakeSceneView.cs) 已接入并初态目检，格距.84m、底面Y.17，模块ID/归属、货物、实线路径/虚线草案和真切点读Core。来源登记已归档并核对脚本/初态图哈希；本轮不假称Blender/FBX/生图已制作。字体/七本仓原创短音/独立原创BGM九份副本见 [运行资源登记](../../../../sources/art/proto-027-snake-hatchery/runtime-resource-register.json)，九副本同字节及字体声明随包已核对，听感待验，无配音服务。

当前Prepare成功、32/32 Unity测试、普通Mono构建0错0警告及初态相机已核对；正常窗口未验。既有系统安全权限modal阻原生阶段，工具不代操作；独立制作和验证继续，正常窗口/原生/听感欠证保留。候选备份不等于完整纵切或Steam发行完成。
