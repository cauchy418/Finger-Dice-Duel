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

最新结果：**1533 项功能回归通过，0 失败**；8 项独立统计默认跳过，本轮专用五模式采样已另行完成。Unity/NUnit 条件编译、195 脚本表现层编译及 36 项真实界面检查通过，见[验收记录](../Art/Integration/monks-portents-rebalance/README.md)。

## 文档

| 文档 | 内容 |
|---|---|
| [RULES](../RULES.md) | 权威规则、完整技能数值、状态与特殊交互 |
| [CODELY](../CODELY.md) | 工程结构、实现细节、开发约定与技术债 |
| [模式与战斗](modes-and-combat.md) | 回合流程、模式细则、伤害类型、状态与元素反应 |
| [角色图鉴](roster.md) | 56 名子职数值、阵营分类、全角色胜率 |
| [图鉴写法](../CODE-STYLE.md) | 文案长度、关键交互与排版约定 |
| [测试工具](../Tools/HeadlessTests/README.md) | 环境、参数与专项运行方法 |
| [全角色AI策略与权重](ai-strategy-reference.md) | 56 名子职的自身策略、针对策略、手值／组合／技能分及 1v1／2v2 配置 |
| [AI策略检查](../Tools/ai-strategy-audit.md) | 专属策略、覆盖范围与统计说明 |
| [美术制作](../Art/Production/)／[接入验收](../Art/Integration/) | 原图、提示词、导入清单与实际界面截图 |

## 验证习惯

每个功能点在 `Art/Integration/` 下有一个验收目录，放真实界面截图、校验说明和复核记录。截图是引擎实际渲染，不是概念图。

字体使用思源黑体（Noto Sans CJK SC，正文与数字）与思源宋体（标题），均采用 SIL Open Font License 1.1，详见[字体许可](../Assets/Resources/Fonts/README-字体许可.txt)。
