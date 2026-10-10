# Finger Dice Duel — 规则规格与实现契约

本文件是**唯一权威规则来源**。代码、测试、UI 必须与本文件一致；若发现不一致，
以本文件为准并修正代码。

---

## 1. 基础设定

| 项 | 值 | 代码位置 |
|---|---|---|
| 初始生命 | 20 | `PlayerState.DefaultMaxHealth` |
| 初始手数 | 每人 2 只（左手、右手） | `PlayerState.LeftHand` / `RightHand` |
| 初始手值 | 1 | `Hand(initialValue: 1)` |
| 手值范围 | 1..9（**不存在 0**） | `Hand.Value` |
| 运算方式 | 只有加法 | `Hand.ComputeSum` |
| 每回合运算次数 | 必须且仅能 1 次 | `GameManager.ExecuteTurn` |

---

## 2. 十进制归一化（核心算术）

一次运算的结果取**十进制个位数**，且：

```
result = (a + b) % 10
if (result == 0) result = 1     // 和为 10 时变成 1
```

因此手值恒在 `1..9`，**永远不会出现 0**。

已验证样例（见 `ArithmeticTests.cs`）：

| 运算 | 结果 | 说明 |
|---|---|---|
| 1 + 9 | 1 | 和为 10 |
| 2 + 8 | 1 | 和为 10 |
| 5 + 5 | 1 | 和为 10 |
| 3 + 9 | 2 | 12 → 个位 2 |
| 4 + 9 | 3 | 13 → 个位 3 |
| 9 + 9 | 8 | 18 → 个位 8 |
| 7 + 8 | 5 | 15 → 个位 5（合盾） |

---

## 3. 数字效果

| 运算结果 | 效果 | 说明 |
|---|---|---|
| **4** | 攻击 4 点 | 伤害 = 4 |
| **6** | 攻击 6 点 | 伤害 = 6 |
| **8** | 攻击 8 点 | 伤害 = 8 |
| **9** | 回复 9 点 | 给自己回血，上限为最大生命 |

> **满血时的 9：无事发生，等于浪费一个回合。** 这是已确认的规则（见第 13 节），
> 不是 bug，不要"顺手修好"。实现上它仍然会消耗掉这个回合的运算次数。
| **5** | 合拢手掌 = **护盾** | 可抵挡一次攻击 |
| 1 / 2 / 3 / 7 | 无效果 | 仅改变手值 |

> 注意：**手值本身即效果**。手值停留在 4/6/8/9/5 时，该手在对手回合仍然"带着"
> 这个数字，但**效果只在运算产生它的那一刻结算一次**，不会持续触发。

---

## 4. 护盾（数字 5）

护盾有 3 个状态（`ShieldState`）：

| 状态 | 含义 | 视觉 |
|---|---|---|
| `None` | 未合盾，普通手 | 展开的手指 |
| `Formed` | 已合盾，可抵挡一次攻击 | 合拢的手掌 |
| `Broken` | 已破碎 | 展开的手掌 |

规则：

1. 运算结果为 5 → 该手进入 `Formed`。
2. `Formed` 的手抵挡一次攻击后 → 变为 `Broken`，本次伤害**完全免疫**。
   - 防守方有**两面**盾时，由 `IChoiceProvider.AskWhichShieldToBlock` 决定用哪一面。
3. 若 `Formed` 的手**被选为运算源**（行动方用它发起运算），运算**前**先自行破碎，
   然后再参与运算。
4. `Broken` 是"**盾碎了，但它的手值仍然是 5**"的标记，不是瞬时状态。它有两条硬约束：

   **约束一：`Broken` 只可能出现在值为 5 的手上。**
   如果运算把那只手变成了别的数字（例如 5+3=8），它就已经是普通手牌，状态回到 `None`。
   ——否则界面会出现"值为 8 的手显示碎盾展开"（曾经的真实 bug）。

   **约束二：只要手值仍是 5，`Broken` 就一直保留，可以跨任意多个回合。**
   碎盾**没有**"过期"概念：战士随时都可以用另一只手合出一面新盾，把这面碎盾修复为
   `Formed`。
   - 能让碎盾消失的只有两种情况：① 它被修复；② 它的手值不再是 5（被运算改写）。
   - 因此实现里**没有**按回合清理 `Broken` 的代码，只靠约束一的值判断维持不变量。

   实现位置：`GameManager.ResolveTurn` 第 3 步的值判断，以及
   `WarriorLogic.OnShieldFormed` 的修复。对应测试见 `CharacterLogicTests` 里
   "碎盾生命周期"一节。


> 待定（P1 前确认）：`Broken` 的手在若干回合后是否自动恢复为 `None`。

---

## 5. 回合契约（重要）

### 5.1 一次运算的两个输入

行动方选择：
- **自己的 1 只手**作为"运算源"（acted hand）
- **对方的 1 只手**作为"运算目标"（target hand）

### 5.1.1 选定与确认是两个动作（操作流程）

点对方的手牌分**两次**，含义不同：

```
① 点自己的手                -> 选定"运算源"
② 点对方的手                -> 选定"加数"（它参与相加）
③ 点目标玩家的手牌（再点一次）-> 确认"打谁"
```

**⚠️ 第 ③ 步只在"结果指向某人"时才出现。** 否则 1 + 1 = 2 这种
**无效果**的运算也会被追问目标，产生大量无意义询问。

| 运算结果 | 是否要求确认目标 | 原因 |
|---|---|---|
| 4 / 6 / 8（攻击） | ✅ 要 | 指向对手 |
| 9（回复） | ❌ 不要 | **自动回自己**（能改写目标的牧师除外） |
| 牧师的 4 / 6 / 8 | ✅ 要 | 当攻击或当回复都要选人 |
| 牧师的 9 | ✅ 要 | 可以回给自己或对手 |
| 5（护盾） | ❌ 不要 | 作用于自己 |
| 1 / 2 / 3 / 7（无效果） | ❌ 不要 | 没有目标可指 |

**为什么 1v1 也要点第 ③ 步（当它出现时）**：挨打的是**玩家**，不是手 ——
手牌只是"指定目标"的载体。多人模式下会有多个可选目标，
所以这一步必须显式存在；1v1 也要求点，是为了**不让多人上线后流程变形**。

- 目标玩家记录在 `BoardController.TargetPlayer`。
- **「放弃攻击」在"没有合法的攻击目标"时出现** —— 判据是 `HasAnyLegalTarget`，
  **不是"对手是不是刺客"**。
  - 玩家原话："应该明确放弃攻击按钮是在没有合法的攻击目标时弹出，
    刺客潜行是让自己无法成为攻击目标，即敌方运算出攻击数字无法点击，
    在没有引入多人模式可能的攻击距离前，1v1 只有刺客会导致没有合法攻击目标。"
  - 判据走 `CanBeAttacked(玩家)`：角色可声明自己**免疫**攻击
    （`IAttackTargetRule`；刺客潜行时实现它）。
    日后的"攻击距离不足"加在同一个判据里，**界面无需改动**。
  - 其余情况不提供该按钮，避免被用来规避正常战斗。
- **放弃攻击 = 运算照常发生（手值照常改变），但这一击不造成伤害**，
  然后算用掉本回合（回合数 +1、换对方行动）。
  - ⚠️ 它与"跳过回合"完全不同：跳过会让手值**回退**，
    而放弃攻击**必须保留运算结果**。玩家实测报过这个 bug：
    "1 + 3 运算出 4 点击放弃攻击会导致手回退至 1，这是不对的，应保留为 4"。
  - 实现：`GameManager.SuppressAttackDamage` 在**分派攻击时短路**，
    而不是跳过整个结算（跳过结算就会丢掉手值）。

### 5.2 只有行动方的手会改变

```
actedHand.Value = ComputeSum(actedHand.Value, targetHand.Value)
```

**`targetHand.Value` 不发生任何变化。**

这是本项目采用的回合制语义，已与需求方确认：

> 只有当前回合的角色数字会因运算操作导致自身数字变化，并触发攻击 / 合成盾牌 / 回血。

推论（务必理解，会影响数值设计与 AI）：
- 同一组数字，由 A 做还是由 B 做，结果归属完全不同。
- 每次运算都会让"行动方的手值"向对手的手值靠拢，因此**先手方更容易主动塑造局面**。
- 效果类型由**行动方手的新值**决定，与目标手无关。

### 5.3 禁止的运算源

- 行动方**被锁定**的手不能作为运算源（`Hand.IsLocked`）。
- 运算目标不能是行动方自己的手（必须是对方的手）。
- 游戏结束后不能再执行运算。

违反以上约束时，`GameManager.ExecuteTurn` **抛出 `InvalidOperationException`**，
不静默失败（便于 UI/AI 层尽早暴露 bug）。

---

## 6. 伤害公式

```
最终伤害 = max(0, 原始伤害 + 攻方.AttackBonus - 守方.DefenseBonus)
若 守方 承受"无视正向防御"，则 DefenseBonus 在 >0 时按 0 计算
```

其中"原始伤害"来自效果值（4/6/8），不是手值本身。

| 角色 | AttackBonus | DefenseBonus |
|---|---|---|
| 白板 / 数据角色 | 0 | 0 |
| 战士 Warrior | **+1** | 0 |
| 咒术师 Curseweaver | **-1** | 0 |
| 野蛮人 Barbarian | 0 | **+1** |

示例：
- 战士（攻 +1）打出 4 → 造成 5 点伤害
- 咒术师（攻 -1）打出 4 → 造成 3 点伤害（但无视护盾）
- 野蛮人（防 +1）受到 4 → 实际掉 3 点

---

## 7. 胜负判定

- 生命归零（`Health <= 0`）即失败，对方获胜。
- 双方同时归零 → 平局（`Winner == null`）。
- **回合数上限：`GameManager.TurnLimit = 50`。**
  打满 50 回合仍未分出胜负 → **按平局结束**（`IsGameOver = true`，`Winner = null`）。
- 判定发生在**每次运算结算完成后**（先判生命归零，再判回合上限）。
- "跳过回合"同样计入回合数上限。

`TurnCount` 记录的是**已完成的回合数**。因此第 50 回合结算完后：
`TurnCount == 50` → 平局，`CurrentPlayer` 不再切换。

> 为什么需要回合上限：单回合最大伤害 8 < 一次回复 9，
> 双方都"只顾活命"时理论上可以无限拖延（见 §18）。上限保证对局一定在有限步内结束。

对应测试：`MatchTests.Match_ends_as_a_draw_at_the_turn_limit`、
`MatchTests.Full_character_matrix_can_all_be_played_to_completion`。

---

## 8. 角色技能

所有角色的攻防修正与生命上限来自数据层（`CharacterDefinition`），
**行为**来自 `ICharacterBehavior` 实现；带"玩家抉择"的角色另外实现
`IPreTurnChoiceProvider`（见 §15.4）。

| 角色 | 攻/防 | 技能 |
|---|---|---|
| 白板 Blank | 0 / 0 | 无 |
| 战士 Warrior | +1 / 0 | 连锁破盾；新合盾时修复另一只手的碎盾 |
| 咒术师 Curseweaver | −1 / 0 | 攻击无视护盾与正向防御加成 |
| 野蛮人 Barbarian | 0 / +1 | 合盾时可锁对手一只手，直到该盾破碎 |
| 牧师 Priest | −1 / −1 | **4/6/8 可选当攻击打或当回复用**（回复量 = 该数字，不受攻防影响）；9 的回复可指定给自己或对手；**运算后双手同为 9** 时额外再回 9（同样可选目标） |
| 游侠 Ranger | 0 / −2 | 被攻击时可用一只「1」变成 2 完全闪避；攻击时可用一只「2」变成 3，使基础伤害 ×1.5 |
| 狼人 Werewolf | −2 / −2 | 生命上限 +5（25）；运算后双手和为 7 时回复上限一半并永久 +1/+1 |
| 剑客 Swordsman | +2 / −1 | 运算出 4/6/8 且**另一只手与该结果数字相同**时，主攻击前先追加一次固定 4/6/8 伤害、**无视双方攻防加成**的攻击 |
| 忍者 Ninja | +1 / −1 | 攻击改为**两段伤害减半**（4→2+2、6→3+3、8→4+4），每段各自结算攻防加成 |
| 刺客 Assassin | +2 / −1 | 双手均为 7 时**潜行**：不能被攻击；每次潜行可跳过一回合（仅一次）；出潜行的攻击 +7 |

### 8.1 牧师的三个要点

**攻防：−1 / −1**（曾为 −2 / 0，已改）。

- **4 / 6 / 8 二选一：当攻击打，还是当回复用。**
  - 选回复时，回复量就是**该数字本身**（4 回 4、6 回 6、8 回 8），
    **不受攻防加成影响**（不像攻击那样 ±攻防）。
  - 目标可以是**自己或对手**（给他人回复是为多人玩法预留的辅助能力）。
  - 放弃 / 不选 = **当攻击打**（默认行为）。
- **9 的回复目标**：打出 9 时问"这次回复给谁"，候选是**双方各两只手**
  （与选盾/锁手共用"点手牌"交互）。放弃 = 回给自己。回复量固定 9，不会超过上限。
- **运算出双 9 时额外回血**（`OnBeforeEffectResolved`）：
  **运算后**双手同时为 9 → 额外回复 9 点，**这次也由玩家选目标**。
  - 判定依据是"运算**后**"的双手值（运算源已是结果值，另一只手未变）。
    例：运算源 8 + 目标 1 = 9，另一只手也是 9 → 运算后双 9。
  - 因为抉择都要在**运算前**收集，所以用 `HasDoubleNineAfter(context)`
    **预测**：`ComputeSum(运算源运算前的值, 目标值) == 9` 且另一只手已是 9。
    （这与剑客"追加攻击"的判定同类 —— 都是运算前可算的。）
  - **与主效果叠加**：8+1=9 且另一只手是 9 时，
    本次回复 9 + 额外回复 9 = 共 **18** 点（两次各选各的目标）。
  - 钩子挂在主效果分派**之前**，只是为了确定两次回血的先后顺序。
  - ⚠️ 出 9 时**不问**"攻击还是回复"：本次运算已经出了 9，
    该问的是"额外回复给谁"和"本次回复给谁"（顺序即此）。

**抉择的时机与顺序**（这是玩家明确要求的交互）：

```
4/6/8 → ①【选目标手之前】攻击还是回复
        → 点目标手（参与运算的那只手）
        → 确认目标玩家
        → ②【选目标手之后】若选了回复，再问"回复给谁"

9     → 点目标手 → 确认目标玩家 → 问"本次回复给谁"

双 9  → 点目标手 → 确认目标玩家 → 先问"额外回复给谁"、再问"本次回复给谁"
```

⚠️ 第 ① 步的答案要**跨越"选目标手"那一段**才能到达结算，所以有两处机制保证它不丢：
- `IPreTurnChoiceSink.GetExplicitChoice` —— 只读**显式记录**，
  从而区分"选了攻击"（记录为 null）与"选了回复"（记录为一只手）。
  用 `GetChoice` 会套用回退值，把"选了攻击"误判成"选了回复"。
- `GameManager.AnsweredLookup`（由 `BoardController` 注入）—— 结算侧读同一份答案。
- 回合边界的清空只在**行动方真正变化**时发生（`IsTurnBoundary`），
  不会把同回合中途的答案清掉。

`PriestExtraHealTarget` 是**独立于** `PriestHealTarget` 的抉择值 ——
否则双 9 时两次回复的目标会互相覆盖。

### 8.2 游侠的要点
- 两种能力都**只是改变数字，不是"消耗"**：1→2（闪避）、2→3（强化）之后
  没有任何"本回合已用"的标记。手值再次回到 1 / 2 时就能再次触发。
- 两者发生在**不同的回合**：
  · 1→2 是**对方回合**用来闪避对方攻击的；
  · 2→3 是**自己回合**用来强化这次攻击的，而此时**另一只手**才是攻击运算源。
- **闪避**优先级高于护盾：完全免伤且**不消耗护盾**。
  两只手都是 1 时自己选用哪只手；**允许放弃**（UI 提供"不闪避"按钮）。
- **咒术师的攻击无法闪避**（与伤害数值无关）。
  咒术师同样会打出 4 / 6，而**其他角色打出 8 时游侠仍然可以闪避**。

  ⚠️ 判定依据是"**攻击者是谁**"，不是"打了多少"。
  我最初按 `Effect == Attack8` 判定，结果**所有角色的 8 伤害都无法闪避** —— 那是 bug。

  实现方式：角色若想让自己的攻击不可闪避，就实现 `IDodgeBlocker`
  （咒术师返回 `PreventsDodge = true`）；游侠询问
  `context.Actor.Character as IDodgeBlocker`，**不认识任何具体角色类型**。
- **强化射击**：用一只手发起攻击后，若**另一只手**为 2，可以把它变成 3，
  使这次攻击的**基础值 ×1.5**（4→6、6→9、8→12）。
  - 候选是"**另一只手**"，不是运算源 —— 运算源是攻击手（4/6/8），本来就不可能是 2。
    （我最初误判成"运算源是 2"，导致强化永远无法触发。）
  - 允许放弃，UI 提供"不强化"按钮。
  - 倍率作用顺序是 **先乘倍率、再叠加攻防加成**（见 §6）。

### 8.3 多段攻击（剑客 / 忍者共用）

剑客的追加攻击与忍者的两段攻击都会造成**同一个回合内的多次伤害结算**。
规则已确认：**一面盾只能挡下其中一段**，另一段照常造成伤害。

实现：`AttackContext.HitCount`（段数）+ 逐段循环，每段
**独立判定护盾、独立扣血、独立结算攻防加成**。

⚠️ 注意 `FinalDamage` 记录的是"经倍率、但**尚未叠加攻防加成**"的伤害。
攻防加成只在逐段结算时施加一次 —— 早先我把它算进 `FinalDamage` 又在逐段算一遍，
导致加成被叠加两次，6 个既有测试同时失败。

### 8.4 剑客的要点

- 触发条件是"**运算源运算后的值** == 另一只手的值"（即双 4 / 双 6 / 双 8），
  所以判定必须用运算结果（`PreTurnChoiceContext.ResultValue`），
  **不能**用 `ActedHand.Value` —— 收集抉择时运算还没执行，那个值还是运算前的。
- **追加攻击的固定伤害 = 那双数字本身**：双 4 → 4、双 6 → 6、双 8 → 8。
  实现上就是 `(int)effect`（`Attack4`/`Attack6`/`Attack8` 的枚举值恰为 4/6/8）。
- 追加攻击是**独立的一次 `AttackContext`**，带 `IgnoreStatBonuses = true`
  （无视双方攻防加成）。**主攻击照常结算攻防加成。**
- 提示里会同时给出"**发动追加**"与"**不追加**"两个按钮。

### 8.5 忍者的要点

- 触发条件是"**运算源运算后的值** == 另一只手的值"，
  所以判定必须用运算结果（`PreTurnChoiceContext.ResultValue`），
  **不能**用 `ActedHand.Value` —— 收集抉择时运算还没执行，那个值还是运算前的。
  （我最初用错了，导致"双 6"永远判不出来。）
- 追加攻击是**独立的一次 `AttackContext`**，带 `IgnoreStatBonuses = true`。


- 段数为 2，伤害平均分配（`4/2=2`、`6/2=3`、`8/2=4`，都能整除）。
- 每段各自叠加攻防加成：忍者基础 4、攻 +1 → 每段 `2+1=3`，共 6。
  **不是** `(4+1)/2 = 2`。

### 8.6 刺客的要点

- **潜行**：双手都是 7。按当前手值**持续判定**，不是"只在变成双 7 的那一回合"。
- **不能被攻击**：复用 `TryDodge` 通道（语义一致：完全免伤、不碰护盾）。
- **跳过回合**：由 <see cref="ITurnSkipRule"/> 接管 —— 潜行中可跳，但一次潜行只能跳一次。
- **出潜行 +7**：`OnResolveAttack` 里加 `FinalDamage`；出手会改写手值，因此自然脱离潜行。
- 内部只持有"本次潜行是否已跳过"这一个标记，`OnMatchReset` 清空。

- 生命上限 = 20 + `WerewolfLogic.HealthBonus`(5) = 25。
- 判定用的是"两只手**运算后**的数字之和"（与牧师相反，注意区别）。
- 回复量 = `MaximumHealth / 2` = 12（向下取整，且不超过上限）。
- 成长是**跨回合状态**，`OnMatchReset` 必须调用 `ResetStats()` 丢弃累计，
  否则重开一局会带着上一局的加成继续长。


### 8.7 咒术师 Curseweaver

- **攻击无视对方护盾**：`AttackContext.IgnoreShield = true`，护盾不消耗、不破碎、不挡伤。
- **无视对方的正向防御加成**：`IgnorePositiveDefense = true`（负向/易伤加成仍然生效）。
- 攻击修正 `-1`（伤害偏低，作为无视护盾的代价）。
- 实现：`CurseweaverLogic.OnResolveAttack`

### 8.8 战士 Warrior

- **连锁破盾（可选发动）**：当自己的一面盾破碎时（无论被攻击打破，还是作为运算源
  主动破碎），可选择场上**任意一面处于 `Formed` 的盾**使其立即破碎；也可放弃。
  - 目标可以是自己的另一面盾，也可以是对手的盾。
  - 目标盾若属于对手，同样会触发对手的 `OnOwnShieldBroken`，可能继续连锁。
  - 同一回合内每面盾最多破碎一次，由 `GameManager._brokenThisTurn` 防递归。
- **盾牌修复（自动被动）**：当自己新合出一面盾（运算结果为 5）时，若**另一只手**
  处于 `Broken`，则自动修复为 `Formed`。
  - 结合 §4 约束二，碎盾**可以跨任意多个回合**被修复 —— 只要那只手的手值仍然是 5。
  - 如果是**自己**把盾当运算源弄碎的，且该次运算的结果不是 5，那么那只手已经不是 5，
    也就不是"碎盾"了，自然不会被修复。
- 攻击修正 `+1`。
- 实现：`WarriorLogic`

### 8.9 野蛮人 Barbarian

- **锁手（可选发动）**：当自己新合出一面盾时，可选择对手一只**未被锁定**的手锁定。
  - 被锁的手**不能被选为运算源**（见 5.3）。
  - 同时最多控制对手一只手：若自己另一只手已经处于"控制中"，本次不能发动。
  - 锁定在该盾破碎时解除（`OnOwnShieldBroken`）。
- 防御修正 `+1`。
- 实现：`BarbarianLogic`

---

## 9. 复位契约（P0 修复项）

`GameManager.ResetMatch()` 必须把所有状态恢复到开局：

1. 双方生命恢复 `MaxHealth`。
2. 双方两手数值恢复 1，护盾状态恢复 `None`，`IsLocked` 清除。
3. 胜负标记清除，`TurnCount` 归零，`CurrentPlayer` 重置为 `PlayerA`。
4. **调用每个角色的 `ICharacterBehavior.OnMatchReset()`**，让角色清空自己的内部状态
   （例如 `BarbarianLogic` 的 `_lockMap`）。

> 第 4 步是 P0 修复的核心：缺少它会导致第二局中对手的手被永久锁死。

---

## 10. 角色数据化（P0 修复项）

角色的数值不再硬编码在 C# 里，而是由 `CharacterDefinition` 描述：

```
Id, DisplayName, SpecialLogicType, AttackBonus, DefenseBonus, MaxHealth, Description
```

- `CharacterFactory` 负责 `SpecialLogicType` → `ICharacterBehavior` 的映射与实例化。
- 内置定义见 `CharacterDefinition.BuiltIn`（`Blank` / `Warrior` / `Curseweaver` / `Barbarian`）。
- 调平衡只需改定义表或资产，无需改动角色逻辑代码。

---

## 11. 依赖方向（架构约束）

```
Tests ──► Assistant ──► Core ◄── Characters
```
- `Core` 只依赖 .NET BCL，**不依赖任何 Unity 类型**，因此可以直接在编辑器外用
  Roslyn 编译并运行测试（见 `Tools/HeadlessTests`）。请保持这一点。
- `Characters` 引用 `Core`（角色逻辑实现 `ICharacterBehavior`，并持有对
  `Core.CurseweaverLogic` 等具体类型的工厂引用）。
- `Assistant`（AI / 测试替身 / 对局驱动器）引用 `Core`。
- `Tests` 引用三者。
- 程序集实现：
  - `Assets/Scripts/FingerDiceDuel.Runtime.asmdef`（Core + Characters + Assistant）
  - `Assets/Tests/EditMode/FingerDiceDuel.Tests.EditMode.asmdef`

## 12. 测试与验证

| 内容 | 位置 |
|---|---|
| EditMode 测试（6 个夹具，95 项） | `Assets/Tests/EditMode/` |
| 无头运行工具 | `Tools/HeadlessTests/`（`powershell -File Tools/HeadlessTests/run-tests.ps1`） |
| 环境适配门面 | `Assets/Tests/EditMode/UnityTestShim.cs` |

测试文件只允许使用该门面提供的 `[TestFixture]` / `[SetUp]` / `[TearDown]` / `[Test]`
与 `Assert.*`，不得直接使用 NUnit 专有 API —— 详见 `Tools/HeadlessTests/README.md`。

## 13. 已决策：满血回复

**决策（已确认，无需再讨论）：**

> 满血时使用 9 —— **无事发生，浪费一个回合**。回复量不超过生命上限。**不需要**任何
> 额外功能（不改伤害、不给别的效果、不加额外规则）。

实现位置：`GameManager.ResolveTurn` 的 `HandEffectType.Heal` 分支
（`actor.Heal(Hand.HealAmount)`，`PlayerState.Heal` 内部 `Math.Min(MaximumHealth, ...)`）。
对应的测试是 `HandEffectTests.Heal_is_capped_at_max_health` 与
`MatchTests.Heal_at_full_health_wastes_the_turn`。

## 14. 跳过回合（仅限双盾在手）

**规则：** 当一名玩家**两只手都处于成型护盾状态**（`HasDoubleShield`）时，
他可以在自己回合**放弃运算、直接跳过**，把回合交给对方。

- 这是**玩家的选择**，不是强制的；双盾在手时仍然可以正常运算。
- 跳过**不产生任何效果**：不改手值、不改护盾状态、不改生命、不触发角色钩子。
- 跳过照样消耗一个回合（`TurnCount` 递增，`CurrentPlayer` 交给对方）。

实现：

| 位置 | 内容 |
|---|---|
| `GameManager.CanSkipTurn(player)` | 静态判定：`player.HasDoubleShield` |
| `GameManager.SkipTurn()` | 执行跳过；不满足条件时抛 `InvalidOperationException` |
| `BoardController.CanSkipTurn` | UI 用：还要排除"选盾 / 锁手 / 已结束"这些中间阶段 |
| `BoardController.SkipTurn()` | 写日志 + 切换回合 |

UI：满足条件时，在**玩家 A 右手牌的右侧**出现"跳过回合"按钮
（`GameBoardUI.SetSkipButtonVisible`，位置由 `EnsureSkipButton` 决定）。

## 15. 谁来决定用哪面盾抵挡（重点）

**被攻击的一方（防守方）自己选择**用哪一面盾抵挡，**不是**攻击方选择打碎哪面盾。

| 场景 | 决策者 |
|---|---|
| 防守方只有一面 `Formed` 的盾 | 无需询问，自动用那一面 |
| 防守方有**两面** `Formed` 的盾，且本次运算会打出攻击 | **防守方**通过 `IChoiceProvider.AskWhichShieldToBlock` 选择 |
| 咒术师无视护盾 | 仍会询问一次（无害），但不会消费任何盾 |

### 15.1 "现在该谁做决定" ≠ "现在轮到谁行动"

这是本项目最容易搞错的一点，也是"选盾询问不出现"的根因。

进入 `ChoosingDefenseShield` 阶段时，`ActivePlayer` 仍然是**攻击方**，
但该做决定的是**防守方**。因此：

- `BoardController.Chooser` 返回"当前阶段该做决定的人"。
- `BoardController.IsHumanChooser` = 那个人是否由人类操作。
- UI **必须**用 `BoardController.CanAct(hand)`（= `IsHumanChooser && CanClick`）
  来决定手牌是否可点、以及点击是否放行。

**绝不要**用 `IsHumanTurn`（看行动方）来放行点击 ——
否则"AI 攻击、人类防守"时，玩家的选盾点击会被静默丢弃。

### 15.2 AI 当防守方时

由 UI 注入 `BoardController.AiResolver`（`IAiChoiceResolver`）代做选盾 / 锁手抉择。
`BoardController.ResolvePendingAiChoice()` 供 UI 在"AI 只需代做一个决定"时调用
（典型：人类攻击、AI 防守；或 AI 攻击、人类防守的收尾）。

### 15.3 接线要求（踩过的坑）

1. **`IChoiceProvider` 与 `IPlayerChoiceSink` 必须是同一个对象**
   （`SimpleChoiceProvider` 同时实现两者）。
2. **必须给两个玩家各传一个 sink**（`BoardController(game, sinkA, sinkB)`）。
   只传一个会导致另一名玩家当防守方时选盾询问被跳过。
3. **抉择器要在创建 `GameManager` 时就传进去**，不能只交给 `BoardController`。
   `GameManager` 内部保存的是它自己那份 `ChoiceProviderA/B` 引用；
   只给 `BoardController` 传 sink 的话，`GameManager` 那侧仍是 `null`，
   `AttackContext` 会退化成"用第一面盾"，表现为"总是破左手盾"。
   `CharacterFactory.CreateMatch(a, b, providerA, providerB)` 就是为此存在的。

对应测试：`BoardControllerTests` 的
`Only_the_defender_can_pick_the_blocking_shield`、
`Human_defender_can_act_while_ai_is_attacking`、
`Ai_defender_does_not_interrupt_the_human_attacker`、
`Attacker_clicking_its_own_hand_is_ignored_during_shield_choice`、
`Only_the_defenders_chosen_shield_breaks`。

### 15.4 角色抉择的声明式机制（加角色时看这里）

**不要**在 `BoardController` 里写 `is XXXLogic` 判断 —— 那是不可扩展的。
角色自己声明需要什么抉择：

| 类型 | 作用 |
|---|---|
| `IPreTurnChoiceProvider.CollectPreTurnChoices` | 角色声明"这次运算前需要玩家先决定什么" |
| `ChoiceRequest` | 一个抉择：`Effect` / `Prompt` / `Candidates` / `AllowDecline` / `DeclineResult` / `Decider` |
| `IPreTurnChoiceSink.GetChoice` | 结算过程中回读玩家选了什么 |

流程：`BoardController.CommitMove` → **先**遍历双方的 `IPreTurnChoiceProvider`
收集角色抉择并逐个问（人类点手牌 / AI 用 `IChoiceProvider.ChooseHandFor`）→
**然后**才进入"用哪面盾抵挡"阶段 → 全部收齐后才 `ExecuteTurn`。

⚠️ **询问顺序必须与 `AttackContext.Resolve` 一致：先闪避、再护盾。**
曾经这里先问选盾、问完直接结算，结果"有双盾的游侠"根本问不到闪避
（表现为"优先碎盾而不是询问是否闪避"）。

**为什么必须在运算前收集**：`GameManager.ExecuteTurn` 一旦开始就会一次跑完，
没有"暂停 / 恢复"机制，结算中无法再向 UI 索取输入。

`ChoiceRequest.Decider` 决定"该谁回答"：默认是行动方，游侠的闪避显式指定为**防守方**。

**踩过的坑**：`PreTurnChoiceReader.Read` 的顺序是"先回读 sink，没有记录则当场询问抉择器"。
如果只回读 sink 而不回退询问，那么所有**直接调 `GameManager.ExecuteTurn`** 的场景
（测试、脚本化对局）里角色的抉择都会静默失效 —— 野蛮人锁手曾因此全部失灵。

## 16. 交互契约（P1）

界面不是直接把规则写在按钮回调里，而是通过 `Assistant.BoardController` 这个
**不依赖 Unity 的状态机**（因此可以在无头测试里完整验证）。阶段如下：

| 阶段 | 含义 | 可点击的手 |
|---|---|---|
| `ChoosingAttackerHand` | 等待行动方选自己的运算源 | 自己的、未被锁定的手 |
| `ChoosingTargetHand` | 等待行动方选对方的运算目标 | 对方的手；**已选中的那只自己的手**（点它 = 取消） |
| `ChoosingDefenseShield` | 防守方有两面成型盾，且这次运算会打出攻击时，先问**防守方**用哪面挡 | 那两面盾（**只有防守方能点**） |
| `ChoosingLockTarget` | 野蛮人合出盾牌时，问要锁对手哪一只手 | 对手那两只手 |
| `GameOver` | 对局结束 | 无 |

另有两条**不进入阶段机**的 UI 行为：
- **跳过回合**：当前行动方双盾在手时，右手牌右侧出现按钮（见 §14）。
- **退出游戏**：右上角按钮，直接回到角色选择界面（清掉当前对局）。

要点：

1. **一次点击只做一件事。** 第一阶段选自己的手，第二阶段选对方的手；
   第二阶段点击后立即结算。
2. **取消选择有两条路：** ① 再点一次已选中的自己的手；② 调用 `CancelSelection()`。
   两种方式都回到 `ChoosingAttackerHand`，且**不推进回合**。
3. **回合开始前的抉择统一先问完再结算**，顺序是：锁手 → 选盾 → 结算。
   逻辑层没有"暂停/恢复"机制，所以 `BoardController` 先把结果写进
   `IPlayerChoiceSink`，再调用 `GameManager.ExecuteTurn`。
4. **`IChoiceProvider` 与 `IPlayerChoiceSink` 必须是同一个对象**
   （`SimpleChoiceProvider` 同时实现两者）。若只实现了 sink，抉择结果传不回
   `GameManager`，护盾/锁手就会用错目标 —— 这个坑已由
   `BoardControllerTests.Barbarian_lock_choice_targets_the_chosen_hand` 等测试钉住。
5. 咒术师无视护盾时，双盾仍会被询问一次（无害的多问），
   但 `AttackContext` 不会消费任何盾。
6. 野蛮人**只有在自己合出盾牌、且当前没有再控制着对手某只手时**才会被询问锁手。
   "已经在控制中"的判定是：己方**另一只手**已处于 `Formed`。
7. `LastTurn` 的语义是"**最近一次已结算**的回合"，会一直保留到下一次结算为止，
   所以玩家为新回合做选择时仍能看到上一回合的结果。

## 17. 表现层约定

- 界面**全部在运行时用代码构建**（`GameBoardUI` / `GameBootstrap`），不使用预制体。
  好处是可以 diff、可以 review，也不会因手工编辑场景 YAML 出错。
- **入口不依赖场景**：`GameBootstrap` 用 `[RuntimeInitializeOnLoadMethod]` 自动创建，
  并在需要时创建 `EventSystem`（没有它界面不响应点击）。
- 所有与"长什么样"有关的判断集中在 `UI/HandVisual.cs`（颜色、文字），
  字体加载集中在 `UI/UiFont.cs`。换美术资源 / 换字体时只改这两个文件。
- 屏幕纵向分区（自上而下）：**B 区（对手）→ 提示 → A 区（自己）→ 战斗文字**。
- AI 出手前会等待 `GameBootstrap.AiThinkSeconds`（默认 2 秒），
  让玩家看清自己刚才打出了什么。
- `Assets/Scripts/UI/` 是**唯一**依赖 UnityEngine 的目录。请保持
  `Core` / `Characters` / `Assistant` / `Tests` 不依赖 Unity，
  否则秒级无头测试就没了。

## 18. 已知的平衡隐患（暂不处理）

**单回合最大伤害 8 < 回血量 9。**

上一节已确认"满血回复就是浪费回合"，所以这个隐患**暂时保留**：不改规则，
只靠 AI 策略规避。

实测（贪心 AI 自对局，600 回合上限）：

| 对局 | 结果 |
|---|---|
| 白板 vs 白板 | 41 回合结束 |
| 战士 vs 战士 | 27 回合结束 |
| 咒术师 vs 咒术师 | 47 回合结束 |
| **野蛮人 vs 野蛮人** | **600 回合未结束（僵局）** |
| 野蛮人 vs 野蛮人，但 AI 阈值 `HealPriorityHealth ≤ 8` | 21 回合结束 |

规避手段：`GreedyChoiceProvider.HealPriorityHealth` 默认值为 8
（阈值 ≥12 时 100% 僵局，≤8 时正常结束）。

**风险提示：** 这个隐患在**两个真人玩家对战**时会比 AI 对战更明显 ——
真人很快会发现"只要我总在低血量时回复，对方就永远打不死我"。
如果 P1 试玩阶段出现这种情况，再回头讨论规则（可选方向：满血时 9 改为造成伤害 /
降低回血量 / 引入回合上限判血量高者胜）。**在此之前不要改规则。**
