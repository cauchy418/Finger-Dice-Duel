# CODELY.md

> 工程结构与开发约定。**规则权威文档是 [`RULES.md`](RULES.md)** ——
> 代码、测试、UI 都必须与它一致，改动规则先改那里。

## 项目概况

当前逐角色AI参考见[全角色AI策略与权重](Docs/ai-strategy-reference.md)：按54子职列出原始手值、组合与技能分、开局对手修正及实际1v1／2v2参数；L情境模型与P机会预览分开说明，权重不是固定策略占比。

技能选择UI按语义统一：选手直接点击战场合法手牌，不创建逐手按钮或全屏遮罩；选角色按主人去重后列出角色按钮；元素、抗性、招式、形态与是否发动保留命名选项。`WizardChoice.TargetsPlayer`覆盖回合边界请求，普通`HandEffect`分类覆盖攻击前／防守请求，均经原控制器回读与放弃出口。

阵营现状：奥法骑士守序善良，破阵者守序邪恶，剑咏者混乱善良，刺客与至高妖精混乱邪恶。本次五项只调整善恶轴，双将仍按守序／中立／混乱选择，当前18／17／19名、460种无序组合；新增征服／复仇分别为守序邪恶／守序中立。

**Finger Dice Duel**（手指骰子对决）：2D 回合制对战。每人两只手（初始 1），
每回合用自己的一只手与**另一名玩家**的一只手相加、取个位（和为 10 → 1），
结果数字决定效果：**4/6/8 攻击、9 回复、5 护盾**。生命 20，先归零者败。

支持 **1v1**、**2v2**（环形四座、1&4 一队 / 2&3 一队、攻击距离、先攻掷骰）
与 **双将对决（娱乐模式，1v1）**、**8 人身份对局**（随机身份，主公固定 1 号位）。
**52 名角色**，每个都有独立技能。

| 项 | 值 |
|---|---|
| 引擎 | 团结引擎（Tuanjie，Unity 中国版）`2022.3.62t15` / Editor `1.10.3` |
| 语言 | C#（源文件 **UTF-8 with BOM**） |
| 渲染 / UI | 内置管线 + 正交相机；uGUI + TextMeshPro |
| 测试 | 1436项功能回归通过；7项大规模统计为主动镜像错位改动前的规则快照，181个运行时脚本及Unity/NUnit条件编译通过；采样修正、源码指纹和七份当前报告见[本轮验收](Tools/Statistics/README.md)。历史界面验收保存在 Art/Integration/ |
| 美术 | 全部界面**运行时用代码构建**，无预制体 |

> 主动镜像错位改动前的52子职统计快照：1v1 **106,080** 局、2v2 **169,728** 局、425双将 **2,162,400** 局、集火 **187,200** 局；八人阵营 **2,000** 局；
> 8 人身份局另有随机/偏好各 **2,000** 局。

## 职业与子职

当前 54 个子职，12 个职业大类；德鲁伊已接入月亮/星辰/大地结社。唯一命名与归属表在 `Core/CharacterIdentity.cs`，职业中英文和大类图标在 `Core/CharacterProfession.cs`。正式 ID 为 `职业_子职`；旧 ID 通过 `CharacterDefinition.Find`、`CharacterCodex.For` 兼容，旧 `Classes/旧ID` 美术键也会归一化。新增角色应先登记身份，再登记定义、逻辑与图鉴。

大类图标在 `Assets/Resources/Art/Battle/v1/Professions/`，子职在 `Subclasses/`。代码与测试类型同步使用新子职名，枚举数值顺序不变；原有 `.meta` 原样移动，新元数据由编辑器生成。职业重构阶段的图标由 `Tools/ArtPipeline/import-selected-profession-icons.cjs` 导入；旧图集导入后须保留该覆盖步骤。

图鉴职业筛选与阵营筛选取交集，德鲁伊当前包含3个子职；详情依次为斜体小字职业中英文、大字子职中英文、阵营。职业迁移阶段技能只做名称与设定调整（如乘风突刺、空间刃、王冠威仪、勇气战歌），原数值、条件与流程不变。重构后的最新检查见 [验收](Art/Integration/profession-subclass/README.md)，完整旧名对照见 [制作记录](Art/Production/profession-subclass-v1/README.md)。历史截图与日志保留其当时含义。

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

`AlignmentMigrationTests` 13项保留迁移阶段27角色阵营与111对双顺序回归、数字分派、真实运算、自加、目标、穿透及两个跳过技能独立选择；1332次运算逐行保存在 `Tools/dual-alignment-probes.tsv`。完整结论与组合增减见 [检查报告](Tools/dual-alignment-audit.md)，真实界面见 [验收](Art/Integration/alignment-migration/README.md)。检查接口竞争及选定场景，不将零异常解释为所有局面无问题或数值平衡。

旧勇士+风流剑客倍率与旧跨阵营种子21639仍以测试专用原始容器保留回归；正式工厂不放宽跨阵营限制。旧制作/验收记录与历史统计保持当时含义，当前规则以本段、RULES和最新检查为准。

## 法师三学派（2026-10-23）

三所学派正式徽记已接入；新增塑能徽记，旧书杖划归法师大类；13类抗性与奥术屏障共有19项导出资源（18张新绘、1张复用），见 [制作记录](Art/Production/wizard-schools-v1/README.md)。当前54子职，守序18／中立17／混乱19，共460种双将；历史迁移记录保留当时人数。

`WizardChoice` 将回合开始免费攻击及死亡换手排队，在控制器安全边界暂停/恢复，不重复开始回合或结算状态。死灵有可用1自动进入角色目标选择并提供「放弃攻击」；2→1逐段由人类选择，AI自动；死亡时交换0～2对手值，可提前结束。`DeferWizardAttack` 在免费/偷窃攻击前收集防守抉择。回合开始、死亡通知、预言压制等钩子遍历双将部件。

防护算出2先选角色，再选 **物理整组、雷鸣、强酸、火焰、寒冷、闪电** 六项，`ResistanceNames.Choices/CanGrant` 同时限制UI、AI与技能API；心灵/力场/毒素/光耀/暗蚀不可由此赋予。`ResistanceNames.All` 仍保存13类通用图标。`IAbjurationSkill` 存储屏障；选择存储后不再走攻击目标/盾选择，抗性后、扣血前吸收并全部清空。原有场上保护范围保持不变。

`IWizardChoicePolicy` 供控制器与无头内核共享目标、抗性、屏障、免费暗蚀和死亡交换策略；`GreedyChoiceProvider.Wizards.cs` 实现。此前50角色已有显式手值配置，旧验收450组（50角色×3难度×2/4/8人）控制器推进检查。还修复偷窃攻击的目标/防御与一次收尾、妖精跳过目标、魔术师自加锁手、赌博无合法目标的结束路径。斩杀估算考虑当前攻防、抗性和屏障，仅预览不消耗；仍是启发式估算。详见 [AI检查](Tools/ai-strategy-audit.md) 和 [引擎验收](Art/Integration/wizard-schools/README.md)。

`CombatStatusIcons` 统一负面、抗性、屏障、资源与标记显示；图标栏最多72项，超出可视区域可滚动。秘法手册为伤害类型、状态、十一条元素反应、抗性／易伤／免疫、角色标记五页；第四页三组各13类型，第五页集中奥术屏障、气、猎人标记、虚假生命，使用完整资源键，元素反应不增加心灵条目。

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

**这套约束让逻辑可脱离编辑器测试。** 专项通常很快，含大规模统计的完整回归需要数分钟。

## 构建与验证

```powershell
# 本机.NET 6长时间统计关闭分层JIT
$env:DOTNET_TieredCompilation='0'
$env:COMPlus_TieredCompilation='0'

# 逻辑测试（含统计，需要数分钟，不需要编辑器）
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

状态图标第三版当时包含18种，圣光阶段曾为19种，加束缚／油腻后曾为21种；当前21种负面与圣光／剑歌／战恩3种角色状态，统一由 `Core/StatusNames.IconKeyOf` 映射，避免 `Freeze/Frost`、`Stun/Panic` 枚举别名及多状态共用伤害类型引起错图。9 张新绘画和 2 个规范资源别名见 [`Art/Production/status-icons-v3/README.md`](Art/Production/status-icons-v3/README.md)。秘法手册通过 `GuideEntry.Status` 保留真实状态，当时按「伤害类型／状态一览／元素反应」分三页（当前为五页，见上文），第三页当时只有六条反应，此后增强酸／中毒互刷与火焰／油腻至九条，现增加雷电互刷共十一条，心灵概率留在伤害类型页。该阶段多人布局为三列六行；现有状态栏可滚动。该阶段验证结果及已复现的既有失败见 [`validation.md`](Art/Production/status-icons-v3/validation.md)。

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
- [ ] **8人角色/阵营平衡** —— 最新50角色采样仍有阵营差异，见 `Tools/8p-report.txt`；需单独调整
- [ ] 竞技（联网）
- [ ] 菜单按钮音效
- [x] 50子职可完整选取；大徽记纵向拖动，双侧三列、单侧四列；图鉴12名一页并按职业/阵营筛选
- [ ] **空运算源**那次不该发生的第二次结算（根因未消除，已有守门与 `[FDD异常]` 日志）

## 已知风险 / 技术债

| 项 | 说明 |
|---|---|
| **批处理启动不可用** | `Tuanjie.exe` 批处理崩溃；已有运行中的编辑器可执行前台／1v1 预览检查，结果见 `Art/Integration/` |
| **平衡隐患** | 基础攻击4/6/8与回复9可能形成拖延；技能可突破此基础伤害范围。轮次上限兜底，模式统计见 `Tools/*-report.txt` |
| **AI治疗仍是启发式** | 生命领域与圣杯已按实际缺血选择队友；尚未穷举未来收益、净化价值和所有双将组合 |
| **空运算源** | 2v2 AI 对局里偶发"用空运算源去结算"，已由两道守门拦住（不再抛异常、不再崩），改为记录 `[FDD异常]` 现场。**根因未消除** |
| **AI 游荡者·赌徒的"是否赌博"策略较简单** | 目前只判"有没有任何有用的运算"，未考虑血量 / 局势 |
| **诊断字段曾多次跨回合残留** | `LastIllegalOperation` / `_advanceTrace` / `_pendingGambleRoll` 都踩过，统一在回合边界清空（`ResetDiagnostics` / `SetupFirstStep`） |

## 2v2 集火策略（AI）

| 文件 | 职责 |
|---|---|
| `Assistant/GreedyChoiceProvider.cs` | `FocusPolicy`（None / Self / Mate）、`FocusBonus = 25000`、`IntendedVictim`；集火分**只加在攻击**上，且目标必须真的可达 |
| `Assistant/FocusPolicies.cs` | 生产策略：**默认所有角色 focusSelf**，黑名单 4 个（邪术师·旧日支配者 / 游侠·幽域追踪者 / 野蛮人·野兽道途 / 游荡者·魔术师）。旧日支配者基于早期实验暂不叠加集火；最新表现见统计报告 |
| `Tests/EditMode/FocusPolicyTests.cs` | 车道解析、只对攻击生效、近战打不到 Mate 车道时不退化 |
| `Tests/EditMode/FocusPolicyExperimentTests.cs` | 50角色 × 600局 × 2段种子 × 3组 = 180,000局，写 `Tools/focus-report.txt` |

### 新角色触发测试

| 文件 | 职责 |
|---|---|
| `Tests/EditMode/SkillBleedAndCleanseTests.cs` | 21 项回归：五条技能流血路径、盾／闪避／取消／零伤害、潜行快照、普通攻击隔离、时长刷新、18 种状态净化、双 9／双将、连续流血致死推进及 AI 双九结算出口 |
| `Tests/EditMode/CollegeOfValorTriggerTests.cs` | 吟游诗人·勇气学院**真实行动**触发链：受伤 / 9 回血 / 满血回复不触发 / 中毒触发 / 2v2 队友受伤不触发；并验证 `CollegeOfValorAi.TryActivate` 接上触发 |
| `Tests/EditMode/CollegeOfValorTests.cs` + `CollegeOfValorChoiceFlowTests.cs` | 四步选择、强度、目标数、替换与死亡清除 |
| `Tests/EditMode/WildMagicTests.cs` / `TheGreatOldOneDistanceTests.cs` | 术士·狂野魔法 / 邪术师·旧日支配者的被动触发与距离结算（无触发窗口，不另做自动触发测试） |
| `Tests/EditMode/AiCharacterTests.cs` | AI 驱动冒烟：已覆盖术士·狂野魔法 / 邪术师·旧日支配者 / 吟游诗人·勇气学院；另有旧角色技能使用回归 |

```powershell
# 只跑集火实验（耗时随机器与角色池变化）
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
| `Tests/EditMode/DualVersusDualHarness.cs` | **交叉对战测试台**：当前460个双将；旧425组合样本为90,100对、每对24局，共2,162,400局 |
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

该阶段39子职、12职业，守序13/中立13/混乱13，234合法双将。`IFixedNormalAttackDamage` 在所有普通攻击钩子之后固定光明伤害与类型，屏蔽攻防、倍率和失能增减；特殊、反弹、免费暗蚀和 `IsStatusDamage` 不接管。`AttackerStatusOverride` 不再要求状态施加接口才能生效。圣光19号经 `PlayerState` 统一净化并拦截状态/抗性，回复减半，回合开始1点不可挡。

战争狂热基础0/0，攻击后涨攻、受攻击后涨防，均用整次攻击结束通知，自己回合结束未攻击清空；重开同时清内部层与动态面板值。战斗大师先确认攻击目标再预选招式/不附加，实际伤害>0才生效；面板图标和文字分区，避免覆盖。新徽记与圣光图标见制作清单；元数据由编辑器生成。

`FrontEndArt.WrapReadableText` 使用真实字体宽度，统一角色详情、手册与规则弹窗排版，保留数字单位、禁止孤立标点、调整短尾行。图鉴交互允许为空，不再强制凑条目；已逐角色删减通用比较和错误交互。验证见 [记录](Art/Integration/cleric-martial/README.md)。

## 四个新增子职正式徽记（2026-10-24）

术士·龙族血脉、游侠·猎人、武僧·散打宗、武僧·四象宗的4枚绘画徽记已导入。龙族血脉采用用户确认方向的赤红雾气与金色脉络虚化龙影，保留原版下方元素宝石、中央琥珀与旧金框；实体龙首备选留存。

资源位于 `Assets/Resources/Art/Battle/v1/Subclasses/`，同一正式资源键用于选将、图鉴、1v1与多人面板。该阶段37个子职与12职业共49枚正式徽记通过尺寸、Alpha、哈希及无重复校验。新四枚导入步骤挂在旧图集总导入末尾，避免被覆盖；元数据由编辑器生成。

全量1151项测试通过，UI及Unity条件编译通过；真实引擎68项检查覆盖37角色资源、四枚选将与图鉴、职业筛选、旧ID别名及1v1/4人/8人显示。素材和精确提示词见 [制作记录](Art/Production/four-role-emblems-v1/README.md)，截图见 [验收](Art/Integration/four-role-emblems/README.md)。

## 气与猎人标记显示（2026-10-24）

Core新增只读契约 `IKiResource`、`IHunterMarkSource`，散打宗、四象宗及猎人实现；`CombatStatusIcons.For(player, game)` 经 `CharacterBehaviors` 遍历双将部件，不引用Characters或Unity。气显示当前资源（包括0），标记反查存活来源并合并到被标记者，多来源角标为人数。`GameBootstrap` 每次刷新将当前GameManager传入战斗板，1v1和多人栏共用模型；对应攻击消耗标记后自然移除。

两枚PNG使用 `Effects/ki`、`Effects/hunters_mark`，当时上限36项（现72项）；角标完全置于图标裁切范围内。秘法手册当时第四页增加资源与标记分组，现已独立第五页，文字说明角标口径；防止孤立标点换行。技能规则、数值与AI不变。

全量1153项测试通过，UI及Unity条件编译通过，真实引擎33项检查通过。素材/提示词见 [制作记录](Art/Production/martial-resource-icons-v1/README.md)，界面与重放见 [验收](Art/Integration/martial-resource-icons/README.md)。

## 四个新子职的玩家与AI选择（2026-10-24）

`SkillChoiceOption` 保存命名选项、图标和内部值；`ChoiceRequest` 可携带选项回调，空手牌候选不再过滤掉有命名选项的请求。散打/四象在确认攻击目标后用原运算前队列收集，答案存角色本次状态，结算回读并清空。双手同点将增加的气提前计入可选项，未实际攻击不扣气。

`WizardChoice` 增加命名选项，用于龙族血脉首次自己回合选元素；7的猎人标记在运算完成后的安全边界暂停，候选按人物去重、文字不带手名。两项排队时不直接向人类取默认值；显式确认后继续一次。龙族血脉不预先赋予火焰抗性，重开清除选型状态；四象/龙族实现 `IStatusApplyingAttack`，复用命中判定与元素反应。

`ISkillChoicePolicy` 由Assistant实现，`GameManager.SkillPolicyFor` 注入真实AI对象，不能从人类sink反查AI。Greedy专属策略选择血脉元素、猎人标记目标、散打招式和四象元素/留气；直接无头结算读取同一策略。UI统一命名选项模态框，复用既有绘画图标。

全量1170项、17项专项及104项真实引擎检查通过，UI与Unity条件编译通过。操作、截图和重放见 [验收](Art/Integration/martial-choice-fix/README.md)。

## 德鲁伊形态接入（2026-10-24）

Core的 `IDruidForm` 暴露结社与形态，`DruidForms`集中维护图案键、中文名称和规则提示，`IDruidChoicePolicy`承载AI选择。月亮/星辰的回合开始、结束分别排队 `WizardChoice`，玩家可保留基础/当前形态；形态按 `TurnCount`防重复询问，重开清零。当时两结社同属混乱，可双将叠加；现星辰移至守序，两者不能正式搭配，底层历史回归保留。类型竞争按主将优先；其他加值遵循原双将规则。

巨龙座在选择第一次加数后独立选择第二只手，允许重复。`GameManager.DruidSecondAddend`在结算前保存，完成/取消/重开清空。预览、抉择、最终运算和攻击快照共用连续两次归一化结果，效果及状态修正只处理最终值；战报写出两段算式。直接内核调用从同一AI策略选第二加数；第二手再次验证合法性。

圣杯在结算后安全边界选择额外治疗对象；人类可选其他存活角色/放弃，AI优先受伤队友，不给敌人无意回血。狼按实际伤害吸血；蛇和射手通过统一命中接口附加元素状态。Greedy形态策略考虑生存、距离、抗性、队友治疗需求；评分鼓励凑10、识别熊4合盾/鹰6不攻击，并评估巨龙最终结果。

`BaseRangeOf`优先读取容器聚合接口，修正双将此前仅取主将射程而丢失副将距离的问题。右侧形态提示在1v1玩家区域和多人各自面板中绘制；当前形态高亮，其余变暗，悬停同源说明，双将各一列。图鉴详情有独立形态图库；资源使用BattleArt完整键。51枚职业/子职与7张形态图通过尺寸/透明度/哈希校验，元数据均由引擎生成。

全量1184项测试、11项专项及83项实际界面检查通过；验收见 [记录](Art/Integration/druid-forms/README.md)，素材/精确提示词见 [制作记录](Art/Production/druid-forms-v1/README.md)。

## 第一章：边境哨站十战（2026-10-24）

剧情冒险→章节列表→“第一章：边境哨站”→初战至终战滚动列表，各战独立PlayerPrefs键`FingerDiceDuel.Adventure.LevelN.Completed`，保留初战旧键。前三战通关可跳入安全自由练习；四战重打完整指引，五战每次真实投骰。完成前九战可继续下一战（九战进入终战选角）。所有关卡共用`Backgrounds/border_outpost_chapter_01`，退出清理后恢复普通桌面。NPC使用旧头盔徽记，不增可玩角色。

`AdventureLesson`共享导演、点击与选择白名单、安全AI；`AdventureLevel1..10`保存关卡数据。初战7步基础教学，次战5步十点重击。三战7步：2/1对3/预成盾5，合盾→受击连锁→再合盾修复双盾→教官5＋5→1→主动盾5＋1＝6（7伤/流血）→教官4＋6→1→安全练习。教程不替玩家自动作答；必须发动的连锁/射程隐藏放弃，须保留盾时隐藏发动，防止误选后卡关。自由阶段无此约束。

`IShieldBreakChoiceProvider`由破阵者实现，控制器在实际防守盾确定后暂停，收集被动连锁，再继续同次结算；不再用加数手猜盾。双盾由玩家先选防守盾，连锁可选另一面完整盾，拒绝不循环。`_preTargetRangeAnswered`记住已答的射程选择，确认目标时不重复提问。`ConfirmTargetPlayer`拒绝不可达攻击，四战配置固定占位时，射程选择后仍回到目标确认。普通1v1原有射程抉择后直接结算流程保留。

四战的石头由`GameManager.ConfigureSeatObstacles(1,1)`计入物理座位间隙，GameManager仍只有2名角色，石头不进合法操作、目标、行动或胜负名单；交换座位后间隙仍属于物理位置。石头显示−/−、无Button，移到上方两侧，避开下方放弃/技能按钮。先借远手算4→超距放弃，再合盾，最终8碎右盾换∞造成9伤与流血；`IsComplete`以真实命中为训练目标，显示训练完成、教官仍存活。

五战`UsesClassicSetup`先仅准备画面、不BeginTurn。开场“学习先攻”打开现有DiceRollScreen；分配结束设置真实先手再BeginTurn。投骰左侧老兵头像/动态旁白解释分配/行动顺序、当前骰点、先攻者及大失败代选；上方规则提示与姓名/顺序分行排版。`SetLesson`统一复位教学专属标题、字号/位置、旁白、返回按钮，普通骰页恢复原布局。五战使用Greedy AI、无ScriptedRestAllowed，正常胜负；结果面板区分胜利/失利/平局，只有胜利记通关，重试新建对局并重新投骰。投骰页可返回章节。

`AdventureGuideHistory`只保存已解锁文字，不持有对局状态；上一页/下一页/回到当前，浏览旧页时刷新和新步骤不强制跳走，跟随当前时正常前进；新关卡清空记录。翻页按钮位于指引面板底部，正文保留独立空间。

三份设计先落盘再编码：[三战](Docs/Adventure/level-03.md)、[四战](Docs/Adventure/level-04.md)、[五战](Docs/Adventure/level-05.md)。新增12项后续关卡专项、3项指引历史专项，全量1215项通过，Unity条件/UI编译通过；含初战/次战原16项回归。实际引擎138项检查覆盖老兵投骰解释、真实随机先攻、失败与胜利、重投/返回、石头避让及翻页，见[最新验收](Art/Integration/adventure-later-training/README.md)。旧双教学[验收](Art/Integration/adventure-chapter-01/README.md)保留历史截图。占位石头以内置imagegen绘制，[原图与提示词](Art/Production/border-outpost-stone-v1/README.md)；不手改meta。挑战仍预留。

`AdventureChapter`统一十战名称、入口提示、固定训练角色及终战战士池；初战对手使用AdventureLevel1.GuideName“老兵”，开场为“身负旧伤的老兵拦住了去路”，各步指引和战报姓名一致；第二至九战使用InstructorName“哨站教官”。终战开场明确显示实际所选子职，骑士属性与经典流程单独分行。六战魔射手单NPC20生命，连续穿盾8伤、增伤8伤、两段4先破盾再击杀，NPC主动改变盾手并重新正常合盾，无中途造盾。八战剑士追加固定4先附流血，再主7，共11；九战战术家体力差0/5/13，动态攻防0/1/4，攻击5/8/13。七战40生命、训练射程2教官以普通核心动作展示四状态，`Beat.Maneuver`和`AllowManeuver`只约束教学状态按钮，最后必须拒绝附加；NPC算出8因震慑无目标而放弃，倒伏下一击4→6后完成目标，NPC仍可存活。普通/终战抉择不受限制。跛脚后的实际射程同步显示于1v1和多人面板。

终战`AdventureFinaleSelect`独立选角，`AdventureChapter.FinaleRoster`仅包含已注册的6个战士子职，三列两行完整可见；详情跟随选择。对手`AdventureChapter.Knight`为关卡定义（None逻辑，20生命，攻防+1/+1、射程1），不加入BuiltIn，也不使用王冠之誓。`Adventure/knight_original.png`直接复用`Art/Production/painted-icons-v2/native/Subclasses/paladin_oath_of_the_crown.png`原骑士徽记；选角与战斗都用同一资源。终战复用第五战真实经典流程、Greedy AI，重打保留选择并重新投骰，返回章内可重选。胜利保存Level10及`FingerDiceDuel.Adventure.Chapter1.Completed`，失败/平局不覆盖既有记录。所有新增关卡共用哨站背景，退出清理章节专属状态。

五份设计先落盘：[六战](Docs/Adventure/level-06.md)、[七战](Docs/Adventure/level-07.md)、[八战](Docs/Adventure/level-08.md)、[九战](Docs/Adventure/level-09.md)、[终战](Docs/Adventure/level-10.md)。新增`AdventureChapterFinaleTests`10项，实际控制器覆盖完整演算、四状态与必选门禁、6子职池、终战无安全AI及重开；旧关卡专项保持通过；全量1225项、Unity条件/UI编译、实际引擎175项主流程通过；姓名/终战开场文案补充后，41项冒险专项与44项引擎复验通过（引擎合计219项）。最新编译/回归/引擎与截图见[验收记录](Art/Integration/adventure-chapter-finale/README.md)。此前138项五战验收保留历史版本口径。

## Codely Structured Memories

### User

### Feedback
- [2026-10-24 06:09:37] 做新徽记等美术资源必须与现有徽记画风一致（用户要求）：沿用 Art/Production/champion-heavy-strike-v1/prompt.txt 提示词模板（旧金双层圆框+四方位菱饰+近黑珐琅内底+写实手绘+雕刻金属纹章主体），以已定稿徽记 PNG 作参考图，产出 512×512 透明 PNG，经 selected-manifest 清单（SHA-256）+ Tools/ArtPipeline 导入脚本入库；状态图标走 status-icons-v3 风格迁移模板（黑铁底旧金方框）。用户还会点名让我判断现有图案与新角色主题是否匹配（先例：圣杯经纹章学分析改判给光明领域、生命领域另绘）。
- [2026-10-24 06:09:37] 实现类任务先交方案再动手（用户两次明确要求"先列工作清单/设计稿给我看，检查后再开始"）：开工前给出分阶段工作清单或设计稿供用户检查；规则口径有歧义时用带选项的问题让用户逐条拍板，未拍板不实现；文档三处同步（RULES.md/代码/测试）是项目铁律。

### Project
- 历史第二章“灰塔秘法”提案已归档；当前权威为 [法师高塔](Docs/Adventure/chapter-02.md)，已完成十关实现。

### Reference
- [2026-10-24 12:45:47] 冒险模式·剧情模式第一关（2026-10-24 已确认并实现）：勇士 vs 生命12旧伤白板士兵，7 回合强制脚本教学（攻击/合盾/碎盾/满十归一/回血/半自由决胜）；三个决策已拍板（士兵HP12、引导员头像=原士兵徽记 guide_soldier、不做三星）。权威设计文档已入库 Docs/Adventure/level-01.md；实现=Assistant/AdventureLevel1.cs（剧本+导演+收官保护，点击守门只在界面层）+ UI/AdventureGuidePanel.cs + 初稿AdventureLevel1Tests 4项（收尾增至8项，当前验收见上文）；RULES.md §20 已同步。
- 历史第二章“灰塔秘法”提案已归档；当前权威为 [法师高塔](Docs/Adventure/chapter-02.md)，已完成十关实现。

## 伤害保护与8人界面收尾（2026-10-24）

`DamageAffinities.cs`将`PlayerState`拆为partial，新增逐类型`Vulnerabilities/Immunities`和Add/Has/Clear接口。`ClearTemporaryDamageAffinities`一起清除临时抗性、易伤、免疫，保留永久抗性。`StatusRules.ApplyDamageAffinities`统一免疫优先、易伤向上取整、抗性向上取整；攻击在转移后快照`WasTypeImmune`，防止消耗后错误追加状态。全部段结算后按`LandedHits`消费一次，盾挡／闪避保留；零伤命中仍消费，物理免疫不阻止技能流血。

`RefreshAcidPoisonInteraction`仅在命中结算刷新既有对方状态至2，保留其来源；AI预览不修改状态。通用图标新增26张，雷鸣用声波、暗蚀为幽绿；精确提示与来源见[制作记录](Art/Production/damage-affinities-v1/README.md)。现有角色的抗性赋予范围仍为原六项，没有新增易伤／免疫授予能力。

奉献冲击的8人问题包括：身份控制器使用了与对局不同的答复容器，结算可能误选回退目标；候选曾含超距加数角色。现在复用真实sinks，并通过`AnsweredLookupFor`读已选目标、校验射程。`ResolvingPassiveShieldBreak`区分被动破盾通知，避免OnOwn/OnOther双触发；主动旧盾碎＋新盾成仍各冲击2点、各选目标。

`IdentityPresentation`将身份阵线与战斗Teams分开：只按存活同阵线计数；已知关系才标队友／对手，未知关系为`RingRelation.Unknown`。身份8个独立战斗队伍、可攻击范围与友伤规则均保留。

新增16项伤害保护、6项八人专项；全量1247项通过。Unity条件/UI编译通过，实际引擎212项包括全部图标、手册滚动、阵营／身份真人按钮冲击及四身份视角，见[验收](Art/Integration/affinities-and-eight-player/README.md)。七份统计报告重跑，README117项角色胜率及模式摘要同步。

## 共同规则与冒险旁白排版（2026-10-24）

游戏内`RuleBook.Full/BasicRules`重写为运算、数字效果、攻防／距离／防护、先攻、跳过、状态与模式胜负的共同规则；删除具体子职和专属技能举例，图鉴仍保存完整技能，RULES仍为详细规则参考。`FrontEndArt.WrapReadableText`按完整数字词组调整末行，避免拆开数字、单位或将下一行挤出宽度。`SetNarrationText`按固定面板高度微调字号，过程指引连续排版，开场与总结保留段落；同样用于先攻老兵提示。覆盖十关开场／全部Beat／总结与先攻，并检查规则滚动页；见[验收](Art/Integration/narration-and-common-rules/README.md)。

## 束缚／油腻与圣光图标（2026-10-24）

`StatusEffect.Restrained/Greased`为20/21，登记名称、说明、图标及颜色；`PlayerState`显示枚举上限同步。`GameManager.MovementStatuses.cs`共享`StatusRandom`与行动序号：束缚每次自身行动只选择一次手，`Hand.IsRestrained`与原技能`IsLocked`独立；合法步、直接入口、偷窃、赌博、跳过、UI与AI一致。清除状态立即撤销临时限制。油腻只在`ResolveTurnCore`完整运算后调用一次（赌博排除），触发后对倒伏延后本次Tick，确保两个后续自身回合；跳过与偷窃不触发。

火焰／油腻反应优先于覆霜／潮湿熔化，增伤一次，命中后只清油腻并附燃烧。免疫快照、盾挡／闪避口径沿用通用规则。元素AI加油腻收益，免疫元素不会因联动奖励反被优先选择。14项新增`MovementStatusTests`覆盖随机两端、稳定手选择、锁手叠加、净化／重开、真实控制器、直接API、持续时长、火焰盾挡／闪避／免疫、反应优先级与预览。

新图标[制作记录](Art/Production/status-icons-v4/README.md)：束缚按用户补充选藤蔓，油腻为油壶与油池，内置imagegen逐张绘制。圣光（用户称圣火）的可见框原约350px，原因是透明度极低的远处像素影响裁边；按Alpha>8重新裁取主体、统一448px并居中至512px，更新selected清单和运行时图片。没有放大UI槽位或改meta。实际引擎69项及截图见[验收](Art/Integration/movement-statuses/README.md)。

上一阶段收尾（2026-10-25）：1261项全量通过、0失败，Unity条件/UI编译通过；最新引擎验收为伤害保护与8人214项、老兵／共同规则排版798项、新状态69项。README保留入门规则、完整39子职图鉴、阵营、13伤害／21状态与全部胜率表；七份报告及117项角色胜率同步最终采样。

## 大地、古贤与妖魔（2026-10-25）

该阶段42子职／12职业，守序14／中立15／混乱13名、274种合法双将。`AncientRoleChoices`统一产生`WizardChoice`：大地3的可选束缚、结束时强制选手封锁，古贤碎盾的可选束缚与高血受伤后的强制治疗均归技能持有者决定；玩家请求进入控制器，AI通过`IAncientRoleChoicePolicy`立即选目标。妖魔通过`IPreTurnChoiceProvider`与命名选项收集失能／恐慌／保留，含图标和耗尽提示，不再默认失能。

`EndTurn`运行角色结束钩子时，当前行动者已轮转；大地不能再用`CurrentPlayer==owner`否决结束钩子。手的封锁按来源集合保存，与旧锁手、束缚分离，只限制加数；下次来源回合开始、阵亡与重置清理自己的来源。古贤用`ResolvingPassiveShieldBreak`避免一次被动破盾提示两次。

双将溢出治疗和实际受伤钩子通过`CharacterBehaviors.Implementers<T>`发现所有部件。`IPostDamageTaken`在真实生命扣除后执行，虚假生命／屏障全挡不触发，额外真实伤害与回合开始持续扣血同样传递；生命归零不自愈。新增`IOnDamageDealt`供妖魔汲取使用，包括击杀后的目标；不放宽旧`IOnHitLanded`要求目标存活的语义。

三枚徽记与绿色虚假生命标记用内置imagegen绘制，黑铁底、旧金框、绘画主体；完整提示词、原图和绿色精确编辑留在[制作记录](Art/Production/ancient-roles-v1/README.md)。圆徽480px主体、方标448px主体，均512px RGBA，canonical清单现54项（42子职＋12职业），导入器最后应用新版，所有meta由引擎生成。

`GreedyChoiceProvider.AncientRoles.cs`实现封锁收益、敌方束缚、强制治疗和契约控制／留防护决策；斩杀与伤害评分扣除虚假生命，预览不消耗资源。378组42角色控制器矩阵均可推进。17项新选择回归加4项基线角色回归，实际引擎/UI108项，覆盖职业筛选、三个图鉴详情、绿色标记、真实技能点击与4/8人最后一只手可点击，见[验收](Art/Integration/ancient-roles/README.md)。

### 至高妖精与星辰阵营调整（2026-10-25）

至高妖精由守序邪恶移至混乱中立；星辰结社由混乱中立移至守序中立。两者技能、数值不变；正式双将与8人阵营选将使用新归属，人数仍为守序14／中立15／混乱13，共274种双将。星辰不再与月亮结社组成正式双将；历史容器叠加回归保留，不放宽跨阵营资格。胜率报告按本次新角色池重生成。

上一阶段收尾：1286项全量通过、0失败，Unity条件编译及UI编译通过。新增17项选择回归、4项阵营／跳过回归，另有原4项新角色基线；实际引擎108＋21＝129项通过。全部七份报告和README的126项角色胜率已按42角色与最新阵营重生成。

至高妖精＋刺客有两个`ITurnSkipRule`：`GameManager.CanSkipTurn/CanSkipTurnFor`遍历所有部件取或，避免先部件遮住后部件。控制器同时满足时用玩家目标选择加“仅潜行等待”，单独妖精条件仍校验不满血；AI沿用原体力差收益判断。所有跳过仍通知潜行消耗。冲突扫描仅为这个有双序专项覆盖的组合放行该接口，其余单入口竞争继续报警。

## 逸闻学院、风暴术法与幻术学派（2026-10-25）

该阶段45子职、三阵营各15名、315种双将，职业徽记清单57项。新增稳定ID、工厂、旧别名、图鉴及各模式选将；[徽记原图、提示词和导入清单](Art/Production/lore-storm-illusion-v1/README.md)与[实机验收](Art/Integration/lore-storm-illusion/README.md)保留在工程内。

`ILoreRecord`暴露记录数／已有数字／成长层数；`CollegeOfLoreLogic`在真实运算后登记、结束边界提供成长选择。两个新防守技能采用命名`ChoiceRequest`，归受击者确认；`IWholeAttackDefense`登记整次攻击减半，在各段攻防修正后生效，防止攻击加成和负防御绕过减伤；不处理持续状态伤害，忽略取消及既有固定伤害屏蔽的情况。AI同接口只读估算，不删除记录或改手。

`StormSorceryLogic`开局通过`WizardChoice`选永久雷抗，结算时投30/30/40；额外运算分两次可取消的选手请求，玩家与`INewSubclassChoicePolicy`共用合法手范围。`GameManager.ApplyStormFreeOperation`只归一化、修正数字、设置手值并记录战报，不调用数字效果、破盾钩子、`OnOperationResolved`、状态Tick或回合推进。手值变更会清除该手原盾状态。

`GameManager.PrepareOperationAddend`在幻术选加数时缓存来源／原加数／实际加数／回合号，控制器先使用同一实际加数收集攻防选择，内核也复用；直接无头运算同入口。预览及合法步枚举不投随机数，执行完成和重开清缓存，额外无效果运算同样验证合法性。

手值表新增45角色完整配置；逸闻奖励未记数字，幻术偏好持2和差2，风暴余势按下回合手值与合盾代价评分，收益不足可放弃；防守斩杀估算减半只读。405组控制器推进、20项专项以及90项实机检查覆盖新角色与多人选择。

### 2026-10-25 失能最终伤害修复

旧流程只把分段前基础伤害清零，逐段仍加回攻击加成／负防御，共鸣额外真实伤害也绕过归零。现在缓存本次失能判定，逐段修正完成后仍保持0，并跳过附加真实扣血；攻击继续判定闪避、破盾、附加状态与命中钩子。AI普通／斩杀估算同步归0；光明领域既有固定4优先规则保留。新增7项失能回归覆盖16组加成／分段组合、元素／流血、护盾、真实伤害／虚假生命、AI追加斩杀与未失能对照。另补3项逸闻／幻术减伤回归，包含攻防加成、逐段和持续伤害不消耗技能资源。

持续伤害边界补充：`IsStatusDamage`不受发起者失能归零影响，避免失能反而免疫自身燃烧；妖魔契约和汲取也跳过此路径，不再因燃烧消耗自己的全部虚假生命、给自己失能或治疗自己。专项另加1项妖魔持续伤害回归。

本轮最终收尾：1317项全量通过、0失败；Unity条件与164脚本UI编译通过。实际引擎109＋21＋93＋41＝264项通过，完整57枚职业／子职哈希校验通过。七份报告和135项角色胜率均按45角色、最新阵营与315双将重生成；README保留共同规则、13伤害、21状态、完整角色与全部胜率。

### 2026-10-25 所有模式统一职业→子职选角

`CharacterRosterView`的Root为带固定标题／返回按钮的面板，内部`RosterScroll`负责裁切和滚动；职业页显示当前合法名单中有候选的职业徽记及人数，子职页显示该职业的子职徽记、简称和原有悬停技能提示。职业图从`BattleArt`的`Professions/`加载，沿用现有12枚资源；`FrontEndArt.RoleTile`增加可选资源键，不改变图鉴原有用法。

`_roster`始终保存调用者的完整合法名单；`VisibleIndices`将职业内卡片映射回该名单，`SelectionChanged`继续传原始名单索引，不能把职业内序号当成全局序号。职业导航不触发选角事件；玩家明确点击子职时即使原索引相同也确认选择，修复身份模式默认首项无法确认的问题。返回职业保留选择；同名单`SetRoster`刷新保留所在职业，名单变化（如双将主将→副将）返回职业页并重新计算可选大类。

`GameBootstrap.ShowScreen`统一调用`ResetProfessionNavigation`，覆盖1v1的双方、2v2的自己与队友、8人阵营、身份、双将（含8人双将）及剧情终战。重入选角从职业页开始，原有默认选择、摘要、开始按钮和角色合法范围沿用。终战只显示战士大类／6子职；双将副将只按同阵营且不重复的候选归类；非主公身份局仍排除主公已选子职。

验证：角色卡10项、职业契约2项；UI编译164脚本通过。实机228项遍历全部12职业／45子职，检查徽记、原索引、详情、返回与重入；182项覆盖1v1人机／真人的入门和经典、2v2入门开局与经典先攻交接、8人阵营／身份、双将1v1／8人和终战挑战，共410项通过。截图、脚本与日志见[选角验收](Art/Integration/profession-selection/README.md)。本轮只改选角表现与导航，沿用既有1317项完整逻辑回归及胜率报告，不重新生成对战数据。

## 五个奥术子职与AI收尾（2026-10-26）

50个子职、62枚职业／子职徽记；五个逻辑在 `ArcaneFiveLogics.cs`，形态血池、伤害通知、临时惑控覆盖与召唤座位生命周期在 `Core/ArcaneFive.cs`。9张导出素材含5徽记、羊头像／状态、幽灵头像、咒剑诅咒。所有玩家抉择均支持不发动，AI经 `IArcaneChoicePolicy`、`SkillChoicePolicy` 接入真实提供器；不能以人类答案槽替代AI策略。手册角色标记8项，新增变形、剑歌、诅咒、幽灵；奥法待用元素显示在持有者旁。

变化学派无序匹配，可选自己；变羊独立10生命、−2/−2、距离1，无原技能，原状态继续计时，解除恢复原生命，多段溢伤重新读取原防御。惑控在整次实际伤害结束后提示，后续掉血只移除惑控来源覆盖，普通同名状态不误删；周期伤害保持真实来源，虚假生命吸收不触发实际伤害技能。

奥法骑士主动用完整盾运算时，由控制器在同步 `ExecuteTurn` 前收集一次元素答案，实际 `OnOwnShieldBroken` 消费准备标记，5+1攻击立即采用本次附魔。非攻击保留附魔；被动碎盾受击后选择。附魔在未取消攻击开始时消耗，预览不消耗；力场复制仍穿盾、无视正防御、不可闪避。

剑歌维持必须读回合结束前的有效行动。`BoardController.BeginTurn` 先消费并清空 `_statusTurnEndFor`，结算旧回合、排空结束技能抉择，再调用 `GameManager.BeginTurn` 复位标记；不能先清 `HealedThisTurn` 再判维持。消耗哨兵早于执行钩子，防止人类抉择恢复入口重复结算。实际回血、攻击及合盾维持，满血或腐朽阻止回复不维持。

咒剑诅咒按目标自己的回合计时，每段+2；亲自击杀后复用死者座位为幽灵，下一轮行动，计距离不计原玩家胜负人数。`IsOriginalAlive`、`ActingIdentityOf` 与 `OriginalTeamOf` 区分原角色和召唤归属；幽灵由AI操作，召唤者死亡、替换或重开清除，座位修订触发UI重绑。

队伍AI使用 `IsKnownAlly`：普通模式遵守实际队伍，隐藏身份仅使用公开关系。奉献之誓冲击优先敌方，援护检查盟友、盾、免疫与自身生存，并覆盖额外攻击／转移攻击。生命领域可主动治疗或净化人类队友；防护、圣杯、勇气战歌等统一读取队伍关系，双将中的勇气学院通过能力枚举发现。连锁破盾启用，优先敌盾，不再因入口硬关闭而永不发动。

逐角色对照：50角色×两种模式，10候选、128训练、320独立留出，共190,400局；采用24项1v1配置，2v2均未过校正门槛而保留原权重。生产显式开启 `UseValidatedTuning`，8人／双将不套用单将结果。修复旧阻挡读取运算前数字、借队友加数被罚的问题。“3:7”不是整个AI决策的比例；新权重与实验边界见 [AI调参报告](Tools/AiTuning/README.md)，机制逐项核对见 `Tools/ai-role-review.tsv`，原始手值见 `Tools/ai-strategy-profiles.tsv`。

### 两种主动碎盾的攻击前连锁

破阵者完整盾作运算源的连锁原本就在攻击前；碎另一手换射程以前直接 `ForceBreakShield`，漏掉连锁通知。现经 `BreakActiveShieldAndNotify` 主动破盾入口，先回读连锁答案再创建攻击。玩家接受换射程后追加 `BreakerRangeChainBreak`，与运算源连锁的答案分开，允许各自放弃；AI共用敌盾优先策略，UI有破盾目标按钮。破阵者＋奥法骑士双将也在这次攻击前收集附魔，准备标记按手保存，避免双盾重复询问。8项专项覆盖4／8人远距、两类放弃、盾挡、流血与双将。

### 大规模双将统计复现（历史50子职）

`Tools/HeadlessTests/run-sharded-statistics.ps1 -Shards 8` 将76,636个配对分给独立进程，共1,839,264局；各局按配对和座位固定随机源，避免线程共享随机状态。合并严格核对构建MVID、392组合清单、每行配对／轮次／座位和总数，不接受缺失、重复、旧构建或额外行。汇总调用原测试台计分；双将AI也开启正式连锁破盾，单将调参不会套到双将。缓存和原始分片仅在忽略的bin内，最终报告入库。`-ReuseShards`仅复用同一构建，`-Only DualVersusDualTests`可只重跑该模式。功能回归可用 `run-tests.ps1 -SkipStatistics`；并行验证用独立 `-OutputDirectory`，不覆盖正在执行的DLL。

最终验证：1380项全量通过、0失败；开启连锁的正式双将AI另跑1,839,264局及3项专项，异常0；该阶段功能回归1373项、统计7项独立运行。Unity/NUnit与170脚本UI编译通过，142项实机检查及62枚徽记校验通过。七份模式报告、150项角色胜率与50角色手值／应对清单均已同步。

## 第二章：法师高塔（2026-10-26）

`MageTowerChapter`保存全局11～20关、37个独立试验及NPC定义；`MageTowerLesson`复用`AdventureLesson`导演，每个试验重新建局，固定NPC动作与普通角色核心结算分离。`GameBootstrap.MageTower`负责章节解锁、续试验、法师终战池、存档与过场。第一章胜利或旧Level10完成键均能解锁；关卡键沿用Level11～20，样本键由`MageTowerChapter.SampleKey`生成。临时试验成功先写样本键，最后样本成功才写关卡键；终战真实胜利才写章节键，失败不清旧记录。

第一章骑士胜利可经三段对话进入高塔目录。终战仅接受wizard职业；幻影为独立NPC，32生命、强酸、永久火焰与寒冷抗性／毒素免疫，普通攻击实际伤人三次后回3，不计持续伤害、反弹、特殊攻击或只扣虚假生命。变羊期间暂停NPC技能，恢复后保留回路进度。进度通过Core合约`INpcCircuitProgress`展示，Core不依赖Assistant实现。永久抗性／免疫通过真实来源查询显示，不以召唤物身份判断。

教学暴露并修复两处结算／界面问题：元素反应明确不附状态时不能回退到角色基础附加（火焰＋潮湿、寒冷＋燃烧）；死灵抵挡收集时排除已知力场攻击。其它竞技规则保持现有实现。

验证与截图见 [第二章验收](Art/Integration/adventure-chapter-02/README.md)。显式采样命令：`dotnet <HeadlessTests.dll> --tower-probe Art/Integration/adventure-chapter-02/finale-ai-probe.csv`，9子职×200相同种子，困难AI、真实骰点与合法分配；用于发现策略问题，不代替真人难度评估。

## 战争领域分离成长（2026-10-26，历史实现）

本段保留旧实现经过；当前战争领域已按下文改为战恩。

基础攻防改0/0。`WarDomainLogic`分别保存`AttackStacks`／`DefenseStacks`，`WarStacks`仅保留两者合计的兼容观测口。发起攻击后只增攻击，受到攻击后只增防御。统一`IAttackFinishedHook`在整次攻击结束后调用，不追溯影响本击；多段一次，独立反弹另算，盾挡／闪避／零伤仍算，取消／状态伤害不算。转移前目标与实际承受者沿用原目标通知口径，各自只计一次。自身回合未发起攻击则回合末清两类层数，重开也清零；双将两种顺序经部件遍历获取相同能力。

图鉴、角色表、RULES、README与策略说明同步。AI手值和模式权重保留，伤害估算读取真实独立攻防层；既有大规模胜率标为调整前统计，未冒充新版本胜率。

## 教学选择与结果观察（2026-10-26）

第二章前九课注入`ITutorialInput`。`MageTowerLesson.Choices`为当前技能请求提供手牌／角色、命名选项、元素、抗性、接受／放弃及换位约束；UI用相同规则禁用、描边指定按钮，`BoardController`各输入出口再次校验。只约束人类选择，NPC内部点击不被误拦；终战不注入规则。死者易手等多阶段请求须同步维护映射，全部37试验回归会检查错误输入不改变回合。

次战所有样本都有命中后的护卫观察回合。冻结走无合法操作的正常跳过；燃烧／中毒／感电／腐朽等按真实起止回合结算，不通过结算界面遮住最后结果。

`GameBootstrap.AdventureResult`统一两章留场：完成时先写进度、显示`TrialEnd`、暂停AI及手牌输入；点击后调用原结果卡。结算按钮不受战报遮罩拦截，退出／重开清理。四人指引位于中央安全横条，`LayoutRingActionTray`的LateUpdate也遵守该布局，避免下一帧覆盖阶段提示。

技能目标图标读取`PlayerState.BattleEmblemKey`；`BattleArt.Get`兼容NPC的旧Subclasses路径，护卫、幻影与原骑士都能解析。变形／召唤物继续显示自身战斗图案。最新截图、脚本和日志见 [验收](Art/Integration/adventure-tutorial-guidance/README.md)。

## 预言快照与完整教学选项（2026-10-27）

`IActionInvalidator.CanInvalidateAction`为无副作用预判：7记录另一手，窗口内其他角色运算前任一手匹配即压制。`GameManager.ResolveInvalidatedAction`仍写入普通加法与状态修正后的真实手值及战报快照；`Effect=None`，不发动攻击、回复、合盾、加数触碰、碎盾、运算后或新的回合末技能。5为普通数字；原盾参与运算会失去盾态，附属的守望者锁手随盾态到期，但不发动碎盾奖励。普通额外运算同样判定；免费攻击不属于运算。回合与状态计时照常，战争领域旧加成清除和已有剑歌失效仍收尾，重开清理压制标记。

控制器在选定加数后直接提交受压制运算，跳过幻术改算、巨龙二次加数、攻击目标与技能请求；奥法骑士预收集与赌博的攻击目标确认也遵守压制。AI共用纯预判，只评估保留手值与后续运算潜力，不领取即时攻击／回复／合盾或技能分。战报保留真实算式，注明“预言：仅改变数字，无其它效果”。图鉴、README、RULES、策略清单与第二章七战脚本同步：护卫1+7→8无伤；窗口关闭后玩家4+4→8、护卫8+8→6正常结算。

教学约束不参与按钮显隐。正常可选项全部显示，只有规定选项可点并描金边，其余置灰；接受、放弃、命名技能、元素、抗性与战斗招式遵守同一原则。第一章旧导演的必选接受／拒绝与战斗大师招式也采用该表现，回调保留规则校验。验收与截图见 [完整选项与快照验收](Art/Integration/tutorial-choices-and-divination/README.md)。

## 战恩与两个圣武士誓言（2026-10-27）

该阶段52子职、12职业，守序18／中立17／混乱17，共425种双将。64枚职业／子职徽记通过资源清单校验。战争领域重做为基础−1/−1、∞射程的回合开始施恩：按两只手的攻击数字数选至多两名不同目标，可选自己、少选或不选。战恩刷新不叠加、攻防+1，9可改为基础9攻击；普通运算、赌博和有效偷窃均在人类结算前选择。

`Core/DivineOaths`集中状态选择、仇敌标记合约与额外纯运算。`BoardController.Divine`复用安全边界，9选择后恢复正常目标／回复流程，`OperationEffect`贯通预览和防守请求。`StatusRules`叠加战恩，`ClearNegativeStatuses`保留战恩；圣光在统一入口清战恩并调用`IBladesong.EndSong`，剑歌进入及延后回答均校验圣光。

征服之誓的层数属于自身部件，双将不覆盖其他动态加成；`AttackContext`记录攻击开始时的实际攻防，恐慌按快照比较，整次攻击后增长。回合末只递减自己的层数，最低0。复仇之誓开局必选，普通受击转移在防守选择前收集，仇敌死亡可重选。`ITargetDamageBonus`逐段计算+1／−1，`ShieldBreaker`保存并恢复实际破盾者；每面盾分别排一次可放弃的纯运算，只点战场手牌，不列手列表，不推进回合与状态。

该阶段手册状态一览分20种负面与3种角色状态；剑歌、战恩使用独立方框图标，仇敌誓言放角色标记页。AI明确选择施恩对象、仇敌、转移、9的攻击／回复与纯运算；新誓言及重做战争领域使用L／H12／D43基线，旧战争领域P12／D100实验配置停用，导出脚本同步排除，未声称已完成新版本胜率调参。

验证与真实UI截图见[誓言验收](Art/Integration/divine-oaths-v1/README.md)，原图、完整提示词与清单见[美术制作](Art/Production/divine-oaths-v1/README.md)。该次功能验收尚未重采样；最新52角色／425组合胜率已于下节重测，七份当前报告已替换。

## 当前规则胜率重采样（2026-10-27）

52子职、425种双将，七项共2,631,408局、异常与模拟器强停0。1v1为106,080局，2v2为169,728局，双将2,162,400局，集火187,200局；八人阵营及身份随机／偏好各2,000局。上述历史实现日志保留时不再作为当前强度结论；README、RULES及七份报告已同步，机器汇总见[results.json](Tools/Statistics/results.json)。

统计台已修正：1v1实际交换物理座位，逐结果保留先手标记；双将FirstWon表示第一组角色获胜，汇总不可再按座位反转；身份偏好按洗牌后的座位索引存回。八人“作为2人阵营”按实际局数作分母。旧手写对局循环改用Tests.TestSetup.RunSampling与实机BoardController，覆盖技能选择、赌博、偷窃、目标、击杀奖励及额外运算。

Tests.TestSetup.SeedSampling按局固定状态及三个随机角色的随机源；SeedControllerSampling固定赌博、普通AI魅惑走子及幽灵AI随机源。GreedyChoiceProvider.SetRandomSeedForTest只注入原随机源，不改正常游戏的策略或默认随机行为。八进程分片合并从头部动态算预期行数，并验证构建MVID、名单、配对／轮次／座位和唯一性。数据记录采样基线提交、源码指纹及报告SHA256，不将重新构建的DLL与旧分片混用。

新改三角色的技能AI已完成，维持L／H12／D43与既有集火配置；旧独立留出中的23项1v1配置继续使用。本轮不是重新调权重实验，不把胜率表写成最优策略证明。现有五项统计回归新增通过，功能回归1428项；七项统计独立通过。完整复现、分母和限制见[采样说明](Tools/Statistics/README.md)。

## 幻术学派主动镜像错位（2026-10-27）

IOperationAddendIllusion改为持2的资格查询，不再有随机数。GameManager.RequestOperationAddend在运算前创建持有者的WizardChoice，提示行动者、运算手、原加数与替换手；两项为发动／不发动。控制器在该边界暂停，回答后才建立后续攻击／治疗等选择；直接内核运算用IIllusionChoicePolicy同步决定，缺省保留原加数。随机Rng／ForcedRoll与相应测试注入已移除，整次减伤技能保留。

缓存按行动者／运算手／原与实际加数／回合／第几次加数隔离，巨龙两步分别询问，第二步显示中间手值。清缓存与重开令旧请求失效；预览不询问，另一手不合法、自加、偷窃、预言压制不触发。风暴余势与誓言反击在纯运算前也询问加数持有者，不推进回合或产生数字效果。AI经IllusionPolicyFor取得该持有者真实策略，比较两种结果，对已知队友选较高收益、对其他行动者选较低收益，平分保留；未做随机或权重调参。

高塔第八战改为“发动改算／保留原加数／镜像折光”三个教学样本，指定选项高亮且其它选项仍显示。README、RULES、图鉴、策略说明和章节指引已同步。原2,631,408局胜率表明确标为本次技能调整前的规则快照，未冒充新规则结果。

## 诡术／风暴领域与耳鸣（2026-10-28）

当前54子职、12职业，守序18／中立17／混乱19，共460种双将；职业／子职正式美术清单66项。两名牧师均0/−1、∞、近钝击远穿刺、混乱中立。`Core/ClericDomains.cs`统一换位预备答案、实际交换、反击与纯运算入口；`ClericDomainLogics.cs`登记角色能力。控制器先等待幻术加数选择，再询问诡诈换位与独立魅惑，随后按换入数字收集攻击／回复等选择；实际提交之前不改手值。战报保留原算式并标注换位结果。

`AttackContext.PrepareResolution`只执行一次取消、攻击钩子与转移。普通攻击遇风暴领域时，延迟伤害及整回合收尾；先在战场点4、选雷电类型，完成反击与同手强制纯运算，再恢复原攻击。反击子流程优先于排队中的父攻击，反击击杀来袭者取消原剩余攻击；剑士追加攻击也接入该队列。原有无反击对局的公共结算保持同步。无头策略走相同规则，人类通过WizardChoice暂停，所有手选项点击战场手牌。

耳鸣集中接入状态枚举、显示上限、回复入口、普通／赌博／有效偷窃合盾、入伤倍率与毒／血持续伤害；雷鸣免疫是状态能力，不写临时免疫集合。新增两条雷电反应与旧反应并存；秘法手册11反应，21负面＋3角色状态。生命净化先移除负面再治疗，圣光清除并阻止耳鸣。

AI专属决策见`GreedyChoiceProvider.ClericDomains.cs`：换位比较双方数字即时价值与自身准备收益，对已知队友不魅惑／不反击；反击避开双免疫，按抗性、易伤及现有状态选雷电。新角色使用L／H12／D43默认值，未重新训练；旧52角色胜率保持改版前快照。制作源图与完整提示词见[美术记录](Art/Production/cleric-domains-v1/README.md)，实机验收见[UI记录](Art/Integration/cleric-domains-v1/README.md)。

## 数字先显示、后选择攻击目标（2026-10-29）

控制器在ContinueSelectedOperation进入任何目标／技能选择前记录只读显示投影，GameManager.OperationDisplay基于原始手值、状态修正与已答复换位生成数字；不调用RewriteEffect、随机源或技能。GameBoardUI的HandValuePreview统一供1v1与RefreshRing读取，数字与手型同步，绘制替身保持真实手牌的左右方向；点击和判定仍绑定原手牌。实际提交前清除投影，回合开始／重选也清除，防止二次相加或残留。新增7项顺序／快照／放弃／状态／巨龙／换位回归测试。

## 格挡／抗性的攻击附加负面扣时长（2026-10-29）

按用户确认的[逐项分类](Docs/combat-defense.md)处理。AttackGuard是本次攻击附加负面时长用途的广义格挡，旧WasBlocked/BlockedHits仍服务盾挡／死灵抵挡的伤害战报，不混用。每段记录ElementHit：格挡扣1、对应抗性扣1、最低0；整次减半／转移／风暴狂怒每段生效，屏障、太极／伏击、虚假生命、羊血池按实际该段生效，多种格挡不重复扣。闪避／取消没有命中记录，对应魔法免疫不附对应元素状态；攻击附加的其他负面状态按格挡减时长。应用时按段顺序附加／刷新，保持原整次攻击伤害与抗性快照，防止中途圣光清抗性影响后段伤害；仅第一段被挡，后段正常命中可刷新回2。3回合血脉同公式；0回合不改既有状态。仅攻击附加负面状态及负面元素反应按此扣时长；圣光／剑歌／战恩等角色状态、异界疏离成对距离等专属技能效果保留原时长。攻击前的技能控制改为命中后落地，按段伤害／掉血条件筛选，异步惑控选择也保留当次攻击的防御快照。长胜率尚未重采样，旧快照不作为本规则强度证据。
