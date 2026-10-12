# 妖怪夜市开张

完整包装与开发准备 v1.0，2026-10-12。把 Crunch Time 的筹备卡牌竞赛改为鬼门开启前的妖怪夜市，加入五格双用途设施、相邻供电、变卖与公开来客条件。

- [开发合同](DEVELOPMENT.md)：任务范围、用户决定、制作基线与实际状态。
- [完整设计入口](design/00-index.md)：产品、规则、内容、AI、故事、UI、工程、保存、音频、验收。
- [全部内容手册](design/04-content.md)：95卡、28委托、12设施/24用途、5类客人、46角色、7章记事、14步教学的全文。
- [权威策划JSON](data/catalog.json) / [字段说明](data/README.md) / [CSV导入查阅表](data/csv/)：全数值和原创中文文案。
- [包装替换总账](replacement/README.md)：95卡、28项目、420文本键、74资源键、1985对象、30业务类全部有明确处置。
- [生图规范](art/ART_DIRECTION.md) / [完整提示词](art/prompt-index.md) / [生图任务JSON](art/generation-jobs.json)：150条完整生成提示词＋150条单件修订提示词；196项资产规格和24项音频规格。
- [28委托设施可达路径](data/opening-layouts.json)：具体格位、用途、支出和合法调整步骤。
- [设计校验报告](evidence/design-verification.json)：覆盖、引用、尺寸、文本、来源、设施服务路径与文档链接检查。
- [来源与授权](sources/register.md)：原作只使用必要结构/数值，不复制其源码、正文、图像和音轨。

本轮交付的是全部已采用范围的设计准备包，做到100%条目覆盖与引用闭合。Unity工程、素材量产、游戏测试、平衡试玩、Player构建及发行尚未执行；其放行条件已列入生产计划。

在当前工程根目录运行：

```powershell
C:/Python310/python.exe -X utf8 prototypes/proto-yokai-night-market/scripts/derive_design.py
C:/Python310/python.exe -X utf8 prototypes/proto-yokai-night-market/scripts/verify_design.py
```

派生脚本不依赖原作安装或私有反编译文件，可在仅有本仓公共内容时复跑。数值修改只改catalog，再重新派生。下一阶段从productionPlan.P02开始，先做确定性规则与保存纵切，样件经Player验收后再量产。
