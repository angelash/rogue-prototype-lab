# 资产组织、场景与媒体

本机 Steam 2.3.30；[容器清单](../evidence/containers.json)、[对象摘要](../evidence/asset-summary.json)、[媒体结果](../evidence/media-summary.json)、[音频结果](../evidence/audio-summary.json)。完整原件与导出在本机 `local/`，不进入公开远程仓库。

## 文件组织与对象身份

安装共 466 文件，Managed 保存 Mono DLL，Plugins/x86_64 保存 FMOD、Burst、Resonance 与 Steam 等原生库；Unity 主资源位于 globalgamemanagers、resources.assets、sharedassets 与 level0–62，相关 .resS 等流数据也完整保留。StreamingAssets 提供语言 CSV、FMOD bank、独立音乐 MP3、配置文本与 aa 设置。

本构建 Addressables catalog 的 InternalIds、ProviderIds、ResourceTypes 均为空；主要内容可以从 Resources 注册表、构建场景和共享资源追踪。这个观察只适用于本输入，不能据此概括所有 Duckov 版本。

SQLite `local/assets.sqlite` 包含 containers、objects、edges、resources、exports、media_status：对象用 container:pathID 定位，记录文件字节偏移/长度/SHA256、类型、名字、脚本、解析状态与压缩 JSON。引用记录源字段、目标对象及是否存在；导出记录本机相对路径、长度和 SHA256。独立核验从 copied build 对象字节区间重新计算哈希。

resources 表有 435 行，包含空引用、共享 shader/subasset 的重复键，不是 435 个独立内容文件。引用列表省略 pathID 0；对象的“被引用”界面最多显示 80 条，数据库保存全部。

## 场景和渲染资源

全对象包括 GameObject 439,639、Transform 423,442、MeshFilter 188,371、MeshRenderer 188,124、BoxCollider 179,926、MonoBehaviour 97,426；高数量主要反映场景实例和依赖，不等于独立制作资产数量。

独立媒体/效果类型包括 Mesh 3,900、Texture2D 4,421、Sprite 1,371、Material 2,879、Shader 369、ParticleSystem 3,182、AnimationClip 238、Animator 364、SkinnedMeshRenderer 196、Avatar 40、AnimatorController 37、Cubemap 59、Texture3D 4、TerrainData 18、PlayableDirector 14 等。它们的配置和引用均可查；没有把所有类型都转换成作者可编辑格式。

63 个构建场景中存在菜单、基地、过场、各地图主/子场景和加载入口。GameObject/Transform 层级、材质、相机、LightmapSettings、Terrain 与场景脚本保留在对象数据库，完整场景原载荷留在副本。未创建可重建并运行的 Unity 工程，未恢复原 Prefab GUID、原文件夹结构、导入设置、编辑器脚本与丢失制作资源。

## 已导出的格式与限制

- Texture2D：4,405 个有载荷对象成功解码，16 个为空载荷；PNG 是渲染预览。7 个 BC6H 纹理另存原始 `.bc6` 压缩数据，PNG 为 8 位预览，不能保留完整 HDR 精度。
- Sprite：1,371 个成功导出 PNG，按 Sprite 渲染信息定位图集；尺寸、pivot、边框与原 texture 引用仍在对象树。
- Mesh：3,900 个成功导出 OBJ，包含可查看几何。骨骼权重、BlendShape、完整动画/材质绑定仍在 Unity 原载荷/配置；OBJ 不等于完整 FBX。
- TextAsset：87 个导出。文本可能是行为图、表或工具数据，不保证均为自然语言；原对象仍有准确来源。
- Font：12 个有文件载荷对象导出，1 个为空载荷动态字体对象；没有把空载荷当作解码成功。
- Shader：362 个生成编译表示文本，7 个不能被当前工具展开，改为精确保留 `.shaderbin` 原对象载荷。编译表示不等于作者 Shader 源文件。

上述对象共 10,161；因空对象无文件、BC6H 多产物，最终生成 10,151 个文件。每个导出有哈希，PNG/WAV/OBJ 另做格式结构检查。AnimationClip、Avatar、AnimatorController、粒子、材质、光照、Cubemap、Terrain 和 Texture3D 等已索引、保留原件，未额外做动画/地形工程化导出。核验没有宣称全素材的主观美术质量通过。

## FMOD 音频

Unity 容器没有 AudioClip 对象；主要音频在 `StreamingAssets/Master.bank`（57,688,640 字节）与 `Master.strings.bank`（19,344 字节），另有 `Music/Xavier Wu - Duck Funk.mp3`。直接银行读取使用副本中的 fmodstudio.dll，输出设置 NOSOUND，没有创建/播放事件。

读出 373 个事件路径及长度、参数数量、3D/一次性/快照标志；字符串表保留 GUID 与路径。Master.bank 内偏移 629,088 的 FSB5 v1 保存为 `local/Master.samples.fsb`，声明 568 样本，实际解码 568 WAV，各自保存采样率、声道、帧数、时长、长度和哈希。

样本序号/文件名不是原音频工作站文件名，事件也不一定对应单个样本。FMOD Studio 的参数曲线、事件分层、随机选样、混音和事件到样本映射尚未完整重建。独立 MP3 原文件已完整复制；没有额外转码，也没有执行主观听感验收。

## 两个仍未恢复完整字段的对象

`level1:7554` 的 UnityEngine.Splines.SplineExtrude 遇到 managed SerializeReference 参考类型节点解析限制；`resources.assets:76733` 的 Shapes.ShapesImportState 需要构建内未包含的 ShapesEditor.dll 类型布局。均保留基础字段、脚本身份和精确原始字节，详情与错误在 [mono-repair.json](../evidence/mono-repair.json)。主要业务库、配方、建筑和总配置已经恢复。
