# 内容、资产与派生格式

日期：2026-10-14。原始内容全文和媒体仅本地保存，统计来自实际配置/对象。

## 配置与文本

config_0 原 JSON 779,350 字节，SHA256 `0ff4e4dec18b6461d995307a629bb14091308e57b4586da0701ca7a0e69fa4ff`。按 GameCore.Config.JsonConfigDataSource 的 QConfig@v2 语义展开 8,257 对象池引用；`805306368` 这样的索引没有被当作数值内容。

[150 表项](../local/content/tables)、[schema](../local/content/schemas.json)、[池引用](../local/content/pool-resolution.json)分别保留：146 行表、10,495 行、1,447 列、2 KV 表/130 项。atom_event_history/gift_event_check 只有空对象、缺数组表头，运行读取返回无行；175/43 原空对象保留为 2 占位，不虚增有效记录。

主要条目：37 单位、1,045 星级/等级组合、9 指挥官、17 羁绊、42 变体、197 技能、148 技能动作、87 Buff、414 商店商品、100 普通物品、69 UI、46 引导、5 教程轮次、21 教程步骤。没有实际可达性/运营开关验证，数量不是全部内容可玩证明。

语言 2,399 行；[1,022 配置中文关联](../local/content/language-links.json)保留原键/hash，[展示行](../local/content/display-rows.json)只增加译名，不覆盖原数据。

## 清单、对象与引用

YooAsset 二进制清单 233,056 字节全部消费，扩展清单 1,438 地址对齐且无余字节。484 清单包每包 MD5/CRC/大小通过，依赖全部本地可用；另一个包为启动资源。1,438 地址全部[关联到对象](../local/content/assets-linked.json)。构建逻辑 `.prefab/.png/.mp3` 路径不等于那些作者源文件的字节恢复。

561 序列化文件有 59,312 对象，包括 15,920 GameObject、8,938 MonoBehaviour、8,673 RectTransform、7,247 Transform、5,977 CanvasRenderer、4,227 MonoScript。当前 59,311 类型树通过，1 GameSkin 保留缺口；235,902 已解析 PPtr 引用有库存目标。

全量[对象](../local/assets/objects.json)、[容器](../local/assets/containers.json)、[引用](../local/assets/edges.json)、[类型树](../local/assets/trees)可检索。生成器 player 根对齐/string[] 派发问题已针对标准头和原字节修正；并以真实声明/同包内嵌树、完整消费及脚本身份核对验证，不用空字段吞错。

## 媒体

[媒体索引](../local/media/index.json)登记来源对象、导出文件、尺寸/采样信息及 SHA256；3,388 对象全处理、无失败。

- 1,501 Texture2D、1,004 Sprite 为 PNG，Pillow 检查通过；3 空运行时纹理未伪造图像。
- 230 Mesh 为 OBJ gzip，原 bundle/树保留完整蒙皮、blend shape、材质和变换。
- 190 AnimationClip、114 Avatar 单独保存序列化字节和完整树，不宣称已得到作者 FBX。
- 122 Shader、6 ComputeShader 保留对象字节，可读 shader 导出的平台/工具限制逐项登记。
- 3 Font 保留序列化字节及原容器，不自动视为获准复用的字体制作源。
- 215 AudioClip 保存原编码与可解码样本；没有真实听感、混音、循环接缝或实机播放验证。

[代表接触图](../local/media/contact-sheet.png)已目检，UI 数字和属性提示字形/alpha 可辨；只是导出样本，不是游戏截图或全部 UI/模型验收。

## 原始数据

92 TextAsset 全保存在[原字节目录](../local/assets/text)，[载体索引](../local/content/payload-index.json)含 21 PE、24 JSON、24 SDF 块、12 A* 块、5 A* 包装/索引、3 FMOD RIFF、3 其他二进制。

SDF 的 5×Int16 头/16-bit 网格尺寸与字节数匹配；scale/物理单位、A* 包装值和 native 路径算法分别留边界。旧世界图/雾/空间数据不因存在就推成当前自走棋功能。战斗一致性清单有 634 个唯一条目，其自述原制作输入 hash 没有被当作已独立校验作者全源目录。
