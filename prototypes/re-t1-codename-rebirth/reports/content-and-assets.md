# 内容与资产组织

2026-10-13。完整地址/包依赖在[本地检索](../local/explorer.html)和[JSON](../local/manifest-assets.json)，原始字节位于 local/apk。数量来自清单或可读基础文件，不是正式玩家内容清单。

## 清单与资源包

普通清单字段 FileVersion 1.4.6、PackageName DefaultPackage、内部 PackageVersion `0`、OutputNameStyle `4`。文件名中的 `73317` 与内部版本不同，不推断构建关系。2,669,735 字节全部读取；MXET 扩展清单版本 1，22,162 地址逐项对齐且无剩余字节，附加整数含义未确定。

1,017 文件共 1,216,598,628 字节，地址/依赖/文件大小全部闭合。981 非原生文件加载方法 3、非标准 UnityFS 头，未解析；36 原生文件加载方法 0、均为独立 MP4。另外一个不在此清单中的标准 firstbundle 已与基础文件读取。[摘要](../evidence/manifest-summary.json)

普通字段顺序参考官方 [YooAsset 1.4.6-preview PatchManifestTools](https://github.com/tuyoogame/YooAsset/blob/1.4.6-preview/Assets/YooAsset/Runtime/PatchSystem/PatchManifestTools.cs) 和 [BufferReader](https://github.com/tuyoogame/YooAsset/blob/1.4.6-preview/Assets/YooAsset/Runtime/Utility/BufferReader.cs)。目标的 Package 命名和 MXET 是差异；通过字节消费、地址对齐、ID/文件大小/摘要验证约束兼容性。

## 系统与数值线索

UI MainAsset/Prefabs 841 地址：Common 149、Main 130、Bag 54、Notify 50、战备 40、WeaponMod 32、Department 29、Map 28、Chat/Team 各 20、Settings 19、Hud 16、训练场 15、Homeland 12、CustomRoom 11、Evacuation/Login 各 9 等。[目录](../evidence/content-summary.json)

这些目录与脚本支持进入/角色、模式匹配、战备、地图战斗、战利品背包、撤离结算、仓库扩容、改装、建筑、制造、部门任务商店、邮件活动、语音组队及回放的组织。完整循环、跳转条件、消耗、失败损失和奖励公式没有方法体/表行证据，不由名称补写。

209 个 data_* 表类型涵盖武器/子弹/配件、背包/安全容器、地图/撤离/钥匙、技能/Buff、AI/怪物、家园/制造、任务、商店/充值、教程/语言/声音。[全表名](../local/config-table-types.json)。config_byte_0/1 仍在封装中，行数、字段 schema、本地化全文未知。

## 场景与角色资产

Scene/MainAssets 1975：Materials 867、3000Battle 573、scene_model 386、2000Play 113、4000UI 34、1000Town 2。客户端碰撞/导航各 14 地址；碰撞 ID 为 1001、2001、2002、2004、2101、3001、3004、4003、9990004、9990005、9990006、9990007、9990008、9990010，其中包含测试/专用候选，不能计为 14 正式关卡。

char/MainAssets 502：Weapon 347、Attachment 62、Equipment 55、Character 22、Monster 11、SceneModel 5；它们包含多种派生资源，不能计为 347 把枪或 22 位角色。渲染 DrawKeys 16,499 占多数地址，16603 个 .asset 后缀主要来自此处。[所有后缀和路径分组](../evidence/manifest-summary.json)

## 媒体与基础对象

487 图像后缀文件中 483 Pillow 可识别、4 失败；主要含平台/SDK UI 与图标，不是主要游戏美术全量导出。[图片](../evidence/raw-images.json)

字体路径出现方正、汉仪和 `MFYuanHei_Noncommercial-Regular.otf`；没有核对其许可或取得再分发授权，文件名不是授权文本，本仓原创游戏未采用。

728 个 .mp3 地址、57 个 FMOD 目录地址未解码载荷。36 独立 MP4 的 FFprobe 容器/轨道检查通过，逻辑视频地址共 38；有入战/撤离、车厢、终端/油库/雷达站/车站、探身/站射/引导命名线索，未据名称编写剧情、未全帧解码或听检。[视频](../evidence/raw-videos.json)

基础文件与 firstbundle：146 容器 / 7472 对象，包括 MonoScript 4352、MonoBehaviour 1030、GameObject 654、RectTransform 644、Texture2D 95、Shader 85、Sprite 35、Material 29、Mesh 15。6501 完整类型树成功，970 脚本组件仅基类、1 BuildSettings 不兼容；这不是 981 未解析包中的对象总数。[范围](../evidence/readable-asset-summary.json)、[失败](../local/readable-errors.json)
