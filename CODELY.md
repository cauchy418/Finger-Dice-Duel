# CODELY.md

> 工程结构与开发约定。**规则权威文档是 [`RULES.md`](RULES.md)** ——
> 代码、测试、UI 都必须与它一致，改动规则先改那里。

## 项目概况

**Finger Dice Duel**（手指骰子对决）：2D 回合制对战。双方各两只手（初始 1），
每回合用自己的一只手与对方的一只手相加、取个位（和为 10 → 1），
结果数字决定效果：**4/6/8 攻击、9 回复、5 护盾**。生命 20，先归零者败。

| 项 | 值 |
|---|---|
| 引擎 | 团结引擎（Tuanjie，Unity 中国版）`2022.3.62t15` / Editor `1.10.3` |
| 语言 | C#（源文件 **UTF-8 with BOM**） |
| 渲染 / UI | 内置管线 + 正交相机；uGUI + TextMeshPro |
| 测试 | **452 项**无头测试，22 个夹具，秒级 |
| 美术 | 全部界面**运行时用代码构建**，无预制体 |

## 目录结构

| 路径 | 用途 |
|---|---|
| `Assets/Scripts/Core/` | 规则内核。**不依赖任何 Unity 类型** |
| `Assets/Scripts/Characters/` | 角色数据层（`CharacterDefinition`）+ 各角色技能 |
| `Assets/Scripts/Assistant/` | AI、测试替身、整局驱动、`BoardController` 交互状态机。**不依赖 Unity** |
| `Assets/Scripts/UI/` | 表现层（**唯一**允许引用 `UnityEngine` 的目录） |
| `Assets/Tests/EditMode/` | 无头测试；`UnityTestShim.cs` 是环境适配门面 |
| `Assets/Resources/Fonts/` | 思源黑体 Medium（正文）/ Bold（标题），SIL OFL 1.1 |
| `Tools/HeadlessTests/` | 无头测试运行器 |
| `Tools/UiCompileCheck/` | 表现层编译 + 组件冲突静态检查 |
| `Tools/push.ps1` | 一键推送（openssl 后端 + 本机代理 + 重试） |
| `Library/` `Temp/` `Logs/` `obj/` | 引擎生成物，**不要编辑或提交**，可安全删除 |

## 依赖方向（架构铁律）

```
UI  →  Assistant  →  Core
       Characters ↗
```

- `Core` / `Characters` / `Assistant` / `Tests` **一律不得引用 `UnityEngine`**
- `Assistant` **不得引用 `Characters`** —— 角色能力通过 `Core` 里的**接口**反向调用
  （`IDodgeCapability` / `IHealOverride` / `IAttackTargetRule` / `IResultTargetProvider` …）

**这套约束是"全部游戏逻辑都能秒级无头测试"的前提。** 破坏它 = 失去测试能力。

## 构建与验证

```powershell
# 逻辑测试（秒级，不需要编辑器）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1

# 校验 Unity/NUnit 侧能否编译
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS

# 表现层编译 + 组件冲突静态检查
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-ui.ps1
```

**改完必须把这三条都跑一遍。**

> ⚠️ 本机 `Tuanjie.exe` 批处理模式**会直接崩溃**（exit 140063，无输出），
> 所以无法用编辑器做自动化编译/测试。上面前两条是逻辑测试（**真跑**），
> 第三条只能保证表现层**能编译 + 无明显组件冲突** ——
> **界面效果、点击、字体渲染都无法在这台机器上验证**，必须实机确认。

**用编辑器看画面**：团结 Hub 打开本目录 → 直接 Play（任意场景都行）。
`GameBootstrap` 通过 `[RuntimeInitializeOnLoadMethod]` 自动创建并按需建 `EventSystem`，
**不需要场景配置、不需要预制体、不需要拖脚本**。

## 开发约定

- 🚫 **绝对不要手工创建/编辑 `.meta` 文件**（GUID 格式很特殊，见下）
- 源文件与 **PowerShell 脚本都必须是 UTF-8 with BOM**，否则中文会乱码并报语法错
- C#：类型/方法 PascalCase，命名空间统一 `FingerDiceDuel.*`
- **非法操作不要静默失败**：`GameManager.ExecuteTurn` 对不合法运算抛
  `InvalidOperationException`，便于 UI/AI 层尽早暴露 bug
- **改规则要同步三处**：`RULES.md`、代码、`Assets/Tests/EditMode` 的对应测试
- **不要修改** `Library/` `Temp/` `Logs/` `.codely.packages/`

---

## 踩过的坑

> 全部已修。按主题归类，"★" 是曾经**真实导致过玩家可见 bug** 的。

### 一、架构与分层

| # | 坑 | 要点 |
|---|---|---|
| ★1 | **AI 不认识角色** | AI 不能 `is XXXLogic` 判断；让角色通过接口自报"这一手值多少" |
| ★2 | **角色抉择不能由控制器再生成一遍** | 控制器只管遍历 `IPreTurnChoiceProvider`；生成两遍会让玩家看到重复询问 |
| 3 | **UI 的计算逻辑放 Assistant 层** | 坐标/配色/文案都放到可测层，UI 只做薄包装 |
| 4 | **界面文案也要放在可测层** | 否则文案错只能靠肉眼看出来 |
| ★5 | **sink（记录）与 provider（决策）是两个职责** | 从 sink 反查 provider 只对人类成立；AI 的决策者在 `IMoveSelector`（**不同对象**）。混用会让 AI 永远返回 null → **技能完全不发动** |
| 6 | **`Assistant` 不得引用 `Characters`** | 用 `Core` 的接口反转依赖；判定"目标能否被攻击"走 `IAttackTargetRule` 这类能力接口 |

### 二、抉择系统

| # | 坑 | 要点 |
|---|---|---|
| ★7 | **"该谁决定" ≠ "轮到谁行动"** | 游侠的闪避属于**防守方**；一律用行动方会让闪避选项点不到 |
| ★8 | **回合边界必须复位"抉择队列"与"队列索引"** | 只清队列不清索引 → `idx >= count` 被判成"问完了" → **下回合的提示完全不弹** |
| 9 | **抉择器必须在创建 `GameManager` 时就传进去** | 否则结算时找不到 provider |
| 10 | **`PreTurnChoiceReader.Read` 必须带"回退询问"** | 直连 `ExecuteTurn`（无收集步骤）时仍要能问出答案 |
| 11 | **询问顺序必须与结算顺序一致** | 闪避在结算里优先于护盾，问也必须先问闪避 |
| ★11b | **结算钩子里不能向玩家索要输入** | `ExecuteTurn` 一次跑完、无法暂停；在钩子里问 provider 的话**人类永远看不到弹窗**（AI 能当场答，所以表现为"有时生效"）。战士连锁破盾曾因此形同失效 —— 必须改成"运算前收集 + 结算回读" |
| 12 | **只有一个候选的抉择要给"发动"按钮** | 让玩家去点手牌很别扭 |
| ★13 | **分两段的抉择要显式设计"跨阶段传答案"** | 见下方详解 |
| ★14 | **新增交互阶段必须同步 4 张"按阶段分派"的表** | `Chooser` / `CanClick` / `ClickHand` / UI 按钮可见性。漏掉 `Chooser` → 返回 null → `IsHumanChooser=false` → **UI 丢弃点击 → 游戏卡死** |
| ★15 | **不能用间接判据表达规则** | 见下方详解 |
| ★16 | **`GetChoice` 会套用回退值，`GetExplicitChoice` 不会** | 要区分"选了攻击（null）"与"选了回复（一只手）"时必须用后者 |
| ★17 | **诊断信息不能放进"会被业务逻辑清空"的容器** | 否则看到"空"会误判成"没被调用" |
| ★18 | **改代码后要立刻 grep 验证改动生效** | 编辑器/脚本可能因换行符编码差异**静默替换失败** |

### 三、时序与状态

| # | 坑 | 要点 |
|---|---|---|
| ★19 | **运算源的"运算前"值在结算钩子里已经被改写** | 收集阶段与结算阶段看到的手值不同；用快照，别读当前值 |
| ★20 | **`AttackContext` 必须保存"运算前的双方手值快照"** | 否则效果判定会算错（曾把游侠闪避整个跳过） |
| 21 | **多段攻击：攻防加成只能叠加一次** | 每段各自算会造成重复加成 |
| 22 | **判定触发条件要看"来源"，不要看数值** | 咒术师"无视护盾"是**身份**属性；按数值判会误伤其他角色的 8 |
| 23 | **看清条件是"运算源"还是"另一只手"** | 剑客要求"另一只手与结果同值"，不是运算源 |
| 24 | **免伤通道复用时，战报要说清是哪一种** | 闪避 / 护盾 / 免疫都要能区分 |
| 25 | **`Hand` 的状态标记必须与"这只手现在的数值"一致** | 手值变了，护盾状态要跟着失效 |

### 四、交互与 UI

| # | 坑 | 要点 |
|---|---|---|
| ★26 | **共用画布绝不能被 `SetActive(false)`** | 没有任何地方会把它重新激活 → **整屏空白** |
| 27 | **界面分屏要用"分组 + 一次切换"** | 不要零散 `SetActive` |
| ★28 | **`RectMask2D` 会自动加 `RectTransform`** | 不要再显式声明，否则运行时组件冲突（编译器抓不到，用 `check-ui.ps1`） |
| ★29 | **填充条必须用点锚点** | 拉伸锚点下 `sizeDelta.x` 是**偏移量**，设 0 反而满宽（血条恒满的根因） |
| ★30 | **`ScrollRect` 的 viewport 必须有参与射线检测的 `Graphic`** | 否则拖不动（`Image` 透明度 0 即可） |
| ★31 | **界面坐标要机器校验，不能靠肉眼** | `LayoutSpacingTests` 用满框高度做重叠检查（字形墨迹 ≈ 框高） |
| 32 | **"可点"不等于"高亮"** | 手牌边框要区分"已选中"与"可点"两种状态；高亮不能重复叠加 |
| 33 | **按钮文案必须与行为一致** | 文案与行为不符比没有按钮更糟 |
| 34 | **提示文案里的数值必须和实际结算同源** | 否则玩家看到"回 4"实际回 6 |

### 五、表现层

| # | 坑 | 要点 |
|---|---|---|
| 35 | **动画要抽成"纯数据计划"** | `AnimationDirector` 只产出 `AnimationPlan`（数值），播放交给 UI，这样能测 |
| 36 | **音效也用"纯数据参数 + 播放层"两层** | `SoundDirector` 产出频率/时长/音量，`SfxPlayer` 负责合成播放 |
| 37 | **可变字体在引擎里只会取默认实例** | 需要 Medium/Bold 就放**两个静态字体文件** |
| 38 | **回合制里没有"动作"可以冻** | `ExecuteTurn` 一次性跑完整回合，"打击停顿"只能做成回合间延迟 |
| 39 | **屏幕震动必须还原位置** | 否则界面会永久偏移 |
| 40 | **标题"像两段字"要同时收投影与描边** | 只收一个仍有重影 |

---

### 详解：两个最容易再犯的坑

#### A. 分两段的抉择 —— "跨阶段传答案"必须显式设计

**需求**：牧师的 4/6/8 要**先弹「攻击 / 回复」**，选完**再**点手牌选目标。
也就是同一次运算里有**两段抉择**，中间隔着"选目标手 + 确认目标玩家"。

**难点**：所有抉择都要在运算前收集（`ExecuteTurn` 一次跑完），
第二段的问题**依赖第一段的答案**；而两段之间会跑一次 `SetupFirstStep()`，
它要清空"仅本回合有效"的抉择记录（否则上回合旧答案会串进本回合）。

**成立的设计（三件事缺一不可）**：

1. **`IPreTurnChoiceSink.GetExplicitChoice`** —— 只读**显式记录**，**绝不套用回退值**。
   于是"选了攻击"（记录为 null）与"选了回复、默认目标是自己"（记录为一只手）能区分开。
   **用 `GetChoice` 就区分不了**，会把攻击误判成回复。
2. **`GameManager.AnsweredLookup`**（由 `BoardController` 注入）——
   让**结算侧**读同一份答案，而不是只靠 sink 回读。
3. **回合边界的清空只在"行动方真正变化"时发生**（见 B）。

#### B. 不要用间接判据表达规则

```csharp
// ❌ 间接：是否成立取决于调用顺序
private bool IsTurnBoundary => _pendingActedHand == null;

// ✅ 意图本身：与调用顺序无关
private bool IsTurnBoundary => !ReferenceEquals(_lastBoundaryPlayer, ActivePlayer);
```

"运算源为空"**不是**"这是新回合"的定义 —— 它只是恰好在旧调用顺序下等价。
顺序一变（例如某个校准步骤先给 `_pendingActedHand` 赋值），
"回合中途"就会被误判成"新回合"，把同回合刚答的抉择清掉，
表现为"牧师选了回复却按攻击结算"。

**通用规则**：判据要写成**问题本身**（"行动方变了吗"），
不要写成**当前唯一的间接表现**（"运算源为空"）。

#### C. 组件冲突（编译器抓不到）

`new GameObject(name, typeof(A), typeof(B))` 里如果两个组件都要求
"同一物体上不能有另一个"，运行时会报错但**编译能过**。
`Tools/UiCompileCheck/check-components.ps1` 就是静态抓这一类。

#### D. GUID 格式陷阱（最坑的一个）

团结引擎**不使用标准 GUID**：

| 资产类型 | GUID 格式 |
|---|---|
| `.cs.meta`（`MonoImporter`） | **41 字节**（base64 后 56 字符）：头部 + 16 字节真实 GUID + 类型码 + 签名 |
| `.scene.meta` | **32 位纯 hex**（`^[0-9a-f]{32}$`） |
| 目录 `.meta` | 41 字节 base64（与脚本同族） |

**结论：绝对不要手工创建/编辑 `.meta` 文件，也不要手写场景里的脚本 GUID 引用。**
入口靠 `[RuntimeInitializeOnLoadMethod]` 自动启动，正是为了避开这条。

---

## Roadmap

- [ ] **实机验证表现层**（本机无法自动化，只能人工看）
- [ ] 2v2 / 8 人身份 / 竞技（联网）—— 目前是"敬请期待"占位
- [ ] 菜单按钮音效
- [ ] 角色卡片列表的滚动提示（最后一张被右边缘切掉）
- [ ] 关闭 `DiagnoseChoices` 诊断日志（目前硬开，每帧刷屏）

## 已知风险 / 技术债

| 项 | 说明 |
|---|---|
| **表现层无法自动验证** | `Tuanjie.exe` 批处理崩溃，UI 效果只能人工确认 |
| **平衡隐患** | 单回合最大伤害 8 < 一次回复 9，双方都只顾活命时可拖延；靠 50 回合上限兜底（见 `RULES.md` §18） |
| **AI 给队友回血未实现** | 1v1 里给对手回血是资敌；等多人玩法再写 |
| **`DiagnoseChoices` 硬开** | 定位问题时打开的诊断日志还没关 |
