# 开发与测试

[返回索引](README.md)

## 技术栈

团结引擎（Tuanjie）`2022.3.62t15` · C# · .NET 6 · 内置渲染管线 · uGUI / TextMeshPro。

规则内核、角色与 AI 独立于 Unity，可以脱离引擎在 .NET 下无头编译运行；界面全部在运行时构建，所以打开工程直接 Play 就能启动，不需要配置场景或挂载预制体。单一程序集定义 `Assets/Scripts/FingerDiceDuel.Runtime.asmdef`，分层为 `Assistant/`、`Characters/`、`Core/`、`UI/`。

## 测试

```powershell
# 本机.NET 6长时间统计关闭分层JIT
$env:DOTNET_TieredCompilation='0'
$env:COMPlus_TieredCompilation='0'

# 全部逻辑测试
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1

# Unity/NUnit条件编译（只编译）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS

# 表现层编译与组件冲突检查
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-ui.ps1
```

最新结果：**1769 项功能回归通过，0 失败**；功能回归跳过8项独立统计；另行完成10,092次选点审计；58子职汇总66,964局，本轮六组全部重新采样，使用同一冻结程序集，见[最新胜率](../Tools/Statistics/2026-11-11-ai-activation/README.md)。德鲁伊重构与孢子结社通过专项回归及21项引擎检查，UI与Unity/NUnit条件编译通过，见[形态与孢子验收](../Art/Integration/druid-rework-v2/README.md)。四章40关的指引、实战和重试已用控制器验证，Unity原生界面覆盖旧进度迁移、章节、技能选择、中央确认与直接重试；见[四章接入验收](../Art/Integration/campaign-v3/README.md)。

## 精简后的完整性核查

已核对精简提交 `ba6c589` 相对 `9947e7b` 的132个删除文件（42份文档、90个工具／数据文件）。`Assets/`、`Packages/`、`ProjectSettings/` 和 `Tools/AiTuning/` 均无删除或改动，运行时资源、Unity导入信息、场景配置与测试源码保持完整；旧采样和预览输出可从Git历史取回。

两份仍有效的次战／九战逐步演算已并入[第一章现行设计](Adventure/four-chapter-v3/chapter-01-and-04.md)，同时校正配置文件与源码注释的文档路径。仍被权重实验入口使用的[角色权重汇总脚本](../Tools/Statistics/summarize-role-tuning.py)已恢复，要求显式指定输入目录，避免引用已删除的旧采样目录。

精简后重新运行功能回归：1769项通过、0失败；UI的243个脚本编译通过且无组件冲突。现行66,964局原始数据与已发布汇总核对一致，八人阵营／身份表均由原数据补全，保留原采样提交与源码指纹。仓库Markdown相对链接已检查，无缺失目标。

## 文档

| 文档 | 内容 |
|---|---|
| [RULES](../RULES.md) | 权威规则、完整技能数值、状态与特殊交互 |
| [CODELY](../CODELY.md) | 工程结构、实现细节、开发约定与技术债 |
| [模式与战斗](modes-and-combat.md) | 回合流程、模式细则、伤害类型、状态与元素反应 |
| [角色图鉴](roster.md) | 58 名子职数值、阵营分类、全角色胜率 |
| [图鉴写法](../CODE-STYLE.md) | 文案长度、关键交互与排版约定 |
| [测试工具](../Tools/HeadlessTests/README.md) | 环境、参数与专项运行方法 |
| [全角色AI策略与权重](ai-strategy-reference.md) | 58 名子职的自身策略、针对策略、手值／组合／技能分及 1v1／2v2 配置 |
| [AI策略检查](../Tools/ai-strategy-audit.md) | 专属策略、覆盖范围与统计说明 |
| [美术制作](../Art/Production/)／[接入验收](../Art/Integration/) | 原图、提示词、导入清单与实际界面截图 |

## 验证习惯

每个功能点在 `Art/Integration/` 下有一个验收目录，放真实界面截图、校验说明和复核记录。截图是引擎实际渲染，不是概念图。

字体使用思源黑体（Noto Sans CJK SC，正文与数字）与思源宋体（标题），均采用 SIL Open Font License 1.1，详见[字体许可](../Assets/Resources/Fonts/README-字体许可.txt)。

本次新增聚众赌博、破阵者AI默认设置、八名高胜角色对策及技能名称调整已完成1769项回归。首次全量运行在宿主.NET发生原生访问异常，使用同一程序集从头完整重跑后1769/0通过；未跳过失败用例。后续已在AI启用审计完成后全量重采，当前报告保存最终源码来源。
