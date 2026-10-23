# CODELY.md

> 工程结构与开发约定。**规则权威文档是 [`RULES.md`](RULES.md)** ——
> 代码、测试、UI 都必须与它一致，改动规则先改那里。

## 项目概况

**Finger Dice Duel**（手指骰子对决）：2D 回合制对战。每人两只手（初始 1），
每回合用自己的一只手与**另一名玩家**的一只手相加、取个位（和为 10 → 1），
结果数字决定效果：**4/6/8 攻击、9 回复、5 护盾**。生命 20，先归零者败。

支持 **1v1**、**2v2**（环形四座、1&4 一队 / 2&3 一队、攻击距离、先攻掷骰）
与 **双将对决（娱乐模式，1v1）**、**8 人身份对局**（随机身份，主公固定 1 号位）。
**37 名角色**，每个都有独立技能。

| 项 | 值 |
|---|---|
| 引擎 | 团结引擎（Tuanjie，Unity 中国版）`2022.3.62t15` / Editor `1.10.3` |
| 语言 | C#（源文件 **UTF-8 with BOM**） |
| 渲染 / UI | 内置管线 + 正交相机；uGUI + TextMeshPro |
| 测试 | **1151 项全部通过**（2026-10-23），含三套统计与集火实验；Unity 条件编译和 UI 编译通过；职业·子职真实界面 **321 项检查通过**；王车易位真实引擎/UI **143 项通过**；本次阵营/双将 **127 项通过**；三所法师学派本批真实引擎/UI **95 项通过** |
| 美术 | 全部界面**运行时用代码构建**，无预制体 |

> 当前统计规模：1v1 **42,240** 局、2v2 **67,584** 局、双将 **336,672** 局、集火实验 **118,800** 局；
> 8 人身份局另有随机/偏好各 **2,000** 局。

## 职业与子职（2026-10-23）

当前 37 个子职，12 个职业大类；德鲁伊仅预留。唯一命名与归属表在 `Core/CharacterIdentity.cs`，职业中英文和大类图标在 `Core/CharacterProfession.cs`。正式 ID 为 `职业_子职`；旧 ID 通过 `CharacterDefinition.Find`、`CharacterCodex.For` 兼容，旧 `Classes/旧ID` 美术键也会归一化。新增角色应先登记身份，再登记定义、逻辑与图鉴。

大类图标在 `Assets/Resources/Art/Battle/v1/Professions/`，子职在 `Subclasses/`。代码与测试类型同步使用新子职名，枚举数值顺序不变；原有 `.meta` 原样移动，新元数据由编辑器生成。最终 45 枚图标由 `Tools/ArtPipeline/import-selected-profession-icons.cjs` 导入；旧图集导入后须保留该覆盖步骤。

图鉴职业筛选与阵营筛选取交集，德鲁伊显示空态；详情依次为斜体小字职业中英文、大字子职中英文、阵营。职业迁移阶段技能只做名称与设定调整（如乘风突刺、空间刃、王冠威仪、勇气战歌），原数值、条件与流程不变。重构后的最新检查见 [验收](Art/Integration/profession-subclass/README.md)，完整旧名对照见 [制作记录](Art/Production/profession-subclass-v1/README.md)。历史截图与日志保留其当时含义。

## 勇士十点重击（2026-10-23 后续平衡调整）

`Champion` 当前攻防 **+1/0**，生命20、距离1；取 `AttackContext.AttackerHandSumAtStart`（运算后、攻击建立时的双手和），等于10则自动令 `AdditionalBaseDamageMultiplier` 乘1.5。该独立倍率与既有 `DamageMultiplier` 相乘，统一四舍五入后再计算攻防，不覆盖乘风突刺或误改其伤害类型。特殊攻击与反弹不触发；不新增按钮。`ChampionLogic` 提供技能计数、战报和斩杀估算，重开清空计数。

`ChampionHeavyStrikeTests` 9项覆盖真实运算时点、倍率顺序、三种攻击数字、快照、双将主副顺序、伤害类型、护盾、特殊攻击隔离、跨次清理及AI估算。迁移前基础快照保持历史原值；`ProfessionContractTests` 中勇士防御按本次明确调整改为0。新金狮徽记及验收见 [记录](Art/Production/champion-heavy-strike-v1/README.md)。

本次勇士完整测试在独立 PowerShell 进程内设置 `$env:DOTNET_TieredCompilation='0'` 后运行：本机 .NET6 分层JIT在长时间统计中两次出现原生 `AccessViolationException`，关闭分层JIT后1061项完整通过；不改测试断言或游戏规则。团结编辑器另执行9项专项及真实界面操作，共28项通过。该环境变量仅对测试进程生效，不影响游戏。

## 咒法学派·王车易位（2026-10-23）

`SchoolOfConjurationLogic` 实现 Core 的 `ISeatSwapSkill`，通过 `SeatSwapSkills.Of` 发现单角色或双将部件。行动前可与另一名存活角色交换座位，双手值必须逐个相同但不限左右；不是比较总和。可选队友与敌人，不限距离，冻结不能发动。按钮「王车易位 · 跳过回合」进入独立选目标阶段，取消不耗回合；确认后仅结束一次行动，按原规则结算状态。

`GameManager.Seats.cs` 将物理座位和当轮行动顺序分离：首次换位快照当轮名单，后续换位保留该快照；距离与上下家立即按新座位计算，本轮绕回后清快照，下一轮从新座位首位存活者开始。队伍、身份、手牌、抉择器和本地控制跟随人物，重开恢复初始座位。轮次绕回使用名单索引，首位被回合开始状态击杀时不会误加轮次；非攻击偷窃路径补齐轮次收尾。

`BoardController.Castling.cs` 处理目标与取消，额外行动内换位消耗该次额外行动而不推进正常轮次；AI 仅在减少相邻敌人数时主动换位。`GameBootstrap.Castling.cs` 根据 `SeatRevision` 重绑多人面板，用人物引用保存人类控制、向内核查询队友关系。新抉择需同时登记 Chooser、CanClick、ClickHand、PendingEffect 和 UI 目标映射。

`ConjurationCastlingTests` 20项覆盖无序匹配、自身/场外/死亡/冻结限制、1v1/4人/8人轮转、轮中/轮末与多次换位、死亡跳过、即时距离与上下家、目标/取消/状态、双将主副顺序、重开、身份、额外行动、AI、偷窃与轮次上限。真实编辑器验收及截图见 [王车易位](Art/Integration/castling/README.md)。批量统计报告使用既有 MatchRunner 策略，不等同于控制器主动换位策略的专项胜率评估。

## 阵营移动与双将兼容（2026-10-23）

10名子职按玩家最终确认移动；狂战士道途撤回、留中立邪恶，魔术师移入混乱中立并修复兼容。迁移时27项阵营唯一数据仍在 `CharacterCodex`；图鉴筛选、主副将候选、随机配对、阵营局和主公按阵营增益均读取此表。当时守序10／中立7／混乱10人，111种双将、222种主副顺序。

`ISelectiveHandEffectRewriter.HandlesResult` 声明接管数字，`CharacterBehaviors.EffectRewriterFor` 统一选处理者；结算、`PreTurnChoiceContext`、双将显示与目标预判共用。空改写不屏蔽其他子职，只有同数字真实争用才按主将优先。魔术师2、巨人4/5/6/8/9互不争用；巨人伤害钩子限5，自加能力在双将容器聚合。另补容器的穿透减伤转发、控制器对妖精部件的查询；碎盾射程逐部件转发，避免主将继承的空实现截断副将。双将全名在姓名行内缩放，避免覆盖血条。单角色数值保持不变。

`AlignmentMigrationTests` 13项覆盖最新阵营、111对双顺序、数字分派、真实运算、自加、目标、穿透及两个跳过技能独立选择；1332次运算逐行保存在 `Tools/dual-alignment-probes.tsv`。完整结论与组合增减见 [检查报告](Tools/dual-alignment-audit.md)，真实界面见 [验收](Art/Integration/alignment-migration/README.md)。检查接口竞争及选定场景，不将零异常解释为所有局面无问题或数值平衡。

旧勇士+风流剑客倍率与旧跨阵营种子21639仍以测试专用原始容器保留回归；正式工厂不放宽跨阵营限制。旧制作/验收记录与历史统计保持当时含义，当前规则以本段、RULES和最新检查为准。

## 法师三学派（2026-10-23）

三所学派正式徽记已接入；新增塑能徽记，旧书杖划归法师大类；13类抗性与奥术屏障共有19项导出资源（18张新绘、1张复用），见 [制作记录](Art/Production/wizard-schools-v1/README.md)。当前37子职，守序13／中立13／混乱11，共211种双将；历史迁移记录保留当时27人的口径。

`WizardChoice` 将回合开始免费攻击及死亡换手排队，在控制器安全边界暂停/恢复，不重复开始回合或结算状态。死灵有可用1自动进入角色目标选择并提供「放弃攻击」；2→1逐段由人类选择，AI自动；死亡时交换0～2对手值，可提前结束。`DeferWizardAttack` 在免费/偷窃攻击前收集防守抉择。回合开始、死亡通知、预言无效等钩子遍历双将部件。

防护算出2先选角色，再选 **物理整组、雷鸣、强酸、火焰、寒冷、闪电** 六项，`ResistanceNames.Choices/CanGrant` 同时限制UI、AI与技能API；心灵/力场/毒素/光耀/暗蚀不可由此赋予。`ResistanceNames.All` 仍保存13类通用图标。`IAbjurationSkill` 存储屏障；选择存储后不再走攻击目标/盾选择，抗性后、扣血前吸收并全部清空。原有场上保护范围保持不变。

`IWizardChoicePolicy` 供控制器与无头内核共享目标、抗性、屏障、免费暗蚀和死亡交换策略；`GreedyChoiceProvider.Wizards.cs` 实现。全部33角色有显式手值配置，当前297组（33角色×3难度×2/4/8人）控制器推进检查。还修复偷窃攻击的目标/防御与一次收尾、妖精跳过目标、魔术师自加锁手、赌博无合法目标的结束路径。斩杀估算考虑当前攻防、抗性和屏障，仅预览不消耗；仍是启发式估算。详见 [AI检查](Tools/ai-strategy-audit.md) 和 [引擎验收](Art/Integration/wizard-schools/README.md)。

`CombatStatusIcons` 统一负面、抗性和屏障显示；图标栏最多32项，超出可视区域可滚动。秘法手册为伤害类型、状态、六条元素反应、抗性与屏障四页；新增页用完整资源键，元素反应未增加心灵条目。

## 目录结构

| 路径 | 用途 |
|---|---|
| `Assets/Scripts/Core/` | 规则内核。**不依赖任何 Unity 类型** |
| `Assets/Scripts/Characters/` | 角色数据层（`CharacterDefinition`）+ 各角色技能 |
| `Assets/Scripts/Assistant/` | AI、测试替身、整局驱动、`BoardController` 交互状态机、2v2 先攻与环形布局。**不依赖 Unity** |
| `Assets/Scripts/UI/` | 表现层（**唯一**允许引用 `UnityEngine` 的目录） |
| `Assets/Editor/` | 仅编辑器的战斗美术预览、运行时界面检查与截图工具；可引用 UnityEditor / UnityEngine |
| `Assets/Tests/EditMode/` | 无头测试；`UnityTestShim.cs` 是环境适配门面 |
| `Assets/Resources/Fonts/` | 思源黑体 Medium（正文）/ Bold（数字），思源宋体 SemiBold（标题），SIL OFL 1.1 |
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

  > **伤害类型同理**：`Core/AttackDamageTypes.cs` 定义 `AttackDamageType`
  > （物理 3 + 魔法 10）与接口 `IAttackTypeProvider`；
  > 各角色在 `Characters/` 里实现它，`AttackContext` 只认接口。
  > 颜色 / 图标键也只有 `AttackDamageTypes` 一份 ——
  > 所以"伤害飘字颜色 = 攻击类型图标颜色"是**结构上**成立的。

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
> 上述命令不会验证画面。1v1 现在可通过正在运行的编辑器执行菜单 `Finger Dice Duel/Preview/1v1 Battle Art`，检验真实点击、字体加载、资源映射与布局，并输出引擎渲染截图至 `Art/Integration/1v1/`。批处理启动失败不代表此编辑器内验证不可用。

**用编辑器看画面**：团结 Hub 打开本目录 → 直接 Play（任意场景都行）。
前台美术验收菜单：`Finger Dice Duel/Preview/All Front End Art`。覆盖主菜单、全部子菜单、五类选将、图鉴与先攻，输出 `Art/Integration/front-end/`。

技能与居中验收菜单：`Finger Dice Duel/Preview/UI Alignment and Skills`。核对 24 个角色入口，检查骰面字形、徽记与名字间隔，并实际点击主动 / 防御技能、法师·塑能学派与吟游诗人·勇气学院面板。截图及角色入口清单见 [`Art/Integration/ui-polish/README.md`](Art/Integration/ui-polish/README.md)。

多人战斗验收菜单：`Finger Dice Duel/Preview/Multiplayer Battle Art`。覆盖 2v2 四个主视角与 8 人八个主视角、绘画手牌和徽记、盾牌专用并拢 5、名字旁的负面状态、战斗记录抽屉及多人技能目标；截图见 [`Art/Integration/multiplayer/README.md`](Art/Integration/multiplayer/README.md)。战斗记录在所有模式默认收起，入口位于“返回标题”下方。

`GameBootstrap` 通过 `[RuntimeInitializeOnLoadMethod]` 自动创建并按需建 `EventSystem`，
**不需要场景配置、不需要预制体、不需要拖脚本**。

状态图标第三版：18 种状态统一由 `Core/StatusNames.IconKeyOf` 映射，避免 `Freeze/Frost`、`Stun/Panic` 枚举别名及多状态共用伤害类型引起错图。9 张新绘画和 2 个规范资源别名见 [`Art/Production/status-icons-v3/README.md`](Art/Production/status-icons-v3/README.md)。秘法手册通过 `GuideEntry.Status` 保留真实状态，当时按「伤害类型／状态一览／元素反应」分三页（现另有抗性与屏障第四页），第三页只有六条反应，心灵概率留在伤害类型页。多人三列六行容纳 18 种。当前验证结果及已复现的既有失败见 [`validation.md`](Art/Production/status-icons-v3/validation.md)。

新角色美术：正式 `Subclasses/paladin_oathbreaker`、`barbarian_path_of_the_berserker`、`fighter_tactician` 已导入；野蛮人·守望者道途换用用户选定的兽角图腾，原斧徽记用于野蛮人·狂战士道途。源图／提示词／选择清单见 [`Art/Production/new-roles-v1/`](Art/Production/new-roles-v1/README.md)。旧绘画图集重导入后自动重新应用本批 4 枚徽记，避免还原野蛮人·守望者道途。图鉴总览采用每页 12 名、4×3 翻页，筛选重置页码，从详情返回保留所在页；选将仍为拖动网格，末行预留边距。实际界面验收及既有双将强停复现见 [`Art/Integration/new-roles/`](Art/Integration/new-roles/README.md)。

## 战报与提示

- 回合出口统一由 `BoardController` 管理：无合法运算不等于结束比赛，冻结／双手不可用时保留选手阶段并提供跳过；AI 自动跳过。所有跳过路径设置待结算的回合结束者，燃烧与状态时长照常推进。震慑保留合法运算，攻击目标按 `StatusRules.CanAttackTarget` 过滤，无合法攻击目标才提供放弃。编辑器菜单 `Finger Dice Duel/Preview/Turn Skip and Give Up` 验证实际按钮，记录见 [`Art/Integration/turn-exits/README.md`](Art/Integration/turn-exits/README.md)。

- 技能流血在 `AttackContext.InflictsBleed` 上按次标记，由统一命中判定附加并记 `流血` 提示，回合开始扣血按实际中毒／流血状态命名；盾抵挡／太极／零伤害算命中，闪避／取消不算。战士·破阵者记录本次运算前完整盾及碎另一盾的实际变化，游荡者·风流剑客只在强化生效后标记，游荡者·刺客在运算前快照潜行，战士·剑士只标记独立追加攻击。追加命中立即降防，后续主攻击使用新防御；追加闪避自动结算，`PreTurnChoiceContext.IsResolvingAttack` 保证不依赖已改写的手值重算攻击类型。
- 牧师·生命领域的 4/6/8 转回复、9 和双 9 额外回复统一走 `LifeDomainLogic.CleanseAndHeal`：先清所有状态再回血，满血也净化，腐朽不阻止这次回复；双将两种位置同样生效。其他角色回复保留原判定。
- 牧师·生命领域相关抉择入口在收集到候选后返回“已接管流程”；AI 同步答完也须停止调用方旧步骤，避免重复运算、覆盖胜负界面。
- `BoardController.BeginTurn` 连续处理回合开始状态致死，直到轮到存活者或比赛结束；不可只重读一次行动者，否则多人连续流血致死会停在已阵亡角色上。

- 三名新角色的 AI 价值表已补齐：圣武士·破誓者重视 5、野蛮人·狂战士道途重视 9，战士·战术家沿用通用表；圣武士·破誓者转移使用 `OathbreakerTransfer`。
- 圣武士·破誓者通过 `IAttackTransfer` 在圣武士·奉献之誓代挡前转移整次攻击；运算前由防守者选择持盾角色，后续抉择交给 `ChoiceRequest.Decider` 对应角色生成，补收实际目标的防御与圣武士·奉献之誓代挡。结算重新验证持 5、存活、完整盾、非自己／攻击者；使用实际目标的抉择器。按钮按角色去重，8 人最多 6 名候选，保留【不发动】。`OathbreakerTransferTests` 覆盖 12 项真实流程与非法／过期目标回归。

- `Assistant/BattleTurnText.cs` 只格式化结算快照；回复显示实际增量，放弃攻击不写成零伤害命中。
- `Core/CombatText.cs` 统一“角色【技能】操作／结果”。技能实际生效后调用 `GameManager.NoteSkill`；不要把收集抉择、点击候选或 AI 意图记成已发动。
- 选将使用 `CharacterRosterView` 大徽记纵向拖动列表；双侧三列，单侧四列。保留末尾角色可达与选择回调。

## 开发约定

- 用户已授权：后续工作完成后，将本工作区的应入库改动提交并推送至 `https://github.com/cauchy418/Finger-Dice-Duel`。继续遵守 `.gitignore`，不提交引擎缓存和构建产物；推送失败时如实报告，不使用强制推送覆盖远程历史。

- 🚫 **绝对不要手工创建/编辑 `.meta` 文件**（GUID 格式很特殊，见下）
- 源文件与 **PowerShell 脚本都必须是 UTF-8 with BOM**，否则中文会乱码并报语法错
- C#：类型/方法 PascalCase，命名空间统一 `FingerDiceDuel.*`
- **非法操作不要静默失败**：`GameManager.ExecuteTurn` 对不合法运算抛
  `InvalidOperationException`，便于 UI/AI 层尽早暴露 bug
- **改规则要同步三处**：`RULES.md`、代码、`Assets/Tests/EditMode` 的对应测试
- **写角色图鉴必须对照篇幅基准** → 见 [`CODE-STYLE.md`](CODE-STYLE.md)。
  玩家明确要求「不要长篇大论」，并且已由
  `CodexTests.Codex_entries_are_not_over_long` **强制守住**。
  一句话：**有机制的角色，每段不超过机制简单角色的两倍**。
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
| ★7 | **"该谁决定" ≠ "轮到谁行动"** | 游荡者·风流剑客的闪避属于**防守方**；一律用行动方会让闪避选项点不到 |
| ★8 | **回合边界必须复位"抉择队列"与"队列索引"** | 只清队列不清索引 → `idx >= count` 被判成"问完了" → **下回合的提示完全不弹** |
| 9 | **抉择器必须在创建 `GameManager` 时就传进去** | 否则结算时找不到 provider |
| 10 | **`PreTurnChoiceReader.Read` 必须带"回退询问"** | 直连 `ExecuteTurn`（无收集步骤）时仍要能问出答案 |
| 11 | **询问顺序必须与结算顺序一致** | 闪避在结算里优先于护盾，问也必须先问闪避 |
| ★11b | **结算钩子里不能向玩家索要输入** | `ExecuteTurn` 一次跑完、无法暂停；在钩子里问 provider 的话**人类永远看不到弹窗**（AI 能当场答，所以表现为"有时生效"）。战士·破阵者连锁破盾曾因此形同失效 —— 必须改成"运算前收集 + 结算回读" |
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
| ★20 | **`AttackContext` 必须保存"运算前的双方手值快照"** | 否则效果判定会算错（曾把游荡者·风流剑客闪避整个跳过） |
| 21 | **多段攻击：攻防加成只能叠加一次** | 每段各自算会造成重复加成 |
| 22 | **判定触发条件要看"来源"，不要看数值** | 法师·咒法学派"无视护盾"是**身份**属性；按数值判会误伤其他角色的 8 |
| 23 | **看清条件是"运算源"还是"另一只手"** | 战士·剑士要求"另一只手与结果同值"，不是运算源 |
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

**需求**：牧师·生命领域的 4/6/8 要**先弹「攻击 / 回复」**，选完**再**点手牌选目标。
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
表现为"牧师·生命领域选了回复却按攻击结算"。

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

- [x] **前台与 1v1 美术运行时验收**（正在运行的编辑器菜单可执行点击、布局和截图检查；批处理仍不可用）
- [x] **2v2**（入门 / 经典 + 先攻界面 + 环形布局 + 攻击距离）
- [x] **8 人阵营混战**（单将、三阵营 3/3/2、50 轮、按阵营总血量判胜）
- [x] **8 人身份对局** —— AI 练习已接入；80 轮、主公 +10 / +1/+1、内奸 5 人亮身份、忠臣替死；真人对战占位
- [ ] **8 人角色/阵营平衡** —— 首版抽样中立仅 14.9%，需单独调
- [ ] 竞技（联网）
- [ ] 菜单按钮音效
- [x] 角色选将现为可竖直滚动的6列徽记网格，30人完整可选
- [ ] **空运算源**那次不该发生的第二次结算（根因未消除，已有守门与 `[FDD异常]` 日志）

## 已知风险 / 技术债

| 项 | 说明 |
|---|---|
| **批处理启动不可用** | `Tuanjie.exe` 批处理崩溃；已有运行中的编辑器可执行前台／1v1 预览检查，结果见 `Art/Integration/` |
| **平衡隐患** | 单回合最大伤害 8 < 一次回复 9，双方都只顾活命时可拖延；靠轮次上限兜底（见 `RULES.md` §17） |
| **AI 给队友回血未实现** | 1v1 里给对手回血是资敌；2v2 已支持队友但 AI 尚未利用 |
| **空运算源** | 2v2 AI 对局里偶发"用空运算源去结算"，已由两道守门拦住（不再抛异常、不再崩），改为记录 `[FDD异常]` 现场。**根因未消除** |
| **AI 游荡者·赌徒的"是否赌博"策略较简单** | 目前只判"有没有任何有用的运算"，未考虑血量 / 局势 |
| **诊断字段曾多次跨回合残留** | `LastIllegalOperation` / `_advanceTrace` / `_pendingGambleRoll` 都踩过，统一在回合边界清空（`ResetDiagnostics` / `SetupFirstStep`） |

## 2v2 集火策略（AI）

| 文件 | 职责 |
|---|---|
| `Assistant/GreedyChoiceProvider.cs` | `FocusPolicy`（None / Self / Mate）、`FocusBonus = 25000`、`IntendedVictim`；集火分**只加在攻击**上，且目标必须真的可达 |
| `Assistant/FocusPolicies.cs` | 生产策略：**默认所有角色 focusSelf**，黑名单 4 个（邪术师·旧日支配者 / 游侠·幽域追踪者 / 野蛮人·野兽道途 / 游荡者·魔术师）。邪术师·旧日支配者 2v2 62.5% / 8.7% 平局，先处理平局，暂不叠加集火 |
| `Tests/EditMode/FocusPolicyTests.cs` | 车道解析、只对攻击生效、近战打不到 Mate 车道时不退化 |
| `Tests/EditMode/FocusPolicyExperimentTests.cs` | 24 角色 × 600 局 × 2 段种子 × 3 组 = 86,400 局，写 `Tools/focus-report.txt` |

### 新角色触发测试

| 文件 | 职责 |
|---|---|
| `Tests/EditMode/SkillBleedAndCleanseTests.cs` | 21 项回归：五条技能流血路径、盾／闪避／取消／零伤害、潜行快照、普通攻击隔离、时长刷新、18 种状态净化、双 9／双将、连续流血致死推进及 AI 双九结算出口 |
| `Tests/EditMode/CollegeOfValorTriggerTests.cs` | 吟游诗人·勇气学院**真实行动**触发链：受伤 / 9 回血 / 满血回复不触发 / 中毒触发 / 2v2 队友受伤不触发；并验证 `CollegeOfValorAi.TryActivate` 接上触发 |
| `Tests/EditMode/CollegeOfValorTests.cs` + `CollegeOfValorChoiceFlowTests.cs` | 四步选择、强度、目标数、替换与死亡清除 |
| `Tests/EditMode/WildMagicTests.cs` / `TheGreatOldOneDistanceTests.cs` | 术士·狂野魔法 / 邪术师·旧日支配者的被动触发与距离结算（无触发窗口，不另做自动触发测试） |
| `Tests/EditMode/AiCharacterTests.cs` | AI 驱动冒烟：已覆盖术士·狂野魔法 / 邪术师·旧日支配者 / 吟游诗人·勇气学院；另有旧角色技能使用回归 |

```powershell
# 只跑集火实验（约 20 秒）
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll FocusPolicyExperimentTests
```

> 实验口径：同一局的三组共享**完全相同**的阵容 / 初始手值 / 术士·狂野魔法骰子，
> 只有受试角色的 AI 切换策略；队友与对手始终 baseline。
> 这样得到的是"**我**用集火，队伍是否更常赢"的配对比较。

## 双将模式（实现说明）

| 文件 | 职责 |
|---|---|
| `Characters/DualGeneralBehavior.cs` | **能力容器**：实现全部组合接口，按规则合并并转发给子角色 |
| `Characters/DualGeneralFactory.cs` | 创建入口、同阵营校验、副将候选、随机搭配 |
| `Tests/EditMode/DualGeneralTests.cs` | 合并规则与选将规则的单测 |
| `Tests/EditMode/DualVersusDualHarness.cs` | **交叉对战测试台**：84 个双将、3486 对 |
| `Tests/EditMode/DualVersusDualTests.cs` | 批量跑测试台并把报告写到 `Tools/dual-report.txt` |
| `UI/GameBootstrap.cs` | 选将界面、`PrepareDualMatch`、选将状态机 |

### 为什么是“容器”

> 上层到处是 `player.Character is IX` 的**单次 cast**（共 16 个接口），
> 而一个玩家只能有一个 `Character` ——
> 所以双将**必须自己实现那些接口**，再把调用转发给子角色。

### 分层

> `DualGeneralFactory` 在 **Characters** 层，**不在 Core** 的 `CharacterFactory` 里 ——
> 因为 **Core 不得引用 Characters**（分层铁律）。
>
> UI 层也**不引用 Characters**：“同阵营副将”由 `GameBootstrap` **注入委托**
> （`_dgPartnersOf = DualGeneralFactory.PartnersFor`）。

### 一个值得记住的坑

> `HandEffectType.None` 是**合法取值**（代表无效果），不能当“未找到”的哨兵值。
> 同类错误还有 `IHandEffectRewriter` 的 `out` 参数预置 0。
> 规律：**“默认值有意义”的枚举不能兼任“未找到”**。

## 光明、战争与战斗大师（2026-10-24）

当前37子职、12职业，守序13/中立13/混乱11，211合法双将。`IFixedNormalAttackDamage` 在所有普通攻击钩子之后固定光明伤害与类型，屏蔽攻防、倍率和失能增减；特殊、反弹、免费暗蚀和 `IsStatusDamage` 不接管。`AttackerStatusOverride` 不再要求状态施加接口才能生效。圣光19号经 `PlayerState` 统一净化并拦截状态/抗性，回复减半，回合开始1点不可挡。

战争狂热用目标通知/发起攻击叠层，自己回合结束未攻击清空；重开同时清内部层与动态面板值。战斗大师先确认攻击目标再预选招式/不附加，实际伤害>0才生效；面板图标和文字分区，避免覆盖。新徽记与圣光图标见制作清单；元数据由编辑器生成。

`FrontEndArt.WrapReadableText` 使用真实字体宽度，统一角色详情、手册与规则弹窗排版，保留数字单位、禁止孤立标点、调整短尾行。图鉴交互允许为空，不再强制凑条目；已逐角色删减通用比较和错误交互。验证见 [记录](Art/Integration/cleric-martial/README.md)。
