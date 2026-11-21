# 121《磁铁拾荒者 · 夜班》Windows 交付记录

2026-11-21。根据用户授权，助手选定系列原案第 121 项，交付可离线从第一单玩到交班的 Windows x64 解谜游戏。五单的新规则、旧方案的问题和保留的取舍见[设计复核](../../design/first-batch/proto-121-magnet-scavenger/night-shift.md)。

## 交付物

- 版本 1.0.0，Unity 2022.3.62f3c1（1623fc0bbb97），Windows x64 Mono，普通非开发、StrictMode 构建。编辑器使用本机 Windows 非开发 Mono 模块。
- [本机完整压缩包](../../../prototypes/proto-121-magnet-scavenger/Builds/MagnetScavenger-NightShift-1.0-Win64.zip)内含 133 个文件，包括运行程序、Mono、Data、字体许可与中文开工说明。压缩包 41,001,439 字节，SHA-256 为 `a6240d7346362f35e7295405f880b9e9f739acb58677d73cb7ecc77de4258cde`。已读取 ZIP 校验码、在全新目录解压，对全部 133 项逐一比对 SHA-256，并从解压目录启动。
- [构建、源码及完整包清单](night-shift-evidence/release.json)记录 104 项 Unity 源文件/配置的 Git blob 与每个发行文件的 SHA-256。BuildReport 汇总 87,654,875 字节，0 错误、0 警告；报告完成于 07:45:24 UTC。本机日志为 `.local/magnet-night-20261121/release-build.log`，重建入口在[工程首页](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/README.md)。

## 规则与状态验证

Unity EditMode **66/66** 通过。32 项为本次重做所用的断言用例，包括 11 项规则、16 项关卡与提示、5 项进度与存储；其余 34 项是旧实现保留的回归。[Unity 测试 XML](night-shift-evidence/editmode.xml)

五关各有一条手工路线，分别用 3、5、11、6、10 步交齐。用生产命令复演，并和广度优先最短解交叉核对。另对箱内零件、链序、吊车位置和余电组成的 1,066 个可达状态独立递归推演，其中 249 个可从当下继续交齐（含已交齐）。提示对每个状态的判断和建议后继都通过。正式提示有 2,000 节点和 75 毫秒上限，若超限会明确提示；全状态回归放宽时间上限，以隔离硬件差异。[生产提示](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/Assets/MagnetScavenger/Core/ShiftAdvice.cs) · [关卡回归](../../../prototypes/proto-121-magnet-scavenger/game/MagnetScavenger/Assets/MagnetScavenger/Tests/ShiftLevelsTests.cs)

存档覆盖通关撤回后保留解锁、越级记录拒绝、损坏主档回退有效备份，以及已交货物、挂载顺序与静音设置的磁盘恢复。正式档与桌面验收使用的练习档分开。旧三维原型的存档继续保存在旧产品目录。

## 最终画面与操作

初次窗口检查暴露三处具体问题：吊车压住当前列号、小号中文模拟粗体糊在一起、唯一目标已在链尾时仍强调磁线被挡。最终包将列号上移，小字恢复常规字重；当剩余订单件已全部吸起时，按钮和工单提示一起切换到卸货。玩家尚需留着订单件去取另一件时继续显示真实吸取预览。[长杆够到井底](night-shift-evidence/shaft-aim.png) · [重芯上钩待交货](night-shift-evidence/delivery.png)

终单的初次动态检查还发现第二个弯头的图标仍朝右。最终包按来向旋转连接件，在 1280×720 真实画面核对了左侧接入、向上出线，并从保存的终单现场完成交班。[双弯头取芯](night-shift-evidence/blind-pocket.png) · [五单交班页](night-shift-evidence/shift-complete.png)

Windows 真实 Player 通过 Direct3D 11 启动。随后在 960×540 用干净练习档从零开始，鼠标吸起首件，键盘逐单完成并打开交班单。五单实机动作串依次为 PPU、PRPPU、PPUURRRRRPU、PRRPUU、PRRPPPUUUU（P 吸、U 卸、L/R 横移）。再回首单耗尽八格电，零电失败后撤销，退出时核对磁盘现存七步、静音开启、五单个人纪录保留。窗口有一部分被另一应用遮挡，F5 直接保存含 HUD 的游戏帧。测试使用独立验收档。[小窗口新档首屏](night-shift-evidence/start-960.png) · [零电后可撤销](night-shift-evidence/out-of-power.png) · [最终包真实输入事件](night-shift-evidence/player-actions.log)

本轮完成范围是五单固定谜题、中文键鼠、提示与撤销、保存、个人步数纪录和离线包。未招募首次玩家评估学习曲线或重玩意愿，未进行耳机主观听感、低配和 Steam 平台验收。
