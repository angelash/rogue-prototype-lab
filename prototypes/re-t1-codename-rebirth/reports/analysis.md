# 代号：新生测试包逆向分析

2026-10-13，v0.4。用户已确认授权。本次完成指定 APK 的全量载体清点、主要封装解码、业务热更新反编译、配置恢复、资源对象索引与媒体导出。**原作者源码、原 Unity 工程和服务端不能由这个 APK 还原**；原生 AOT 声明、实际 IL、机器代码和反编译文本分别保存。

## 身份与输入

标签 `代号:新生(测试版)`，包名 `com.gnml.t1yfnbcs`，版本 `0.9.0` / `9000000`，Unity **2022.3.62f3c1**，IL2CPP ARM64 / ARMv7。入口为 com.zero.main.MainActivity，Application 为 FNCrashCatchApplication，min SDK 22、target 30；manifest 的 debuggable 不单独证明所有调试功能开启。[身份](../evidence/identity.json)

原 APK 1,532,660,866 字节，SHA256 `9ee8b8d033772495b477f9b984b1e6b30eaee8959359fbf3e7e5c3768a2a84ea`。2051 ZIP 条目及解包的 1,552,692,913 字节已做 CRC/哈希核对；分析使用校验副本，原件没有改写。[输入](../evidence/intake.json)、[文件清单](../evidence/apk-files.json)

## 实际恢复

- **托管代码**：全部 38 份 QXPM 镜像，即 debug/release 各 3 份业务热更新和 16 份 AOT 补充镜像。208 分片、163,613 加密块的摘要与 AES-GCM 检查通过，每个输出为有效 PE。38 工程生成 17,872 个 C# 文件，全部 IL 指令读取无失败。AOT 补充镜像多数方法没有 IL。[解码](../evidence/hotfix-decoding.json)、[覆盖](../evidence/deep-coverage.json)
- **IL2CPP**：QXGM 3145 页、摘要表和头 HMAC 通过，恢复标准 metadata v31；生成 85 份 AOT 声明 DLL、12,586 类型声明和 199,560 原生方法地址。方法体仍以 ELF 机器代码为权威，DummyDll 的空体不是恢复的 C#。[元数据](../evidence/qxgm-decoding.json)、[本机声明](../local/il2cpp-arm64/dump.cs)、[地址](../local/native-methods.csv)
- **配置**：QCFG v2 两个容器有 **208 表、30,676 条记录、2362 列、35,592 池字符串、7256 池对象**。所有池/表段、对象消费、列宽及主键闭合。207 行表与生成类型比较，只有 task_action 的代码字段 format 不在表中；example 类型没有包内表。[配置](../evidence/config-decoding.json)、[关联](../evidence/content-linking.json)
- **资源**：YooAsset 22,162 地址对应 1017 文件，全 MD5/CRC/大小匹配。981 自定义资源包全部恢复、解压并读出 **191,092 对象**，类型树没有失败；1077 TextAsset 按原字节导出。[包](../evidence/bundle-decoding.json)、[对象](../evidence/bundle-objects.json)
- **媒体**：3204 Texture2D 中，3201 有可导出数据，3 个字体图集为 0×0 且无像素。17 浮点数据贴图另存逐字节一致的 RAW/NPY，PNG 仅为归一化预览。6867 网格导出 OBJ gzip；728 AudioClip 保存原编码数据和解码音频；27 个 FMOD 库另导出 333 样本。蒙皮、形态键、材质、动画与混音原资料仍在包中。[媒体](../evidence/media-export.json)、[FMOD](../evidence/fmod-samples.json)
- **其它载荷**：64 JSON、14 碰撞文件的 49,987 形状、57 FMOD RIFF 银行结构已读。641 HCRH、48 SDF、2 token map 与 QFGS 泛型共享清单保留完整字节和格式登记，未声称已转换全部原生语义。[载荷](../evidence/payload-inspection.json)
- **Android**：28 DEX 的 SDK 完整指令反汇编与独立解析核对为 11,505 类定义、71,190 方法体、2,059,980 指令单元；重复定义保留来源。JADX 有 6719 常规 Java 文件与 11,502 个逐 DEX fallback 文件，常规恢复有 15 错误，原指令未丢弃。[Android](../evidence/android-recovery.json)
- **基础 Unity**：此前仅基类的 970 组件，967 已补齐；加上原成功对象，7468/7472 类型树完整。剩余 3 脚本组件和 1 BuildSettings 不兼容，原字节及诊断保留。[基础补齐](../evidence/base-recovery.json)

## 游戏结构与内容

客户端包含拟人动物角色、射击、物品搜集与撤离机制，以及战备、背包/仓库、配件改装、技能、部门任务、家园建筑/工作台、交易、组队匹配和回放。相应方法、协议、配置与界面资源可回查。[架构](architecture.md)、[逐系统入口](systems.md)

序列化热更新配置的值 1 对应 ReleaseHotfix，不能凭 APK 文件名的 debug 判定 DebugHotfix。Release 三个业务程序集共 6972 类型、56,814 方法定义，其中 55,733 含实际 IL；包含协议、绑定器和编译器辅助类。[代码地图](code-map.md)

EntityMgr 明确注册 52 ECS 系统。EcsWorld 顺序执行 OnUpdate/OnLateUpdate，在边界回放命令缓冲；射击、子弹、后坐力、散布、技能、Buff、移动与相机分别处理。匹配、背包、建筑、制造和撤离通过请求/回包更新状态，服务端最终规则没有因此恢复。

实际表中有 64 武器、77 弹药、147 配件、1025 通用物品、7 角色、14 地图、100 部门任务、74 Buff、67 技能和 7214 语言行。地图含宿舍、教学、靶场、六号机场与测试场景；记录存在不等于全部开放。按真实 LangMgr 哈希关联后，14 地图、7 角色及 64 武器名称全部对应。[内容与资产](content-and-assets.md)

## 精度与边界

业务源码使用完整 AOT 声明引用重反编译，保留正确属性/索引器语义。两套 DressableModelMgr 各有 4 条 ILSpy 类型诊断，共 8 条；原 IL、token 和指令可回查。未宣称反编译文件已通过 Unity 重新编译。

QGSG 与 QXTL 外层签名未独立验签；内部摘要、HMAC、分片/块 SHA 和 AES-GCM 标签已验证。没有修改签名、凭证或许可。未安装/运行 APK、登录、发送服务端请求、操作存档，也没有 Android 输入、性能、全帧视频或听感测试。[完整边界](recovery-scope.md)

25/25 深层字节、格式、覆盖与索引检查通过；本机检索的 15/15 检查通过，含源码跳转、中文配置、媒体预览、路径范围、快速切换及桌面/窄窗口布局。[深层校验](../evidence/deep-verification.json)、[页面 QA](../evidence/deep-explorer-qa.json)

所有原件及第三方派生产物留在 local/ 独立无远程 Git。公开仓库只备份原创报告/工具和汇总证据；源码、配置全文、媒体不公开上传。初查证据保留，与深层结果分别登记。

完整本地提交 `433faff` 保存 71554 文件 / 8.22 GB；C 盘独立备份的全部 Git 对象检查与原 APK blob SHA256 通过。F 盘研究文件保留原位，工作库借用 C 库对象，迁移版本历史须保留 C 库。[本地备份](../evidence/deep-backup.json)、[原创资料远程备份](../evidence/deep-publication.json)

## 使用

[本地只读检索](../scripts/serve_catalog.py)支持类型/源码、方法、调用位置、配置全文/中文名称、对象与媒体。启动后访问 http://127.0.0.1:8769/。[原离线地址页](../local/explorer.html)保留全部地址/依赖及初始载体索引。[复跑文档](../scripts/README.md)
