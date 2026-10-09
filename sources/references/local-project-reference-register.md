# 本地项目参考与工具证据登记

版本：v1.0；日期：2026-10-09（Asia/Shanghai）。状态：只读参考登记；集中复核时间为 `2026-10-09T16:19:08+08:00`，此前资料在本轮分批读取。

用户已明确为 013 套圈改造摊采用本地已有 Unity、PC 单机和 Steam 目标，并指定参考本地 highschool 与 world-of-claudecraft。这里登记实际读取的来源、版本、筛选理由和环境证据；工程结构、2.5D 美术及开发流程的采用方式属于助手建议。原始文档、代码和资产仍保留在外部仓库，本轮未复制或导入本仓。

对应整理文档：[08 Unity PC 技术方案](../../docs/design/first-batch/proto-013-ring-toss/full-game/08-unity-pc-technical-plan.md)、[09 美术与资产生产](../../docs/design/first-batch/proto-013-ring-toss/full-game/09-art-and-asset-production.md)、[10 开发流程与技能](../../docs/design/first-batch/proto-013-ring-toss/full-game/10-development-workflow-and-skills.md)。这些文档记录适配方案；本登记保留来源快照，不重复玩法参数。

## 1. 快照与证据的含义

- 提交号、作者时间、提交时间来自本地 `git log -1`；仓库来源来自本地 remote 配置。本轮未拉取远端，链接不代表核对了远端最新状态。
- 文件状态来自针对选定路径的 `git status --short -- <路径>`，只说明这些参考文件的状态，不宣称整个外部工作区干净。
- SHA256 来自 `Get-FileHash -Algorithm SHA256`，针对当时的实际本地文件。哈希用于以后复核文件是否变化，不替代资产授权、功能测试或构建验证。
- 下文的绝对路径是本机原始位置，固定提交链接便于追溯版本。存在工作区修改的文件单独注明，不能把其内容归到 HEAD。

## 2. highschool：本地 Unity 工程与构建方法参考

### 2.1 仓库快照

- 原目录：[F:/workspace/highschool](F:/workspace/highschool)。Unity 工程：[game/SheGuessedItAgain](F:/workspace/highschool/game/SheGuessedItAgain)。
- origin：`https://gitee.com/wingkin/highschool.git`；来源主页：[wingkin/highschool](https://gitee.com/wingkin/highschool)。
- HEAD：`71bb99d1916857de954d3751b028faa80604e323`；[固定提交](https://gitee.com/wingkin/highschool/commit/71bb99d1916857de954d3751b028faa80604e323)。
- 作者时间与提交时间均为 `2026-08-07T09:21:34+08:00`；提交标题为 `docs: align asset checkout with Gitee`。
- 六项关键 Unity 文件及 `production/BUILD_INSTRUCTIONS.zh-CN.md` 的定向状态核查没有修改记录；[README.md](F:/workspace/highschool/README.md) 显示 ` M README.md`，本轮读取的是该本地修改稿。下面的 README 哈希属于工作区版本，不对应固定提交内容。

### 2.2 关键文件、筛选理由与 SHA256

以下相对原文件名均以 `F:\workspace\highschool` 为根；六项文件均为已跟踪文件。

1. [game/SheGuessedItAgain/ProjectSettings/ProjectVersion.txt](F:/workspace/highschool/game/SheGuessedItAgain/ProjectSettings/ProjectVersion.txt)
   - 理由：记录精确编辑器版本与 revision，与本机 Unity 可执行文件交叉核对；保留 `c1` 标识。
   - SHA256：`B42279CFD794D9F1825F3B7C1F318B861FA9E2E2B3C6C146737BDBD41C01B389`。
2. [game/SheGuessedItAgain/Packages/manifest.json](F:/workspace/highschool/game/SheGuessedItAgain/Packages/manifest.json)
   - 理由：了解参考项目实际声明的依赖；不把参考依赖全集或包源自动移植到新游戏。
   - SHA256：`8FE69DD0528D091DDD3769123853E65E2FB9B65A8ECF61A906D9786EB7EEFE6A`。
3. [game/SheGuessedItAgain/Packages/packages-lock.json](F:/workspace/highschool/game/SheGuessedItAgain/Packages/packages-lock.json)
   - 理由：区分声明版本和锁定版本，为新项目后续实际包解析提供参考。
   - SHA256：`64257A73C48CFCA5AC2759AC6B1C9D4C5FA08564C5E348BD8DFF85107C41EF3D`。
4. [game/SheGuessedItAgain/Assets/Game/Editor/ProjectBuilder.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Editor/ProjectBuilder.cs)
   - 理由：参考静态编辑器构建入口、Windows x64 Mono、内容检查与失败报告；原脚本重建 Bootstrap 场景和设置原产品名的行为不照搬。
   - SHA256：`02A314D7E472DB66B43E292A786D00BBB50AF68DCCCD51B9A51702C91B84C9C7`。
5. [game/SheGuessedItAgain/Assets/Game/Runtime/State/GameState.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/State/GameState.cs)
   - 理由：参考规则状态与序列化 DTO 分离、集合稳定排序；原剧情变量不代替本游戏物权和账本。
   - SHA256：`BD8CC5ECEED4C4EB807DD933A4B15D6ACC76BF10A5C8095F3D1EA290CD6AB632`。
6. [game/SheGuessedItAgain/Assets/Game/Runtime/Services/SaveService.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/Services/SaveService.cs)
   - 理由：参考进度、设置与 Profile 职责。其 `AtomicWrite` 实际是临时写入、删除当前文件、再移动，存在中断窗口；不能作为本游戏原子保存保证直接采用。
   - SHA256：`A4B22EAE32AC64223E39D28C06CB2F66849B45251E190F13CE8D2E96A3DE751C`。

本地修改的 [README.md](F:/workspace/highschool/README.md) SHA256 为 `88142E80992E5B8DC6CBAFD8799823F6F18A4E7070121AB551CD25268B4D8230`。它仅用于复核本轮读到的项目组织说明；不将其工作区内容作为 HEAD 的固定版本证据。

### 2.3 其他实际参考路径

下面保留原文件名与取舍，详细解读以 08/09 为准；没有复制文件正文。

- [production/BUILD_INSTRUCTIONS.zh-CN.md](F:/workspace/highschool/production/BUILD_INSTRUCTIONS.zh-CN.md)：核对既有编辑器、Windows x64 Mono 与构建入口。
- [Assets/Game/Runtime/Bootstrap/RuntimeBootstrap.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/Bootstrap/RuntimeBootstrap.cs)：参考服务初始化、单一启动入口与设置加载的职责，不采用单个脚本包办新玩法。
- [Assets/Game/Runtime/UI/UiFactory.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Runtime/UI/UiFactory.cs)：参考 uGUI 组织；实际使用 `UnityEngine.UI.Text`，不能据 manifest 中有 TMP 就宣称参考 UI 已验证 TMP。
- [Assets/Game/Editor/GameAssetPostprocessor.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Editor/GameAssetPostprocessor.cs)：参考目录化导入约定，不直接套用影游图片设置到透明圈或三维模型。
- [Assets/Game/Tests/Editor/StoryGraphTests.cs](F:/workspace/highschool/game/SheGuessedItAgain/Assets/Game/Tests/Editor/StoryGraphTests.cs)：参考 EditMode 内容检查与状态往返测试组织；原游戏测试结果不计为本游戏结果。
- [ProjectSettings/GraphicsSettings.asset](F:/workspace/highschool/game/SheGuessedItAgain/ProjectSettings/GraphicsSettings.asset)、[ProjectSettings/ProjectSettings.asset](F:/workspace/highschool/game/SheGuessedItAgain/ProjectSettings/ProjectSettings.asset)：核查参考项目现有渲染和输入设置，不推导新游戏已经采用 URP 或 Input System。
- [production/THIRD_PARTY_NOTICES.zh-CN.md](F:/workspace/highschool/production/THIRD_PARTY_NOTICES.zh-CN.md)：09 所记录 Noto Sans CJK SC 2.004、OFL 1.1 的参考依据；字体仍未导入，正式采用时核对实际字体文件和许可。

历史证据位置：[artifacts/unity-build-full.log:3003](F:/workspace/highschool/artifacts/unity-build-full.log:3003) 记录 `Windows full build complete`，输出共 312,297,628 字节；现存 [SheGuessedItAgain.exe](F:/workspace/highschool/game/SheGuessedItAgain/Builds/WindowsFull/SheGuessedItAgain.exe) 为 666,624 字节、同目录 UnityPlayer.dll 为 31,200,080 字节。它们属于参考项目已有结果，本轮没有重新构建或运行；原题材、剧情、图片与小游戏没有进入本仓。

## 3. world-of-claudecraft：工作流与资产追溯参考

### 3.1 仓库快照

- 原目录：[F:/workspace/github/world-of-claudecraft](F:/workspace/github/world-of-claudecraft)。
- origin：`git@github.com:angelash/world-of-claudecraft.git`；来源主页：[angelash/world-of-claudecraft](https://github.com/angelash/world-of-claudecraft)。upstream 为 `git@github.com:levy-street/world-of-claudecraft.git`，对应 [levy-street/world-of-claudecraft](https://github.com/levy-street/world-of-claudecraft)，其本地 push URL 为 `DISABLED`。
- HEAD：`cd961d32105186f4938afd2047adee943ff85ae8`；[固定提交](https://github.com/angelash/world-of-claudecraft/commit/cd961d32105186f4938afd2047adee943ff85ae8)。
- 作者时间与提交时间均为 `2026-07-28T19:47:25+08:00`；标题为 `refactor(fork): retire superseded client paths`。
- 10 所选的五项技能、`docs/codex.md`、`docs/design/planning-docs.zh_CN.md`、`CLAUDE.md` 和 `LICENSE` 定向状态核查无修改记录；09 所登记资产规范与许可文件亦在其只读核查范围内。本登记没有断言外部仓所有文件都未修改。

### 3.2 开发工作流筛选

以下固定提交链接对应原文件名。迁移的是方法，由本仓重新编写技能与说明，不复制参考技能正文。

- [.agents/skills/woc-feature-plan/SKILL.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/.agents/skills/woc-feature-plan/SKILL.md)：纵切任务、显式决定、验证和接续点，适配为 Unity 任务包。
- [.agents/skills/woc-qa/SKILL.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/.agents/skills/woc-qa/SKILL.md)：一次确定检查范围、集中执行检查、共享证据后独立审阅，适配为阶段 QA。
- [.agents/skills/woc-extract-and-test/SKILL.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/.agents/skills/woc-extract-and-test/SKILL.md)：纯决策隔离、实际路径复现和围绕行为验证，适配为 C# 规则与 Unity 薄适配。
- [.agents/skills/woc-release-merge-audit/SKILL.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/.agents/skills/woc-release-merge-audit/SKILL.md)：比较集成双方意图、注册和兼容性，适配为实际发生集成时的语义复核。
- [.agents/skills/woc-codex-audit/SKILL.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/.agents/skills/woc-codex-audit/SKILL.md)：薄技能入口与权威文档路由，适配为本仓两项技能；不绑定未核实的工具和模型。
- [docs/codex.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/docs/codex.md)：入口、流程与规范分工；不把其自动发现、hooks 或 MCP 描述当作本机能力证据。
- [docs/design/planning-docs.zh_CN.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/docs/design/planning-docs.zh_CN.md)：区分事实、设计意图、PRD 和表现规格；原文最后核对日期为 2026-06-20。
- [CLAUDE.md](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/CLAUDE.md)：规则核心、显式随机源、表现不改变结果与生成资料可追溯；不移植 MMO 公式、服务权威、20 Hz 或模型专属约定。

### 3.3 关键文件 SHA256

原目录根为 `F:\workspace\github\world-of-claudecraft`：

- `CLAUDE.md`：`B2A14875B7275A85E2D8A2ED5A4015DCE866B1AD1E07379DAEDA8C4F9FAE088A`。
- `LICENSE`：`FD7243A1F67FEBF462437C1E21C05720C872CB821F99EE8CEC10F88E8244C4FB`。
- `docs/codex.md`：`E119939793FD1FC37765B6C2B18F4C410B3582F42D9F029B119B88FA9FC55AE6`。
- `docs/design/planning-docs.zh_CN.md`：`6506E7E1668CABEBBE632B24C56587699F2C0F10393BEF8A5819B36A655FE4E1`。

### 3.4 资产参考的原文件名与范围

- [docs/image-to-glb-asset-workflow.md](F:/workspace/github/world-of-claudecraft/docs/image-to-glb-asset-workflow.md)、[.agents/skills/woc-image-to-glb/SKILL.md](F:/workspace/github/world-of-claudecraft/.agents/skills/woc-image-to-glb/SKILL.md)、[.claude/skills/image-to-glb/SKILL.md](F:/workspace/github/world-of-claudecraft/.claude/skills/image-to-glb/SKILL.md)、[scripts/assets/CLAUDE.md](F:/workspace/github/world-of-claudecraft/scripts/assets/CLAUDE.md)：参考适用性、语义部件、源文件、导出与图像质检的顺序；不引入其 Three.js/npm 生产工具作为 Unity 必要依赖。
- [docs/design/graphics-plan.md](F:/workspace/github/world-of-claudecraft/docs/design/graphics-plan.md)、[docs/design/lookdev-hookup.md](F:/workspace/github/world-of-claudecraft/docs/design/lookdev-hookup.md)：参考按已实现证据更新材质、质量档位和后处理计划；浏览器性能数字不作为 Unity 实测值。
- [docs/design/physics-asset-audit.md](F:/workspace/github/world-of-claudecraft/docs/design/physics-asset-audit.md)：参考逐件记录视觉、碰撞与故意例外；本游戏的模型与动画不能反向改变二维规则。
- [CREDITS.md](F:/workspace/github/world-of-claudecraft/CREDITS.md)、[THIRD_PARTY_NOTICES.md](F:/workspace/github/world-of-claudecraft/THIRD_PARTY_NOTICES.md)、[scripts/assets/specs/props.json](F:/workspace/github/world-of-claudecraft/scripts/assets/specs/props.json)、[scripts/assets/specs/lookdev.json](F:/workspace/github/world-of-claudecraft/scripts/assets/specs/lookdev.json)：核对作者、原始包、许可、派生映射与候选资源。候选模型/纹理的具体原文件名、当前文件 SHA256 和官方来源集中保存在 09 的第 9 节，本登记不另复制一套清单。

09 已检查候选 GLB 的 `EXT_meshopt_compression`、`KHR_mesh_quantization`，部分含 `EXT_texture_webp`。这只说明发行文件编码要求，不能宣称其能被本机 Blender 或 Unity 原生完整导入。建议保留 Blender 制作源和显式 FBX 输出，并按 08/09 验证单位、轴与枢轴；本轮未执行往返导入。

## 4. Unity 安装与 Windows x64 Mono 证据

2026-10-09 只读读取工程文本与 Windows 文件版本，未启动 Unity。

- 编辑器：[D:/Program files/2022.3.62f3c1/Editor/Unity.exe](<D:/Program files/2022.3.62f3c1/Editor/Unity.exe>)；大小 `89411408 B`，`ProductVersion=2022.3.62f3c1_1623fc0bbb97`，`FileVersion=2022.3.62.1451004`。
- highschool `ProjectVersion.txt` 的版本为 `2022.3.62f3c1`、revision `1623fc0bbb97`，与编辑器文件相符。
- 开发 Mono Player：[win64_player_development_mono/UnityPlayer.dll](<D:/Program files/2022.3.62f3c1/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_development_mono/UnityPlayer.dll>)；大小 `50221392 B`。
- 非开发 Mono Player：[win64_player_nondevelopment_mono/UnityPlayer.dll](<D:/Program files/2022.3.62f3c1/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/UnityPlayer.dll>)；大小 `31200080 B`。
- 两个 DLL 均为 `ProductVersion=2022.3.62f3c1 (1623fc0bbb97)`、`FileVersion=2022.3.62.1451004`。模块原目录为 `Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/`。
- 08 另记录 [modules.json](<D:/Program files/2022.3.62f3c1/modules.json>) 中 `windows-il2cpp`、`windows-server` 为 `selected=false`，检查到的 Player 变体为 Mono；不把 IL2CPP 列为已具备。

已确认的是这套安装和 Mono 模块文件存在；许可证、首次编辑器启动、包解析、新工程编译与新游戏桌面构建均未验证。未复制 Unity 二进制、Library、账号配置或许可证到本仓。

## 5. Blender CLI 版本查询

原可执行文件：[D:/SteamLibrary/steamapps/common/Blender/blender.exe](D:/SteamLibrary/steamapps/common/Blender/blender.exe)。2026-10-09 执行已有 CLI 的 `--version`，实际返回：

```text
Blender 5.2.2 LTS
build date: 2026-09-15
build time: 01:37:04
build commit date: 2026-09-14
build commit time: 15:14
build hash: d13f752e3b9c
build branch: blender-v5.2-release
build platform: Windows
build type: Release
build system: CMake
```

该查询确认已有 CLI 的实际版本；没有生成几何、转换模型、连接 MCP、安装插件或检验 Unity 往返。Blender 低模旧物与机关、二维人物/背景、UI、音频的分工见 09，工具版本不能代替资产成品与功能可读性验收。

## 6. 许可与迁移边界

- WoC 的 [LICENSE](https://github.com/angelash/world-of-claudecraft/blob/cd961d32105186f4938afd2047adee943ff85ae8/LICENSE) 为 MIT，署名 `Copyright (c) 2026 Levy Street`。该代码许可不自动授权仓内图片、模型、字体和音频，也没有被赋给本仓；本仓开源许可仍由用户后续决定。
- 本轮采用原创工作流适配与来源引用，没有复制外部代码、技能正文、文档全文或媒体。借鉴流程不等于移植其 Web、MMO、Three.js、多人同步或服务部署实现。
- 09 中 Kenney、Quaternius 等开源候选仍须逐项保存原作者、包名、实际版本/文件、许可和派生关系；候选登记不等于素材已经授权入库或导入成功。不得用仓库 MIT 替代单项媒体许可。
- WoC 中专用许可、仅随项目使用或 CC BY-NC 的素材不作为本游戏商业发行资产来源。highschool 的剧情、图片和其他媒体也未取得可迁移结论；本轮不复制。
- 原始资料仍留在两处外部仓。`sources/references/` 本次仅新增此登记，整理文档通过链接指向实读来源，不以外部完整工程充当本仓的备份。

## 7. 后续复核方法

需要再次采用参考内容时，先核对 HEAD、选定文件状态和哈希，再按具体内容重新确认许可与适用性。HEAD 未变但文件哈希改变时，记录工作区版本，不能继续引用旧快照结论。

后续实际工具验证分别保存 Unity 启动/许可证、包解析、构建、桌面运行和 Blender/Unity 往返的证据；本登记的版本查询结果不补写为这些步骤已通过。参考文件新增或版本变化时追加原路径、日期、筛选理由与变更说明，不覆盖本轮来源依据。
