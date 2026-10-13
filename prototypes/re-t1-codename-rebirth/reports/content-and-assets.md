# 内容、场景与资产

2026-10-13。配置记录、序列化对象和逻辑地址分别计数。

## 配置与文字

两份 QCFG 有 208 表、30,676 条记录；misc 是 628 键 KV 表，其它 207 行表共 2362 列。池、行跨度及索引精确消费。local/config/tables 保留完整数据；映射使用 $map 的键类型与 entries，不静默改变整数键。[配置](../evidence/config-decoding.json)

实际表有 64 武器（219 字段/行）、77 弹药、147 配件、1025 通用物品、41 背包、961 容器、3217 容器用途、35 装备、7 角色、151 怪物、67 技能、74 Buff、100 部门任务、28 建筑、55 建筑等级、4 工作台和 7214 语言行。武器表包含射速、弹速、装填、部位系数、距离衰减、散布、后坐力和瞄准等；客户端值不证明服务器最终规则。

LangMgr 对非数字键执行特定 double/UTF-16 哈希后读取 data_language.content。已关联 8030 配置字符串、87 表，全部地图、角色、武器名称对应；任意字段匹配仍是候选，不证明实际 UI 可见。icon_conf 有 1795 个图标/图集映射。[关联](../evidence/content-linking.json)、[本机全文](../local/config/resolved-content.json)

task_action 的代码声明 format 没有对应列；example 类型没有表。原状保留，不修改第三方数据，也不据此宣称运行故障。

## 地图与内容范围

14 条地图名称为宿舍、新手关、新手操作教学、新手操作教学2004、进阶教学1/2、水城（地编看效果）、六号机场、靶场、机场外围（PVE）、容器测试场景、TA测试场景、水城（策划白模）、新手关测试。测试/白模不算正式上线地图。

14 份碰撞数据共 49,987 形状，保留分组、位置/旋转、盒/球/胶囊/柱参数及邻接 ID。还有 14 NavMeshData、5 TerrainData。碰撞、导航和地图表的 ID 集合不完全相同，不强行一一对应。[载荷](../evidence/payload-inspection.json)

表内还有 22 地图任务、74 任务步骤、108 任务规划、60 撤离点、162 对话、134 引导、199 引导阶段和 9 进阶教程。未由名称补写未见剧情或声称所有触发条件已运行。

## 对象与媒体

981 包有 191,092 对象，含 47,282 GameObject、39,031 MonoBehaviour、6867 Mesh、3793 Sprite、3491 Material、3204 Texture2D、1283 AnimationClip、728 AudioClip、603 SkinnedMeshRenderer、452 Animator、191 AnimatorController、150 Shader、108 SpriteAtlas。[全类型计数](../evidence/bundle-objects.json)

全部包内类型树通过。39,030 脚本组件对应 131 种脚本身份，另一个 m_Script 为零。语义 JSON 对大数值数组做摘要；完整顶点/像素/序列化值在解码包中，缩略 JSON 不等于全字节。[对象](../local/assets/objects-enriched.json)

local/assets/export 有 3201 纹理输出、6867 OBJ gzip、728 原编码音频及解码样本。3 个动态字体图集为空；17 浮点贴图用 RAW/NPY 保全值，PNG 归一化预览不可回用于数值贴图。OBJ 不保留完整蒙皮、形态键、Unity 材质/场景变换，原包仍保留这些信息。

57 FMOD RIFF 库约 27.4 MB，结构闭合；27 库的 333 样本已导出 WAV。可能与 AudioClip 重复，不相加为独立音效总数。未验证事件/混音运行或听感。[媒体](../evidence/media-export.json)、[音频库样本](../evidence/fmod-samples.json)

641 HCRH、48 SDF 保留原生渲染/体素资料，未转换完整编辑器场景。16499 DrawKeys 地址用于渲染组织，不能算物品或关卡。全部地址/依赖仍在[离线地址页](../local/explorer.html)。

## 原生文件与预览

487 图片后缀文件，初查 483 可识别。两个相同 PNG 仅首字节损坏，全部 chunk CRC 正确；另存首字节修复副本并验证，原件未动。另两份 SDK 载荷不是可直接打开的 PNG。36 MP4 容器/轨道通过，逻辑地址 38，未全帧/听检。

基础 7472 对象现有 7468 完整类型树；Driver、UniversalRenderPipelineGlobalSettings、GUISkin、BuildSettings 的兼容缺口单列。[补齐](../evidence/base-recovery.json)

抽样纹理可见拟人动物、武器/配件、背包/HUD 图标，但不代替 Android 实际画面验证。第三方字体和媒体许可没有因此变更；本仓原创游戏未采用这些资产。
