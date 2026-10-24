# 边境哨站三战至五战、指引翻页与老兵先攻说明验收

2026-10-24，团结引擎Play模式 **138项检查通过**；[逐项结果](validation.txt)。全量 **1215项逻辑测试通过，0失败**，Unity/NUnit条件编译和UI编译通过。

## 覆盖范围

- 初战/次战原16项专项在引擎中回归；新增12项后续关卡专项和3项指引历史专项同步执行。
- 从五战滚动列表实际点击三战；完整连锁、修复、主动碎盾7伤/流血、自由阶段并取胜，继续四战。
- 被动连锁由防守方玩家选择，不因加数手不是盾而漏问；被动破盾不附流血。双盾修复实际成型。
- 上一页、下一页、回到当前均通过EventSystem射线点击；翻旧指引后刷新保留旧页，战斗回合数不回退。
- 四战固定两块石头，只占座，不成为参战角色；两手显示−、无按钮，距离真实为2。石头移至上方两侧，包围盒与放弃按钮不重叠，实际放弃/碎盾按钮点击成功。
- 4超距时不可点击教官，有放弃出口、手值保留4；合盾后8主动碎盾换射程，单次确认命中9伤并附流血，显示训练完成，教官仍存活。
- 五战真实随机1d20动画与合法分配；老兵投骰前解释分配/行动区别，落定后引用真实骰点和先攻者，分配时随当前决策人更新。姓名、先攻短提示与顺序行分离，老兵面板不遮骰子与选项。
- 分配完成才BeginTurn，先行动者与MatchStartController一致；第五战无安全NPC休整权限。
- 真实AI击杀玩家，显示训练失利，不记通关；重新投骰建立新对局、清掉旧骰点，投骰页可返回章节。另用受控残局检验胜利与独立通关标记；后者布置局面，不宣称为完整随机实战胜率。
- 普通对战不出现石头、指引或骰页教学旁白；验收开始备份5个PlayerPrefs标记，结束恢复原值、AI等待时间和日志设置，回到主菜单，不代替玩家通关。

## 截图

| 内容 | 实际引擎画面 |
|---|---|
| 章内滚动选择 | [五场训练](five-battles.png) |
| 翻阅旧指引 | [指引历史](guide-history.png) |
| 三战开场 | [介绍](third-intro.png) |
| 被动连锁破盾 | [选择敌盾](third-passive-chain.png) |
| 合盾修复 | [两面完整盾](third-repaired.png) |
| 主动碎盾流血 | [技能结算](third-bleed.png) |
| 四战超距与石头位置 | [放弃攻击无遮挡](fourth-out-of-range.png) |
| 碎盾换射程 | [发动按钮](fourth-range-choice.png) |
| 命中完成训练 | [四战完成](fourth-complete.png) |
| 五战开场 | [先攻介绍](fifth-intro.png) |
| 投骰前老兵解释 | [投骰页](fifth-before-roll.png) |
| 投骰后动态解释 | [分配页](fifth-allocation.png) |
| 自由实战 | [战斗](fifth-free-battle.png) |
| 失败重试 | [失利](fifth-defeat.png) |
| 通关要点 | [胜利](fifth-victory.png) |

## 复验

[review-a.cs.txt](review-a.cs.txt)先执行三战/四战/翻页并进入五战真实投骰，返回后让动画与AI协程运行；再执行[review-b.cs.txt](review-b.cs.txt)，若返回WAIT，待AI分配后再执行B，直至PASS。两段包含[公共助手](helpers.cs.txt)，已展开保存，可直接交给引擎execute_csharp_script（Play模式）。临时偏好备份保存在忽略目录Tools/UiCompileCheck/bin，成功或异常时恢复。

设计：[三战](../../../Docs/Adventure/level-03.md)、[四战](../../../Docs/Adventure/level-04.md)、[五战](../../../Docs/Adventure/level-05.md)。美术：[石头原图与内置imagegen完整提示词](../../Production/border-outpost-stone-v1/README.md)。
