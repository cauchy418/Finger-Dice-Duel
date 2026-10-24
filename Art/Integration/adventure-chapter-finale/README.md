# 边境哨站六战至终战验收

2026-10-24，先完成五份设计文档，再实现与引擎验收。章节现在共十战，全部共用哨站背景；终战按用户补充仅开放战士的6个子职。

## 验证

- 新增10项`AdventureChapterFinaleTests`，覆盖六至九战完整控制器演算、四种状态真实效果/按钮白名单、终战6名战士池、固定骑士攻防、正常AI可击败玩家与重开；全部通过。原三至五战12项专项通过。
- 全量 **1225项通过、0失败**；Unity条件测试编译及UI编译通过。胜率报告重新生成，README的117项角色胜率与各模式摘要同步。
- 实际引擎 **175项主流程通过**，见[完整结果](validation.txt)。所有输入按钮通过EventSystem真实射线点击；六至九战未改中途手值、体力或状态，完整执行真实剧本。终战先使用真实随机骰及实际分配验证先攻，再用明确配置的致死场景检查失败/重试和胜利/存档分支；胜负仍由真实控制器与AI攻击结算。后一个胜利场景用20/18固定骰完成合法分配，以避免验收依赖随机等待。没有把这些测试场景写进正式关卡。
- 验收备份并恢复十战/章节PlayerPrefs及AI等待配置，不替玩家写通关。最后返回章内列表。旧五战的138项检查是历史版本，保留原目录。

补充统一前九战对手名为“哨站教官”，终战开场直接显示所选战士子职、分行说明骑士属性与经典流程；**41项冒险专项回归、44项实际引擎姓名与文案复验通过**，详见[补充结果](validation-names-and-intro.txt)，引擎检查合计219项。全量1225项后仅调整这些名称/文案及对应断言，随后再次通过Unity条件/UI编译。

最新剧情调整：初战对手及全部指引改为“老兵”，开场“身负旧伤的老兵拦住了去路”，与次战“老兵请来哨站教官”衔接。第二至九战仍为哨站教官。仅调整名称与叙事，41项冒险测试、UI编译及[24项引擎检查](validation-veteran.txt)通过；旧统一教官截图是前一版，当前初战见下表。

## 截图

| 画面 | 文件 |
|---|---|
| 初战与次战衔接 | [初战老兵开场](first-veteran-intro.png)／[老兵指引](first-veteran-guide.png)／[次战请来教官](second-veteran-transition.png) |
| 十战入口 | [章内列表](chapter-ten-battles.png) |
| 六战 | [8穿盾](sixth-pierce.png)／[4两段破盾击杀前](sixth-double-hit.png)／[胜利](sixth-complete.png) |
| 七战 | [流血](seventh-bleed.png)／[跛脚，实际射程1](seventh-limp.png)／[震慑](seventh-stunned.png)／[倒伏](seventh-prone.png)／[目标完成](seventh-complete.png) |
| 八战 | [追加选择](eighth-follow-up.png)／[真实4＋7击杀](eighth-complete.png) |
| 九战 | [差0](ninth-gap-zero.png)／[差5](ninth-gap-five.png)／[差13](ninth-gap-thirteen.png) |
| 终战 | [仅6名战士与原骑士徽记](finale-fighter-selection.png)／[开场](finale-intro.png)／[真实先攻](finale-before-roll.png)／[分配](finale-allocation.png)／[自由对战](finale-free-battle.png) |
| 结果与技能 | [失利](finale-defeat.png)／[招式全部可选](finale-free-maneuver.png)／[第一章通关](finale-chapter-complete.png) |

## 复验入口

`helpers.cs.txt`＋`review-a.cs.txt`运行六至九战并进入真实骰子动画；动画/AI分配完成后运行`helpers.cs.txt`＋`review-b.cs.txt`，必要时等待AI分配后再次运行B。使用本地引擎Codely的Play脚本执行接口。脚本在成功或异常后恢复存档；退出中断时应使用同一备份执行恢复再重试，不将备份纳入版本库。

`helpers.cs.txt`＋`review-names-and-intro.cs.txt`复验前九战姓名、第五战先攻姓名，以及6个战士子职的终战开场正文和排版；它恢复AI等待时长，不写通关存档。

## 素材来源

`Assets/Resources/Art/Battle/v1/Adventure/knight_original.png`逐字节复用`Art/Production/painted-icons-v2/native/Subclasses/paladin_oath_of_the_crown.png`，即原骑士的红缨头盔、狮盾和长枪徽记；不覆盖新版王冠之誓。资源GUID由引擎导入生成，未手改meta。
