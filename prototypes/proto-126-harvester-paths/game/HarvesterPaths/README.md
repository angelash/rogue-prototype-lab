# HarvesterPaths / 收割机自己铺路

版本：0.2.0-slice；日期：2026-10-10。Unity2022.3.62f3c1/revision1623fc0bbb97，Windows x64 Mono PC单机。Prepare、39/39正式测试（35规则+4存储）、最终普通Mono构建0错0警告及标签修正后初态相机已核对，证据见[实际记录](../../../../docs/development/proto-126/2026-10-10-slice-01.md)；原生/动态/完整HUD/听感与HRV-H01–03未验，不宣称完整纵切或Steam发行。

## 作品与规则

助手选择“清晨试验田”原创三维样件表现二维离散10×10格，保留原v0.1方案。三地块Crop/Ground/Mud，仓库(1,1)是功能标记；10作物每格一次粮1秸1，共享车箱，收后/铺路地面跨三轮保留。三模块滚轮/宽头/箱体加None基础，当前目标3/3/4、预算24/32/52、初油/上限24、容量6、钱4是助手初值。

行为与资源来自Core Rules/Session单一真值，Runtime/SceneView不靠动画/帧率算判定。[项目规范](../../DEVELOPMENT.md)、[实施合同](../../../../docs/development/proto-126/implementation-plan.md)、[当前操作](../../../../docs/development/proto-126/input-and-ui.md)、[实际记录](../../../../docs/development/proto-126/2026-10-10-slice-01.md)分别维护方法、有限规则、绑定和证据。

达标前部分交粮只移出真实车粮、付价2/单位；补油花2钱/1行动，不加截止。车载2秸转最多6油或铺相邻Mud，互斥扣料。前两轮达标仅旧预算仓库卸/补，显式Continue锁下一模块/新预算但延续全部地图/货/油/钱；warehouseStraw只入库、不取回/远程使用。最后有效交付先目标再截止，第三轮达标/任一轮失败结束，Retry整局回初始。

## 开发命令

仓库根由协调者统一运行：

```powershell
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-126-harvester-paths/game/HarvesterPaths -Mode Prepare
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-126-harvester-paths/game/HarvesterPaths -Mode Test
./scripts/run-unity-project.ps1 -ProjectPath prototypes/proto-126-harvester-paths/game/HarvesterPaths -Mode Build
```

[Editor方法](Assets/HarvesterPaths/Editor/ProjectBuilder.cs)为`PrototypeBuild.Editor.ProjectBuilder`；Prepare仅缺Bootstrap才建场景，保留Standard运行材质/1280×720窗口/Mono。Build输出普通Strict完整`Builds/Windows64/HarvesterPaths.exe`及Data/Mono等目录/build-summary，不只交exe。`.local/`与Builds排除Git，真实日志/XML/包manifest由实际记录归档。

## 当前实际绑定与安全点

[HarvesterRuntime.cs](Assets/HarvesterPaths/Presentation/HarvesterRuntime.cs)已只读核对：W/上前进、Q/E转向，Space收割/C转油；IJKL或鼠标选格/P铺路，Z/X调整数量1..Rules.BoxCapacity（10），Enter交粮/U卸秸/F补油。1–4预选None/滚轮/宽头/箱体，只“新三轮”按钮或合法N生效，R按InitialModule重试。

F6键盘保存后return，F9/R先Claim并return，与领域Execute/新局/轮切共享门禁；H最近10事件/Y过滤或包含Moved/Turned，Tab帮助、M音乐、F5保存persistentDataPath/player-window.png，Esc退出确认，保存失败留窗。源码绑定不是原生键鼠通过。

完整快照保存地图/车位方向/模块、车粮秸/库秸/油钱、轮目标/进度/预算/阶段、累计守恒/命令ID/事件；每成功新事务/轮切保存，恢复领域校验与生产命令重放，坏主档回有效备份，保存退出失败留窗。4项磁盘故障、重复/未知版本与伪造回放已通过正式Unity定向验证，不承诺硬件断电保证或Cloud。

## 来源与边界

[九运行资源登记](../../../../sources/art/proto-126-harvester-paths/runtime-resource-register.json)保留七本仓原创短音、126原创BGM候选、Noto CJK2.004及完整OFL。原创C#场景源与配方已归档；没有Blender/生图已采用声明。副本hash/解码、相机初态、完整动态/HUD、真实听感与native分别验；已有系统权限modal只阻正常桌面阶段。

基础None三轮黄金终账预期余22行动/油4/钱22，交粮10/仓秸6/车秸2/转换耗秸2，已通过本项目正式Unity生产路径测试；不证明全部模块组合或玩家H01–03。手柄/重映射、性能低配、完整音画/随机成长/Steam未做；本项事实/证据已归档，单项Git备份按总进度实际登记。
