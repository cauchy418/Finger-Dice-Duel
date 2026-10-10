# CODELY.md

## Project Overview

**Finger Dice Duel**（手指骰子对决）是一款 2D 回合制手指游戏。玩家各用两只手（初始数值均为 1），
每回合用自己的一只手与对方的一只手做加法，结果按十进制取个位（和为 10 时变成 1）；
结果数字决定效果：4/6/8 为攻击、9 为回复、5 为护盾。生命初始 20，先归零者败。

**规则权威文档是 [`RULES.md`](RULES.md)**，代码、测试、UI 都必须与它一致。
本文件只描述工程结构与开发方式。

- **引擎:** 团结引擎（Tuanjie，Unity-China 的 Unity 衍生版）`2022.3.62t15` / Tuanjie Editor `1.10.3`。
  资产是 YAML 序列化，但场景/资产使用团结的 tag 命名空间 `tag:yousandi.cn,2023`。
  标准 Unity 的知识与约定基本适用。
- **语言:** C#。
- **渲染/UI:** 内置渲染管线 + 正交相机；uGUI (`com.unity.ugui`) + TextMeshPro。

## Current State

### 已完成

| 模块 | 内容 |
|---|---|
| **规则内核** (`Assets/Scripts/Core`) | 数值运算归一化、护盾状态机、回合驱动、伤害结算、胜负判定、连锁破盾防递归、整场复位 |
| **角色系统** (`Assets/Scripts/Characters`) | 数据层 `CharacterDefinition` + `CharacterFactory`；三个角色技能：战士（连锁破盾 / 盾牌修复）、咒术师（无视护盾）、野蛮人（锁手） |
| **对局驱动** (`Assets/Scripts/Assistant`) | `MatchRunner` 整局驱动、随机/贪心 AI、脚本化测试替身、`BoardController` 交互状态机 |
| **表现层** (`Assets/Scripts/UI`) | `GameBootstrap` 入口（`[RuntimeInitializeOnLoadMethod]` **自动启动** + 自动建 `EventSystem`）、`GameBoardUI` 棋盘界面、`HandVisual` 显示规则、`UiFont` 字体加载。**全部 UI 运行时用代码构建，无预制体** |
| **字体** | `Assets/Resources/Fonts/NotoSansSC-Regular.ttf`（Noto Sans SC，**SIL OFL 1.1** 可自由分发；同目录有许可说明） |
| **测试** (`Assets/Tests/EditMode`) | 375 项 EditMode 测试，22 个夹具 |
| **工具** (`Tools/`) | 无头测试（秒级回归）、表现层编译检查 |
| **程序集划分** | `FingerDiceDuel.Runtime.asmdef` + `FingerDiceDuel.Tests.EditMode.asmdef` |

### 尚未完成

- **尚未实机验证**：本机 `Tuanjie.exe` 批处理模式会直接崩溃，所以表现层只做到"能编译"，
  界面效果、点击、字体渲染都还没有在编辑器里真跑过一次。
- **中文字体**：界面用内置字体（`LegacyRuntime.ttf`），**不含中文字形**，
  中文会显示成方块。需要接入一个中文字体资源（见下方 P1 待办）。
- 手部/护盾的美术资源、动画、音效、伤害飘字。
- 血条（目前只有数字）。
- 完整角色选择界面（目前只有两个下拉框）。
- 战士连锁破盾 / 野蛮人锁手的**玩家手动选择**（目前自动决定）。

## Directory Structure

| Path | Purpose |
|---|---|
| `Assets/Scripts/Core/` | 规则内核。**不依赖任何 Unity 类型**，可在纯 .NET 下编译测试。 |
| `Assets/Scripts/Core/Enums/` | `HandEffectType` / `ShieldState` / `SpecialLogicType` |
| `Assets/Scripts/Characters/` | 角色数据层与工厂 |
| `Assets/Scripts/Characters/SpecialLogics/` | 各角色技能实现 |
| `Assets/Scripts/Assistant/` | AI、测试替身、整局驱动器、交互状态机。**同样不依赖 Unity。** |
| `Assets/Scripts/UI/` | 表现层（唯一依赖 UnityEngine 的目录）。界面全部代码构建。 |
| `Assets/Tests/EditMode/` | EditMode 测试；`UnityTestShim.cs` 是环境适配门面 |
| `Assets/Scenes/SampleScene.scene` | 模板场景，**不含任何游戏内容也无需修改**（入口靠自动启动） |
| `Tools/HeadlessTests/` | 无头测试工具（见其 `README.md`） |
| `Tools/UiCompileCheck/` | 表现层校验：`check-ui.ps1`（编译）+ `check-components.ps1`（组件冲突静态检查） |
| `RULES.md` | **规则权威文档**，改动规则先改这里 |
| `Library/`, `Temp/`, `Logs/`, `obj/` | 引擎生成物，**不要编辑或提交**，可安全删除 |

## Building and Running

### 在编辑器里跑（看画面）

1. 用团结 Hub 打开本目录。
2. **直接按 Play**（任意场景都行，包括模板的 `SampleScene`）。
   `GameBootstrap` 通过 `[RuntimeInitializeOnLoadMethod]` 自动创建，并按需创建
   `EventSystem`，不需要场景配置、不需要预制体、也不需要把脚本拖到物体上。
3. 界面会是"角色选择"页：选好双方角色 → 点"开始对局"。

界面纵向分区（自上而下）：**玩家 B（对手） → 提示文字 → 玩家 A（自己） → 战斗文字**。
AI 出手前会等 2 秒（`GameBootstrap.AiThinkSeconds`），方便看清上一回合结果。

### 跑逻辑测试（秒级，不需要编辑器）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1
```

校验 Unity/NUnit 侧能否编译：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS
```

### 编译检查表现层

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-ui.ps1
```

这一步同时做两件事：
1. 用**真实引擎程序集**编译 `Assets/Scripts`（能抓 API 写错、名字空间缺失）。
2. 静态检查 `new GameObject(name, typeof(...), ...)` 的**运行时组件冲突**
   （编译器抓不到的那类，见 `check-components.ps1`）。

> 本机 `Tuanjie.exe` 的批处理模式会直接崩溃（exit 140063，无输出），
> 所以**无法用编辑器做自动化编译/测试**。上面两个脚本是替代方案：
> 逻辑测试是真跑，表现层只保证能编译 + 无明显组件冲突。

### 编辑器内测试

Window → General → Test Runner → EditMode → Run All。

## Development Conventions

- **🚫 绝对不要手工创建 / 编辑 `.meta` 文件。** 见下方"GUID 陷阱"。
- **编码：** 源文件为 **UTF-8 with BOM**。PowerShell 脚本必须是 UTF-8 with BOM，
  否则 Windows PowerShell 5.1 会把中文读成乱码并报语法错。
- **每个资产都有同名 `.meta`**，与资产一起提交；不要手工编辑 `Library/` 派生数据。
- **C# 约定:** 类型/方法 PascalCase，一个文件一个主要类型，命名空间统一 `FingerDiceDuel.*`。
- **保持 `Core` / `Characters` / `Assistant` 不依赖 Unity。** 这是"秒级无头测试"的前提。
  如需 Unity 类型（`MonoBehaviour` / `ScriptableObject`），放到 `Assets/Scripts/UI/`。
- **非法操作不要静默失败。** `GameManager.ExecuteTurn` 遇到不合法运算会抛
  `InvalidOperationException`，便于 UI/AI 层尽早暴露 bug。
- **改规则时同步改三处：** `RULES.md`、代码、`Assets/Tests/EditMode` 的对应测试。
- **不要修改:** `Library/`、`Temp/`、`Logs/`、`.codely.packages/`、`Temp/TuanjieTempFile-*`。
- **AI 生成资产:** TJGenerators MCP 已配置在 `.codely-cli/settings.json`，可生成
  3D 模型、图片、精灵表、天空盒、粒子、音频/BGM/TTS。

## ⚠️ 表现层踩过的坑（都已修，别再踩）

### 1. GUID 格式陷阱（最坑的一个）

团结引擎**不使用标准 GUID**：

| 资产类型 | GUID 格式 | 说明 |
|---|---|---|
| `.cs.meta`（`MonoImporter`） | **41 字节**（base64 后 56 字符） | 加密格式：头部 + 16 字节真实 GUID + 类型码 + 签名 |
| `.scene.meta` | **32 位纯 hex**（`^[0-9a-f]{32}$`） | 普通 GUID |
| 目录 `.meta` | 41 字节 base64 | 与脚本同族 |

**后果：** 手工按普通 base64（32 字节）生成的 `.cs.meta` 过不了引擎校验，
引擎报 *"does not have a valid GUID and its corresponding Asset file will be ignored"*，
**脚本被整体忽略**；场景里对脚本 GUID 的引用随之失效，导致
*"Cannot open scene … the scene hasn't been imported"*。

**正确做法：**

1. **不要手工写 `.meta`。** 让编辑器导入时自己生成 —— 那是唯一能产出合法 GUID 的途径。
2. **不要手工写场景里的脚本引用**（`m_Script: {fileID: 11500000, guid: ..., type: 3}`）。
   本项目改用 `[RuntimeInitializeOnLoadMethod]` 自动启动，完全绕开这条引用链。
3. 确实需要场景内引用脚本时，**在编辑器里拖拽赋值**，不要手写 YAML。

### 2. `new GameObject(name, typeof(...), ...)` 的组件冲突（编译器抓不到）

```csharp
// ✗ RectTransform 与 UI 组件重复：UI 组件会自动带上 RectTransform
new GameObject("X", typeof(Image), typeof(ScrollRect), typeof(RectTransform));
// ✓
new GameObject("X", typeof(Image), typeof(ScrollRect));

// ✗ 两个 Selectable 共存：Dropdown 自己就是一个 Button
new GameObject("Y", typeof(Image), typeof(Dropdown), typeof(Button));
// ✓
new GameObject("Y", typeof(Image), typeof(Dropdown));
```

这类错误只在运行时 `Debug.LogError` 然后丢弃组件。已由
`Tools/UiCompileCheck/check-components.ps1` 静态拦截（接在 `check-ui.ps1` 里自动跑）。

### 3. 没有 `EventSystem` → 界面不响应任何点击

uGUI 的**绘制**与**输入**是两件事：没有 `EventSystem` 时界面照常显示，
但点击、下拉框全部无效，**而且控制台没有任何报错**（非常难查）。

本项目入口靠自动启动、不依赖场景，所以 `GameBootstrap.EnsureEventSystem()`
会在启动时按需创建。本项目 `activeInputHandler = 0`（旧 Input Manager）
且未安装 `com.unity.inputsystem`，因此用 `StandaloneInputModule`。

### 4. `Hand` 的状态标记必须与"这只手现在的数值"保持一致

`ShieldState.Broken` 只应该出现在**值为 5** 的手上。曾经的写法是
"运算前破盾后无条件把状态设回 `Broken`"，结果 `5+3=8` 的手也挂着"碎盾展开"，
界面上会一直显示错误的碎盾标签。

规则（见 `RULES.md` §4）：

1. `Broken` 只可能出现在值为 5 的手上（`ResolveTurn` 第 3 步做值判断）。
2. **只要手值仍是 5，`Broken` 就一直保留，可以跨任意多个回合** —— 碎盾没有"过期"概念，
   战士随时都能合一面新盾把它修好。

**因此实现里刻意没有"按回合清理 `Broken`"的代码。** 我曾在这一点上反复改错：
先加了按回合清理，结果把修复窗口整个关掉，`OnShieldFormed` 永远看不到碎盾，
修复机制静默失效。**不要再加回合清理。**

### 5. "现在该谁做决定" ≠ "现在轮到谁行动"（选盾询问不出现的根因）

进入"选盾"阶段时，`ActivePlayer` 仍然是**攻击方**，但该做决定的是**防守方**。

UI 曾经用 `IsHumanTurn`（看行动方）来放行点击，于是"AI 攻击 / 人类防守"时
玩家的选盾点击被**静默丢弃**，表现为"双盾被直接破盾、没有询问"。

**规则：UI 只允许用 `BoardController.CanAct(hand)`**（= `IsHumanChooser && CanClick`）
决定手牌是否可点、以及点击是否放行。它已经可测，不要再自己拼两个条件。

### 6. 抉择器必须在创建 `GameManager` 时就传进去

`GameManager` 内部保存自己那份 `ChoiceProviderA/B` 引用。
只把 sink 交给 `BoardController` 是不够的 —— `GameManager` 那侧仍是 `null`，
`AttackContext` 会退化成"用第一面盾"，表现为**总是破左手盾**。

正确写法：`CharacterFactory.CreateMatch(defA, defB, choiceA, choiceB)`，
同一个对象同时充当 choice provider 与 sink（`SimpleChoiceProvider`）。
另外**必须给两个玩家各传一个 sink**（`BoardController(game, sinkA, sinkB)`）。

### 7. 角色抉择不要写 `is XXXLogic` 硬编码

`BoardController` 曾经用 `ActivePlayer.Character is BarbarianLogic` 判断"要不要问锁手"。
每加一个带抉择的角色（牧师回血目标、游侠闪避/强化）都要改控制器 —— 不可扩展。

现在改为角色自己实现 `IPreTurnChoiceProvider` 声明抉择，控制器只遍历、只负责问。
详见 `RULES.md` §15.4。

**推论：运算前必须收集齐所有抉择。** `GameManager.ExecuteTurn` 一次跑完，无法暂停，
所以"回合中途弹选项"是做不到的。

### 8. `PreTurnChoiceReader.Read` 必须带"回退询问"

顺序是"**先回读 sink，没有记录则当场询问抉择器**"。
只回读 sink 会让所有直接调 `GameManager.ExecuteTurn` 的场景（测试、脚本对局）
里角色抉择静默失效 —— 野蛮人锁手曾因此**全部失灵**，而且不报任何错。

### 9. 运算源的手值在结算钩子里已经是"运算后"的值

`GameManager.ExecuteTurn` 先 `PerformOperation`（改写了运算源），之后才调用
`OnResolveAttack` / `TryDodge` / `OnOperationResolved`。

所以钩子里想判断"运算**前**手里有没有 2"这类条件，必须用
`AttackContext.SourceHandValueBefore` / 钩子的 `actedHandValueBefore` 参数，
**不能**读当前手值。（游侠强化射击曾因此永远无法发动。）

### 10. `AttackContext` 必须保存"运算前的双方手值快照"

`GameManager.ExecuteTurn` **先** `PerformOperation`（改写运算源），**之后**才调用
`AttackContext.Resolve`。所以在 `Resolve` 里已经**无法**由手值反推这次运算的效果。

我在这里栽过一次：`Resolve` 里构造闪避判定用的 `PreTurnChoiceContext` 时把
**运算源当成目标手**传了两遍：

```csharp
new PreTurnChoiceContext(Attacker, Defender, SourceHand, SourceHand)  // ✗ 目标手传错
```

于是 `ComputeSum(1, 1) = 2` → `Effect = None` → "不是攻击" → **游侠的闪避被整个跳过**，
表现为"被攻击时有 1 却卡住不弹选项"。

正确做法：`GameManager` 注入 `SourceHandValueBefore` / `TargetHandValueBefore` 两个快照，
`PreTurnChoiceContext.Effect` 优先用快照重算。

**推论：任何"运算前/运算后"口径的判断都要显式确认用的是哪个值。**
游侠强化射击（要运算前的 2）和狼人双手和（要运算后的和）就是两种相反的用法。

### 11. "该谁决定"要跟着抉择走，不能一律用行动方

`BoardController.Chooser` 决定 UI 放行谁的手牌点击。角色的抉择**不一定属于行动方**：

- 游侠的闪避属于**防守方**（此时 `ActivePlayer` 仍是攻击方）；
- 野蛮人的锁手属于行动方。

所以 `ChoosingPreTurnChoice` 阶段必须返回收集时定下的 `_pendingChoiceDecider`。
这里曾经返回 `ActivePlayer`，导致**游侠被攻击时闪避选项点不到**（卡住）。

### 12. 询问顺序必须与结算顺序一致（闪避要先于护盾）

`AttackContext.Resolve` 里闪避**优先于**护盾，所以"问玩家"的顺序也必须是
**先闪避、再选盾**。

`BoardController.CommitMove` 曾经先问选盾、问完直接 `ExecuteTurn`，
于是"有双盾的游侠"**问不到闪避** —— 表现为"优先碎盾而不是询问是否闪避"。

现在：`CommitMove` → `BeginCharacterChoices`（角色抉择）→ 全部问完 →
`BeginShieldChoiceIfNeeded`（选盾）→ `ExecuteTurn`。

**测试时注意**：要区分这个顺序，防守方必须**有双盾**。
单盾时"选盾阶段"本来就会被跳过，测试测不出顺序（我第一版测试就踩了这个坑）。

### 13. 判定"某个效果能不能触发"要看**来源**，不要看数值

游侠的闪避不能对咒术师生效。我最初写成"伤害是 8 就不能闪避"，理由是
"咒术师的无视护盾攻击必然是 8" —— 这个前提**是错的**：咒术师照样能打出 4 / 6。
后果是**其他角色打出 8 时游侠也无法闪避**。

正确做法：让角色自己声明，游侠不认识咒术师：

```csharp
public interface IDodgeBlocker { bool PreventsDodge { get; } }   // 咒术师实现
// 游侠侧：
var blocker = context.Actor?.Character as IDodgeBlocker;
return blocker == null || !blocker.PreventsDodge;
```

**推论：凡是"某种能力对某种角色无效"，都应该按角色身份判定，
而不是按某个数值/效果去间接推断。**

### 14. 触发条件要看清是"运算源"还是"另一只手"

游侠的强化射击规则是"用一只手发起攻击后，若**另一只手**为 2"。
我误实现成"**运算源**为 2"，于是永远无法触发 ——
因为运算源正是攻击手，它的值是 4 / 6 / 8，不可能是 2。

**推论：规则里出现"另一只手 / 双手 / 该手"这类措辞时，
先明确指的是哪一只，再去写判定。** 这个项目里"运算源"和"另一只手"
在结算钩子里的可读性完全不同（运算源已被改写，另一只手未变）。

### 15. 多段攻击：攻防加成**只能叠加一次**

加忍者的"两段伤害"时，我把攻防加成算进了 `FinalDamage`，又在逐段结算时算了一遍，
于是**加成被叠加两次** —— 6 个既有测试（战士/野蛮人/咒术师的攻防数值）同时失败。

正确做法：`FinalDamage` 只记录"经倍率、**未叠加攻防**"的伤害，
逐段循环里每段各自调用一次 `ApplyStatBonuses`。

**推论：改动共享的伤害结算路径后，一定要跑全量测试** ——
这次正是既有测试立刻抓到了它。

### 16. "运算前"还是"运算后"：收集阶段与结算阶段看到的值不同

剑客的触发条件是"运算源运算后的值 == 另一只手"，但**收集抉择时运算还没执行**，
`ActedHand.Value` 仍是运算前的值（5+1=6 时它还是 5）。用它去比会永远判不出"双 6"。

所以收集阶段必须用**运算结果**（`PreTurnChoiceContext.ResultValue`）。
这与第 9 条（结算阶段运算源已被改写）正好是两个相反方向的坑。

### 17. 免伤通道被复用时，战报要说清"是哪一种免伤"

刺客的潜行免伤复用了游侠的闪避通道（语义一致：完全免伤、不碰护盾），
于是战报把"打不到潜行的刺客"显示成"**被游侠闪避**"。

修法：在 `PreTurnChoiceContext` / `AttackContext` / `TurnResult` 上带一个
`DodgeReason`，`DescribeTurn` 有原因就用原因、没有才用默认文案
（"被游侠闪避"）。

**推论：复用一条机制时，要检查所有面向玩家的输出是否还说得通。**

### 18. 只有一个候选的抉择要给"发动"按钮

剑客的追加攻击、游侠的强化射击都只有唯一一个候选。
让玩家去点那张手牌既不直观、也容易找不到（用户反馈"只有不发动选项"）。

现在 `ChoiceRequest.AcceptLabel` 非空时，UI 会在"不发动"**左侧**显示一个发动按钮，
`BoardController.AcceptPendingChoice()` 负责应答。

### 19. 角色规则有歧义时**先问，不要猜**

这个项目加角色时，规则描述经常有歧义（"另一只手"指哪只、"运算前还是运算后"、
"是否叠加"、"能否放弃"）。我多次先按自己的推断实现，然后再被纠正，
来回改了好几趟（牧师的触发时机与叠加、游侠的运算源/另一只手、
剑客的运算前/运算后、刺客的跳过机会复位）。

**代价不只是返工**：我曾经"推断"出"运算后判定双 9 不可达"这个结论，
并据此改了实现 —— 而那个推断本身是错的（运算源运算前可以是 8，所以完全可达）。
错误的推断比没有推断更危险，因为它会伪装成"已验证的结论"写进注释和文档。

**做法：拿不准就直接问，一次问清；已推断的地方要在文档里标注"这是推断"。**

### 20. "可点"不等于"高亮"：手牌边框要区分两种状态

手牌边框原本把"**可点**"也当作高亮条件之一。但"可点"会持续**整整一个回合**，
于是游侠的 1 闪避成 2、2 强化成 3 之后**手牌一直亮着**（真实 bug）。

现在 `HandVisual.OutlineState` 分三档：
`Hidden`（不可点）/ `Available`（可点，但不是当前抉择的候选）/ `Highlighted`（候选或已选中）。

**推论：视觉状态要挂在"当前这一步的有效选择"上，不能挂在"这个控件能不能点"上。**

### 21. 提示文案里的数值必须和实际结算同源

剑客的追加提示原本用 `context.ActedHand.Value` 显示伤害值，
但收集抉择时运算**还没执行**，那个值还是运算前的
（5 + 1 = 6 时它显示"双 5、固定 5 点"，实际却是双 6）。

这和触发条件的 bug 是**同一个根因**：收集阶段只能看 `context.ResultValue`。
现在提示与结算都用它，并加了测试逐个核对 4 / 6 / 8。

### 22. 动画要抽成"纯数据计划"，不要直接写进 MonoBehaviour

UI 的 `Refresh(...)` 是**无状态重绘**：给它当前状态，它画出来。
动画需要的是"**从 A 到 B 的过程**"，两者结构不兼容 ——
直接往 `Refresh` 里塞协程，`GameBoardUI` 会迅速变成一团。

正确做法（已落地）：`AnimationDirector.Build(TurnResult, defender)` 生成
**纯数据**的 `AnimationPlan`（"该飘什么字、飘在谁头上、间隔多久"），
UI 只负责"按计划播"。好处是**动画逻辑可以被无头测试覆盖** ——
UI 外观看不到，但"飘字内容与顺序"可以精确断言。

同理，落点换算 `CanvasLocalToAnchored` 也放在 Assistant 层（纯浮点），
UI 只做一层薄包装。原因：**测试工程在无头环境编译，用不了 UnityEngine 类型**。

### 23. 可变字体（Variable Font）在引擎里只会取默认实例

界面中文一直偏细。原因是 `NotoSansSC-Regular.ttf` 是**可变字体**（有 `fvar` 表），
而 Unity 的旧版动态字体管线**不支持选择可变轴**，只取默认实例 —— 当时默认是 Thin。

本机装不上 `fonttools`、网络也不可达，无法把可变字体实例化为静态字重，
所以改用上游发布的**静态 OTF**（Noto Sans CJK SC Medium / Bold）。

**换字体时必须确认目标文件没有 `fvar` 表。**
另外 `.ttc`（字体集合）`Resources.Load<Font>` 读不了，不要用。

顺带修掉一个隐藏问题：原来 7 处 `fontStyle = FontStyle.Bold` 是让引擎
**伪粗体合成**（把 Medium 机械加粗，发糊）。现在用真 Bold 字体。

### 24. UI 的计算逻辑放到 Assistant 层，UI 只做薄包装

血条的比例与颜色、飘字的落点换算，算法都放在 `AnimationDirector`
（**不依赖 UnityEngine**），`GameBoardUI` 里只留一层把 `Rect`/`Vector2`
拆开再拼回去的包装。

**原因**：测试工程在无头环境编译，**引用不到 UnityEngine**。
算法留在 UI 层就等于无法测试 —— 而"血条比例会不会溢出"、
"飘字落点对不对"恰恰是容易写错、又值得验证的地方。

判断标准：**能用 float/int 表达的算式，就别让它碰 UnityEngine 类型。**

### 25. 回合制里没有"动作"可以冻 —— "停顿"只能是回合间延迟

动作游戏的 hit stop 是"命中瞬间冻结所有动作"。
本项目**伤害是瞬间算完的**（`ExecuteTurn` 一次跑完整回合），没有持续动作可冻，
所以"停顿"只能落在"回合间延迟"上：结算后先让震屏与飘字播完，再进下一回合。

**逐段停顿做不到**：引擎一次算完整个回合，中途停不下来。
多段攻击的"一下一下"是靠**把表现按段错开**（飘字/震屏各 0.25s）模拟的。

推论：想真正"逐段停顿"，必须把 `ExecuteTurn` 改成可重入的状态机 ——
改动很大，收益有限，目前不做。

### 26. 屏幕震动必须还原位置

`ScreenShaker` 通过偏移 Canvas 根的 `anchoredPosition` 实现。
**结束时、`OnDisable` 时、退出对局时都必须还原**，
否则画面会永久偏掉（玩家看不出原因，只会觉得"界面歪了"）。
`ReturnToSelection` 里也调了 `StopAnimations()` 清一遍。

### 27. AI 不认识角色：让角色自己报"这一手值多少"

早期 `GreedyChoiceProvider` 只认识白板 / 战士，六个新角色的技能**一个都不会用**：
牧师会把 9 回给对手、游侠永远不闪避、狼人从不凑 7。

修法**不是**让 AI 引用 `WerewolfLogic` / `SwordsmanLogic`
（Assistant 层按约定不得引用 Characters 层），而是反转依赖：
角色实现 `IAiMoveAdvisor.ScoreMoveBonus(MoveValueContext)`，自己声明这一手值多少分。

**"AI 判断角色"要改成"角色告诉 AI"** —— 依赖方向才不会被破坏。

### 28. 高亮不能重复叠加

`ExtraHighlights()` 曾在 `ChoosingPreTurnChoice` 阶段把 `ChoiceCandidates`
**又**返回一次当"额外高亮"，而主高亮已经在亮同一批牌。
叠加结果是没有独立的生命周期 —— 技能发动后仍留着一层亮色（实测：牧师 / 游侠高亮不消失）。

**教训**：同一批视觉状态只应由一个来源设置。
要表达"额外高亮"就得给它独立的进入 / 退出时机，不能复用同一份数据。

### 29. 音效也用"纯数据参数 + 播放层"两层

`SoundDirector` 只产出 `SfxSpec`（频率 / 时长 / 音量 / 延迟），
`SfxPlayer` 才去合成 `AudioClip` 并播放。

好处有三：
1. **不需要任何音频资源文件**（方波 + 指数衰减包络现场合成，无授权问题）；
2. "该响哪个音、音高多少、节奏对不对"是纯数据，**可被无头测试覆盖**；
3. 以后想换成真实音效，只替换 `SfxPlayer`，参数层不动。

⚠️ 音效用**查表**而不是公式定音量：公式版（0.35 + 0.20 × 档位）会给出 0.95，
乘上主音量后逼近削波 —— 重击会"炸"而不是"沉"。现在上限 0.68，并加了防削波测试。

### 30. 界面文案也要放在可测层

角色卡的身材摘要与技能摘要原本写在 `CharacterRosterView`（UI 层），
而**测试工程引用不到 UI 命名空间**，于是无法验证。

移到 `CharacterCardText`（Assistant 层）后可以测 —— 并且立刻抓到一个真问题：
**狼人的 +5 生命上限不在数据层**（由 `WerewolfLogic.MaxHealthBonus` 提供），
界面若直接用 `CharacterDefinition.MaxHealth` 会显示 20，
和实战的 25 不一致，玩家会以为卡片在骗人。
现在用 `CharacterFactory.MaxHealthOf(def)`，并有测试保证"卡片上限 == 实际建出的玩家上限"。

### 31. 界面分屏要用"分组 + 一次切换"，不要零散 SetActive

界面从"一个角色选择页"发展成"主菜单 → 1v1 子菜单 → 角色选择"之后，
零散的 `SetActive` 很容易漏掉某个元素，导致上一层残留。
用 `ScreenGroup` 登记每屏的节点后，切屏就是 `ShowScreen(screen)` 一句话。
**并且必须在所有分组建好之后才调用第一次 `ShowScreen`**，否则未隐藏的分组会一起显示。

### 32. 规则文案要和引擎"同源"，并逐条对照测试

规则说明写错比代码写错更误导玩家，所以：
- 攻击点数 / 护盾值**从 `Hand.GetEffectType` 推导**，不在文案里写死；
- 生命上限用 `CharacterFactory.MaxHealthOf`（狼人的 +5 不在数据层）；
- `RuleBookTests` 逐条对照引擎：和 10 变 1、攻击值、护盾值、
  回合上限、狼人 25 血、各角色逻辑类的关键常量。

### 33. `RectMask2D` 会自动加 `RectTransform`，不要重复声明

`new GameObject("x", typeof(RectTransform), typeof(RectMask2D))` 会触发
"RectTransform 重复"的**运行时**冲突（编辑器不报、进 Play 才炸）。
`check-ui.ps1` 能静态查出这类冲突，**改 UI 后一定要跑**。

### 34. 界面坐标要机器校验，不能靠肉眼

元素重叠**连续出现三次**（玩家A标题被卡片压住、选择角色标题与"玩家角色"叠住、
主菜单副标题压住大标题），每次都是玩家截图才发现。

原因：`MakeText` 的 `sizeDelta` 是**整个文本框**的尺寸。
标题字号 48、文本框高 70，而字体行高约 1.2~1.4 倍字号 —— 实际墨迹接近框高。
所以"中心差 > 半个框高之和"才是真安全，凭感觉估一定出错。

`LayoutSpacingTests` 把每行的 `[下边界..上边界]` 算出来，断言：
① 从上到下**不重叠**；② 相邻至少 8px 间隙；③ 整体不超出画布。
改坐标就必须同步改测试 —— 这正是它的价值：**逼着重算一遍间距**。

⚠️ 写这类测试时，断言数组**必须按 y 递减**（上 → 下）排列。
把它和内联顺序写错会让断言反过来比较，报出"下面的压住上面的"这种反直觉消息。

### 35. ScrollRect 的 viewport 必须有参与射线检测的 Graphic

规则弹窗"点进去拖不动"，根因：Viewport 上只有 `RectMask2D` + `ScrollRect`，
**没有任何 Graphic**。`ScrollRect` 靠**射线检测**接收拖动事件，
该区域没有 Graphic 时事件根本到不了它，于是拖动完全无效。

修法：给 Viewport 挂一个 **Image**（颜色 `a=0` 全透明即可，仍参与射线检测），
作为"看不见的拖动热区"。正文 `Text` 保持 `raycastTarget = false`，避免它抢走事件。

### 36. 标题"像两段字"要同时收投影与描边

`TitleArt` 是"投影 + 8 向描边 + 本体 + 高光"叠出来的。
投影从 (6,-7) → (3,-3) 后**仍显得分离**，因为除了投影，
**描边（2~3px）也把整体边界往外扩了一圈** —— 两者叠加才造成"两段字"的观感。

所以这类"厚描边 + 投影"的标题，要**同时**收敛两者：
投影 2px、描边 1.5px（对角 1px）。只收一个没用。

### 37. 角色的抉择绝不能由控制器再生成一遍

**玩家实测 bug**：游侠对战士翻滚后无法强化。

根因：`BoardController.AppendDodgeChoices` 与游侠自己的
`RangerLogic.AddDodgeRequests` **各生成了一个闪避询问**。
于是同一次单段攻击出现两个闪避槽位（variant 0 与 1），
而这一击实际只有 `hitIndex = 0` 一段 —— 真正被读取的槽位答不上来，
技能表现为"发不动"。

**教训**：我在做"每段各问一次"时，忘了角色**本来就会**通过
`IPreTurnChoiceProvider.CollectPreTurnChoices` 提出自己的抉择。
控制器再生成一遍就是重复。

**规则**：抉择的**生成**属于角色（它最清楚自己的条件），
控制器只负责**编排与推进**。发现"某个角色的抉择"出现在控制器里，就是重复的信号。

排查手法：`BoardController.PendingRequests`（只读）能打印整个待答队列 ——
只看 `PendingRequest`（队首）是发现不了重复的。

### 38. 按钮文案必须与行为一致

游戏内右上角按钮文案写着"退出游戏"，实际只回标题 ——
玩家实测反馈"按退出游戏直接关闭游戏了"（预期与行为不符引发的误解）。

现在：**对局中 = "返回标题"**，**真正结束进程的"退出游戏"只在主菜单提供**。

### 39. 回合边界必须复位"抉择队列"与"队列索引"

**玩家实测 bug**：游侠接受过一次翻滚之后，**后续回合的强化提示再也不弹**
（提示条里连文字都没有）。

根因：`SetupFirstStep()`（每个新回合开始时调用）只清了 `_shieldChoices`，
**没有复位 `_pendingRequestIndex`**。

完整链条：
```
回合 N：接受闪避 -> _pendingRequestIndex++ -> 变成 1
        结算完 -> BeginTurn -> SetupFirstStep（idx 仍是 1）
回合 N+1：CommitMove -> BeginCharacterChoices
        清空队列 + 加入强化请求（长度 1）
        但 idx 仍是 1 -> `1 >= 1` 被判成"全部问完" -> 直接结算
        ⇒ 提示不弹
```

**在回合边界上，任何"有索引 + 有集合"的成对状态都必须一起复位。**
只清集合不清索引，就会出现"索引越界 → 静默跳过整段逻辑"这种最难查的错。

⚠️ **定位过程值得记住**：单回合测试全部通过 ——
因为它们都是"新建 board、只问一次"，天然看不到跨回合残留。
真正定位靠的是**实机诊断日志里的 `idx=1/0`**（索引指向了不存在的元素）。
**无法复现时，先加日志让用户上报现场，不要连续猜。**

### 40. 共用画布绝不能被 SetActive(false)

**玩家实测 bug**：对局中"返回标题"或结算后"再来一局"，**整个 UI 消失**，
只剩一片纯色（相机背景），看起来像游戏崩了。

根因：`OnStartClicked()` 把 `_selectRoot`（主菜单 / 1v1 / 角色选择**共用的画布**）
整个 `SetActive(false)`，而 `ShowScreen()` 只切分组显隐、**从不重新激活画布** ——
于是再也回不来。

两条修法：
1. `OnStartClicked` 不再碰画布 —— 隐藏界面是 `ShowScreen` 的职责；
2. `ShowScreen` 加保险：要显示菜单类界面时，若画布未激活就先激活。

`ScreenFlowTests` 用**源码守卫**钉住这条：禁止源码里出现
`_selectRoot.gameObject.SetActive(false)`，并检查 `ShowScreen` 里有激活保险。
（已验证：重新引入该行会让守卫测试 FAIL。）

**通用教训**：多个界面共用的容器，其"激活状态"不能由某一个界面的进入流程去改 ——
否则离开那个界面后没人负责恢复。要么统一由切屏函数管理，要么加保险。

### 41. 「选定参与运算的手」与「确认攻击目标」必须分两步

**规则**（玩家定义）：
- 运算要用**两只手**相加，所以点对方的手仍然是"选加数"；
- 但**挨打的是玩家，不是手**。手牌只是"指定目标"的载体；
- 多人模式下会有多个可选目标，所以**1v1 也必须点这一步** ——
  否则多人上线后流程会变、玩家要重新学。

**流程**：
```
① 点自己的手          -> ChoosingTargetHand
② 点对方的手（加数）   -> ChoosingTargetPlayer   ← 新增，记 TargetPlayer
③ 点目标玩家的手牌确认 -> CommitMove -> 抉择 / 选盾 / 结算
   没有合法目标时（刺客潜行）-> 右侧只给「放弃攻击」
```

`ConfirmTargetPlayer()` 是**公开的显式一步**（UI 与测试都能调用），
它不在该阶段时返回 false —— 因此**幂等安全**，可以在每次点击后无条件调用。

**「放弃攻击」= 算用掉本回合**（`GameManager.GiveUpTurn()`：
回合数 +1、换对方、手值不变）。它与 `SkipTurn()` 不同 ——
后者是角色技能（双盾/潜行）有前置条件，前者是玩家任何时候都能做的选择。

⚠️ **重构时的大坑**：这次改动同时影响产品代码和**约 26 个测试的点击序列**。
正确顺序是**先给测试加一个封装的辅助方法并批量替换点击**，
**再**改产品流程 —— 我第一轮反过来做（先改产品），被 26 个测试同时挡住而被迫回退。

### 42. 新增交互阶段时必须同步 4 张「按阶段分派」的表

新增一个 `BoardPhase` 值，**必须同时检查这些 `switch`**，
漏掉任何一个都会产生"卡住"级别的问题：

| 位置 | 作用 | 漏掉的后果 |
|---|---|---|
| `Chooser` | 这一步该谁做决定 | 返回 null -> `IsHumanChooser=false` -> **UI 丢弃玩家点击 -> 游戏卡死** |
| `CanClick` | 哪些手牌可点 | 玩家点不动，或点到不该点的 |
| `ClickHand` | 该阶段的点击处理 | 点击被忽略 |
| `DescribeHint` / UI 按钮可见性 | 提示与出口 | 没有可用按钮 |

**真实 bug**：玩家实测"战士打战士后**第一步就卡住**，显示 AI 正在思考"。
根因就是新增 `ChoosingTargetPlayer` 时漏了 `Chooser` 的分支。

**更重要的教训 —— 为什么测试没抓到**：我写的 6 个新流程测试都用
`ConfirmTargetPlayer()` **直接调用**，绕过了 `IsHumanChooser` 这道门。
所以要**断言门本身**（`Assert.True(board.IsHumanChooser)`），
而不只是断言门后面的行为。

### 43. 抉择可以分两段，但"跨阶段传答案"必须显式设计

**玩家要求的交互**：牧师的 4/6/8 要**先弹「攻击 / 回复」**，
选完**再**点手牌选目标。也就是同一次运算里有**两段抉择**，
中间隔着"选目标手 + 确认目标玩家"一整段流程。

**难点**：所有抉择都要在运算前收集（`ExecuteTurn` 一次跑完），
而第二段的问题**依赖第一段的答案**（选了回复才需要问"回复给谁"）。
更麻烦的是，两段之间会跑一次 `SetupFirstStep()` ——
它要清空"仅本回合有效"的抉择记录（否则上回合的旧答案会串进本回合）。

**我在这上面失败了约 90 轮**，前后踩了三个坑：

| # | 坑 | 表现 |
|---|---|---|
| 1 | 把答案**合并进一次抉择**（点哪只手 = 回给谁）绕开问题 | 玩家要的两段交互做不到 |
| 2 | 用会被中途清掉的容器存答案 | "选了回复却按攻击结算" |
| 3 | 用 `GetChoice` 读回答案 | 它套用回退值 -> "选了攻击"被误判成"选了回复" |

**最终成立的设计**（三件事缺一不可）：

1. **`IPreTurnChoiceSink.GetExplicitChoice`**
   只读**显式记录**的答案，**绝不套用回退值**。
   于是"选了攻击"（记录为 null）与"选了回复、默认目标是自己"（记录为一只手）
   能区分开。用 `GetChoice` 就区分不了。

2. **`GameManager.AnsweredLookup`**（由 `BoardController` 注入）
   让**结算侧**读同一份答案，而不是只靠 sink 回读。

3. **回合边界的清空只在"行动方真正变化"时发生**
   ```csharp
   // 表达意图本身，与调用顺序无关
   private bool IsTurnBoundary => !ReferenceEquals(_lastBoundaryPlayer, ActivePlayer);
   ```
   ⚠️ 曾用 `_pendingActedHand == null` —— 那是**脆弱的间接判据**：
   它是否成立取决于调用顺序（`CommitMove` 恰好在 `BeginCharacterChoices`
   之前赋值）。顺序一变，"回合中途"就被误判成"新回合"。

**还有一个重要的自我纠正**：我一度以为坑 3 是根因，
并写了一条"改回旧判据就该失败"的测试 —— 结果**旧判据也通过**。
说明那条诊断是错的。我没有硬说"测试验证了修复"，
而是核实了真实生效的 5 处改动，并按**意图**重写了判据。

**教训**：**不要用间接判据表达规则**。
"运算源为空"不是"这是新回合"的定义；
"行动方变了"才是。间接判据会在调用顺序变化时静默失效。

### 44. "确认目标"只在结果**指向别人**时才问

玩家实测反馈：战士 1 + 1 = 2（**无效果**）也被要求选目标，
"会出现很多无意义询问"。

**规则**：只有运算结果会产生**指向别人的效果**时才让玩家确认目标：

| 结果 | 问？ | 原因 |
|---|---|---|
| 4 / 6 / 8 | ✅ | 攻击，指向对手 |
| 9 | ❌ | **默认自动回自己**（只有牧师例外） |
| 护盾 5 | ❌ | 作用于自己 |
| 1 / 2 / 3 / 7 | ❌ | 无效果 |

**能改写回复目标的角色才为 9 提问** —— 目前只有牧师
（实现了 `IHealOverride`，可以把回复给对手）。
玩家原话："除了牧师外，其余角色运算出 9 也是自动回复，无需询问。"

实现走 `IResultTargetProvider`（角色自己声明），
与"角色抉择"用的是同一套思路：控制器不认识任何具体角色。

⚠️ **兜底的方向很重要**：`Character` 为 null 时（测试里直接
`new PlayerState("A")` 就会这样）要退回基础规则（攻击需要目标），
但**不能把回复也算进去**。我第一版兜底写成了 `return false`，
结果**攻击被静默跳过确认步骤** —— 是失败的测试把它逼出来的。

### 45. 「放弃攻击」不能撤销运算结果

**玩家实测 bug**："点击放弃攻击会导致运算失效，如 1+3 运算出 4
点击放弃攻击会导致手回退至 1，这是不对的，应保留为 4。"

**根因**：`GiveUpAttack` 原本直接调 `GameManager.GiveUpTurn()` ——
那只是"回合数 +1、换对方"，**整个结算没跑**，于是手值停在运算前。

**修法**：让结算**照常跑**，只在**分派攻击时短路**：

```csharp
_game.SuppressAttackDamage = _giveUpAttack;   // 结算前设
result = _game.ExecuteTurn(...);              // 手值照常改变
// GameManager 内：
if (SuppressAttackDamage) { result.DamageDealt = 0; break; }
```

**通用教训**：需要"只取消效果、保留状态变化"时，
要**短路那一步效果**，而不是**跳过整个流程** ——
跳过流程会连带丢掉流程里的状态变更。

同时：**「放弃攻击」的判据是"有没有合法的攻击目标"**，
而不是"对手是不是刺客"。

玩家原话："应该明确放弃攻击按钮是在没有合法的攻击目标时弹出，
刺客潜行是让自己无法成为攻击目标，即敌方运算出攻击数字无法点击，
在没有引入多人模式可能的攻击距离前，1v1 只有刺客会导致没有合法攻击目标。"

所以抽象成 `CanBeAttacked(玩家)` → `HasAnyLegalTarget`：
- 角色通过 `IAttackTargetRule` 声明自己**免疫**（刺客潜行时）；
- 界面只问"有没有合法目标"，日后的"攻击距离不足"加在同一处，
  **界面无需改动**。

⚠️ 我第一版把判据写成了"对手是不是刺客"（而且错用了 `IDodgeBlocker` ——
那是"能不能闪避"，刺客并不实现它），
导致按钮在该出现时**永远不出现**。两次都是
"存在某个看起来相关的字段就以为它是那个意思"。

**通用教训**：判据要写成**问题本身**（"有没有合法目标"），
不要写成**当前唯一的原因**（"对手是不是刺客"）——
后者在加入新原因时必然要重写。

### 46. AI 的抉择器不能和"记录玩家点击的 sink"混为一谈

**玩家实测 bug**：AI 使用新角色时**从不发动任何技能**
（AI 游侠有 1 不闪避、有 2 不强化、剑客不追加……）。

根因：`BoardController.GetChoice(player)` **无条件**返回 `_choiceA / _choiceB`，
而它们是从 **sink** 反查出来的（UI 里是 `SimpleChoiceProvider` ——
只负责**记录**玩家点了什么，没有任何策略）。
AI 真正的策略在 `IMoveSelector`（`GreedyChoiceProvider`）里，两者是**不同的对象**。

于是 AI 的抉择被交给了一个"永远返回 null"的对象 → 判定非法 → 放弃发动。

**实机诊断日志里的铁证**（一行就定位了）：
```
AI作答 RangerDodge(v0) provider=SimpleChoiceProvider pick=null/非法 cands=1
                                         ^^^^^^^^^^^^^^^^^^^ 应该是 GreedyChoiceProvider
```

修法：`GetChoice` 里 **AI 玩家优先用它的 `IMoveSelector`**
（`GreedyChoiceProvider` 同时实现 `IChoiceProvider`）；人类玩家才用 sink。

**通用教训**：`sink`（记录）与 `provider`（决策）是两个职责。
从 sink 反查 provider 只在"人类玩家"成立；一旦某方是 AI，
决策者就是另一个对象 —— 不能混用。

### 47. 诊断信息不能放进"会被业务逻辑清空"的容器

排查这个 bug 时我犯过两次**同类**错误，都导致误判：

1. 把 AI 作答记录放进 `_aiTrace`，而它在 `SetupFirstStep()`（每个新回合开始）
   被 `Clear()` —— 作答时机正好在回合边界之前，于是**永远看不到记录**，
   我据此误判成"AI 根本没被问到"。
2. 用字符串替换改代码，**换行符不匹配导致替换静默失败**，
   我却当作成功继续，于是一整轮都在错误前提上分析。

**规矩**：
- 诊断数据放在**不受业务清理**的地方，或直接输出（Console）；
- **每次用脚本改完代码，立刻用 grep 确认改动生效**，不要凭"命令没报错"就相信。

### 48. PowerShell 脚本必须 UTF-8 with BOM

Windows PowerShell 5.1 在没有 BOM 时会按 GBK 解码，中文注释被读成乱码并抛出
"Missing closing '}'" 之类的**假语法错误**。用编辑器改过 `Tools/**/*.ps1` 后请确认 BOM：

```powershell
$p = 'Tools/UiCompileCheck/check-ui.ps1'
$t = [IO.File]::ReadAllText($p, (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($true)))
```

## Roadmap

**P0（已完成）** 规则内核可信化：测试固化规则、补齐 `ResetMatch` 复位链路、
角色数值数据化、增加可跑完整局的驱动器。

**P1（进行中）** 让游戏可交互。
- ✅ `BoardController` 交互状态机（无头可测）+ `GameBootstrap` + `GameBoardUI` + `Game.scene`。
- ⬜ **实机验证**：在编辑器里跑一次，确认布局、点击、文字都正常。
- ⬜ **中文字体**：导入中文 TTF 并接进 `LoadFont()`（当前中文是方块）。
- ⬜ 血条、结算动画、伤害飘字。
- ⬜ 把战士连锁破盾 / 野蛮人锁手也交给玩家选择（目前自动决定）。
- ⬜ 正式的角色选择界面。

**P2（体验与内容）** 美术资源（手部/护盾）、音效、胜负结算画面、
平衡性迭代（用 `MatchRunner` 批量跑对局统计胜率）、扩展角色、教学模式、难度 AI。

**P3（可选）** 异步 PvP 联网（工程里已有 `UnityTcp` 包，但成本高，建议最后做）。

## 已知风险

1. **平衡**：单回合最大伤害 8 < 回血量 9，双方都倾向回复时理论上可无限拖。
   已决策"不改规则，仅靠 AI 策略规避"，详见 `RULES.md` 第 13–14 节。
   真人对战若出现僵局，再回来讨论。
2. **无法自动化验证表现层**：编辑器批处理崩溃，UI 只能靠人工在编辑器里点。
3. **字体字重未验证**：`NotoSansSC-Regular.ttf` 其实是**可变字体**（默认实例偏细，
   PostScript 名为 `NotoSansSC-Thin`），Unity 旧版动态字体管线可能渲染成 Thin 字重。
   本机 `pip` 装不上 fonttools，无法实例化静态字重。
   修法见 `Assets/Resources/Fonts/README-字体许可.txt`（用 fonttools 固定 `wght=400`，
   或换静态字重版本，或迁到 TextMeshPro 动态 SDF）。
   `UiFont.cs` 有三级回退（项目字体 → 内置 LegacyRuntime → Arial），所以即使字体
   加载失败界面也不会变空白。
