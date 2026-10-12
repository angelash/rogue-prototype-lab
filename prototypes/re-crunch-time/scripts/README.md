# 分析工具与复跑

本项目工具只读安装目录，写入项目自己的 `local/` 与 `evidence/`。版本和预期数量固定在本次 Steam 构建 484621；升级后应另建版本目录、核对差异，不强行修改断言掩盖变化。

## 已使用环境

- Python 3.10.8：`C:/Python310/python.exe`；UnityPy 1.25.0、Pillow 11.1.0、fontTools 4.58.0。原有库未做全局升级。
- .NET SDK 9.0.309；本仓原创 `metadata-index` 使用 `System.Reflection.Metadata` 读取 PE/IL，既不执行程序集也不调用游戏入口。
- ILSpy CLI 9.1.0.7988：`local/tools/ilspy/`，安装于项目忽略目录，开启运行时 roll-forward。
- TypeTreeGeneratorAPI 0.0.10：`local/tools/python/`，仅本项目导入路径。
- FFprobe/FFmpeg 8.0.1：检查音频编码、时长并完整解码到空输出；不以解码成功宣称听感通过。
- Playwright CLI：本地内容浏览器的功能和页面检查；不是游戏实机测试。

## 完整复跑

```powershell
& ./prototypes/re-crunch-time/scripts/run-analysis.ps1
```

脚本只在副本不存在时复制原安装。已有副本必须通过逐文件 SHA256 校验；不会先删除或覆盖。需要已具备 Python 的 UnityPy 1.25.0、Pillow、fontTools，以及 dotnet、FFprobe、FFmpeg；缺依赖时报错。ILSpy 和 TypeTreeGenerator 仅在项目目录缺失时下载指定版本。其他平台可直接使用下列 Python 工具，但安装源路径需调整。

- `analyze_assets.py --source <安装> --copy <项目/local/build-copy> --project <项目>`：25 文件哈希、1985 原始对象/字段、4443 依赖、场景/设置、媒体导出。
- `metadata-index/MetadataIndex.csproj`：完整符号/token/签名、方法体指令、调用/字段引用、字符串。游戏及五个支持库的原始完整索引只留在 local。
- `analyze_content.py --project <项目>`：95 卡/28 项目/420 文本，按加载器规则补默认值；图片关联；本地 HTML；去掉完整字符串的游戏代码索引；所有程序集与两份原生 PE 的结构索引。
- `verify_analysis.py --source <安装> --project <项目>`：安装与副本、1985 对象原始哈希、引用闭合、内容数量、脚本映射、345 非空文件解码与空载荷解释。结果在 `evidence/verification.json`。
- `explorer.html`：原创页面模板；`analyze_content.py` 将第三方内容嵌入 `local/explorer.html`，模板本身不包含游戏内容。

## 本地浏览

通常可双击 `local/explorer.html`。浏览器工具若禁止 `file:`，可运行：

```powershell
C:/Python310/python.exe -m http.server 8766 --bind 127.0.0.1 --directory F:/workspace/rogue-prototype-lab/prototypes/re-crunch-time
```

然后打开 `http://127.0.0.1:8766/local/explorer.html`；仅绑定本机回环地址，用完退出终端。页面无需第三方网络、账户或服务；资源通过同目录相对链接访问。

## Unity 4.5 兼容处理

1. MonoBehaviour 不含完整发布 TypeTree。根据本包游戏程序集生成字段；生成器遗漏基类 `m_Enabled` 后的四字节对齐，所以构造新树时采用 UnityPy 对应版本的四项基类字段，保留生成的业务字段。46 个 `m_Script` 指针均与独立基类解析比对，1985 个对象完整消费通过。
2. Unity 4 Sprite 使用旧 `vertices/indices`，并非现代 `m_VertexData`。导出器按实际三角网格生成透明遮罩并翻转纹理坐标。PNG 解码/图片对应通过，未把此结果声称为与原生 GPU 画面逐像素相同。
3. 本版 Shader 的 `m_Script` 是字符串，保存为 UTF-8 `.shader`。其中包括编译平台片段，不能认为是作者原始 Shader 工程。
4. 动态字体纹理在构建内宽/高/载荷均为零，运行时生成；内置 Arial 仅有引用、无字体文件。原始对象和字段照样保存，空导出显式登记。

公开证据不包括 BuildSettings 的 token、Steam 账号、完整脚本文本、音乐、图片或字体；这些本地派生产物统一由项目 `.gitignore` 排除。
