# 手指骰子对决 · Finger Dice Duel

暗色奇幻风格的 2D 回合制对战游戏。用两只手的数字运算发动攻击、回复与护盾，通过职业技能、状态和座位距离决定战术。

团结引擎（Tuanjie）`2022.3.62t15` · C# · 内置渲染管线 · uGUI / TextMeshPro。

## 开始游戏

用团结引擎打开本目录，直接进入 Play。`GameBootstrap` 自动启动界面，不需要配置场景或挂载预制体。

已接入主菜单、各模式选将、先攻投骰、战斗、图鉴与秘法手册。选将可拖动；图鉴每页12名，支持职业和阵营筛选；战斗记录按钮位于“返回标题”下方，默认收起。

## 核心玩法

每人通常20点生命，初始双手为1/1；经典模式先投1d20决定初始手值与行动顺序。

选择自己的一只手，再选择另一名角色的一只手相加，只改变自己的手。结果取个位，0变1；攻击目标另选。

| 运算结果 | 基础效果 |
|---|---|
| 4 / 6 / 8 | 发起对应基础伤害的攻击 |
| 9 | 回复9点生命 |
| 5 | 合盾，抵挡下一次攻击后破碎 |
| 1 / 2 / 3 / 7 | 无基础效果 |

角色技能优先于基础规则。伤害还会计算攻防、抗性、状态与技能修正；多人距离按存活角色的座位环计算。完整数值和操作见 [RULES](RULES.md)。

## 模式与角色

| 模式 | 内容 |
|---|---|
| 1v1 | 入门 / 经典；入门可选简单、普通、困难AI |
| 2v2 | 入门 / 经典，四座环形战场；1与4、2与3分别组队 |
| 双将对决 | 娱乐模式1v1，每人组合两名同属守序、中立或混乱的子职 |
| 8人阵营混战 | 单将 / 双将，守序、中立、混乱三队，人数3/3/2 |
| 8人身份对局 | AI练习，主公、忠臣、反贼与内奸；隐藏身份及击杀奖惩 |

通常50轮，到限按个人或队伍总生命判胜；身份局80轮，到限平局。联网对战尚未实现。

当前 **12职业、39子职**，按“职业·子职”命名。职业包括战士、圣武士、牧师、法师、邪术师、术士、吟游诗人、游侠、游荡者、武僧、野蛮人和德鲁伊。

职业与子职使用独立绘画徽记。德鲁伊月亮结社的四种荒野形态、星辰结社的三种星耀形态均有图案，在图鉴及战斗面板右侧显示，当前形态高亮。伤害、状态、抗性、屏障、气与猎人标记可在秘法手册查阅。

## 开发与验证

规则内核、角色和AI独立于Unity；界面在运行时构建。完整回归包含大规模对局模拟，需要数分钟；单个测试类可单独运行。

```powershell
# 本机.NET 6长时间统计使用此设置，避免分层JIT原生崩溃
$env:DOTNET_TieredCompilation='0'
$env:COMPlus_TieredCompilation='0'

# 运行全部逻辑测试
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1

# 校验Unity/NUnit条件编译（只编译）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS

# 表现层编译与组件冲突检查
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-ui.ps1
```

当前 **1184项逻辑测试通过，0失败**；Unity条件编译及UI编译通过（2026-10-24）。界面效果另由实际引擎操作与截图验收，记录保存在 [Art/Integration](Art/Integration/)。AI统计是回归采样，不代表角色已平衡。

## 文档

| 文档 | 用途 |
|---|---|
| [RULES](RULES.md) | 权威规则、角色技能、状态与特殊交互 |
| [CODELY](CODELY.md) | 工程结构、开发约定、实现细节和技术债 |
| [图鉴写法](CODE-STYLE.md) | 文案长度、关键交互与排版约定 |
| [测试工具](Tools/HeadlessTests/README.md) | 环境、参数和专项运行方法 |
| [AI策略检查](Tools/ai-strategy-audit.md) | 策略覆盖、验证范围与统计报告入口 |
| [美术制作](Art/Production/) / [接入验收](Art/Integration/) | 原图、提示词、导入清单与实际界面截图 |

## 字体许可

正文与数字使用思源黑体（Noto Sans CJK SC），标题使用思源宋体（Source Han Serif CN），均采用SIL Open Font License 1.1。详见 [字体许可](Assets/Resources/Fonts/README-字体许可.txt)。
