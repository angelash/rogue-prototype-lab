# 包装替换总账

全部记录从只含必要结构的来源快照与本版catalog派生，旧文本全文/源码/媒体不进入新项目。原作所有结构条目都有保留语义并原创替换、引擎重建、数据替换或明确退役的处置；完整覆盖不意味着复用原媒体。

- [18旧流程状态](state-flow.json) / [系统改动](system-decisions.json)：输入、设置、胜负、保存、语言、得分与新增规则逐项锁定。
- [95卡](cards.json)：源类型、时长、图片名到新卡/新图。
- [28项目](contracts.json)：原有效五值、Animation到新五值及客人，逐项记录修正理由。
- [420文本键](text-keys.json)：新原创中文键或明确退役；空键和REASON_-1共2项退役。
- [74资源键](resource-keys.json)：原共享图片到多张原创新图，UI/数据替换与退役。
- [1985对象](objects.json) / [CSV](objects.csv)：全部对象及345个非空导出所属对象逐项处置；空载荷/引擎默认资源也不漏账。
- [30业务类](code-roles.json)：原创新架构职责与替换接口。
- [40元类型](metadata-types.json)：30业务类职责和10个模块/嵌套/编译器类型的明确退役。

旧Texture/Sprite名字不能作为复用授权；每个targetAssetID都必须在本版assets存在。场景/Transform等按新slot锚点与uGUI重建，不假称把462个旧GameObject原样搬过来。字体/音轨全部新来源；3D内置网格、碰撞点选、联网和旧设置不采用。原5个XML统一替为JSON，字段决定写在新规则中。
