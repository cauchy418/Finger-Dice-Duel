# 四个新子职选择修复（2026-10-24）

修复人类玩家被默认结算代答的问题，补齐对应专属AI策略。全量1170项逻辑测试、UI编译及Unity条件编译通过；17项专项在无头和团结引擎内均通过。实际Canvas检查104项通过，见 [validation.txt](validation.txt)，重放见 [review.cs.txt](review.cs.txt)。

- 龙族血脉首次自己回合弹出五种元素，确认前不获得默认抗性；只获得所选永久抗性，本局锁定、重开重选。
- 猎人合出7后暂停选目标，可放弃；人名不带手名，4人局能标记加数主人以外的目标。
- 散打宗按气点给出共鸣/倒伏/震慑/保留选项；四象宗提供五种元素和保留选项。
- 答案在实际结算消耗一次，双将部件独立；无足够气时不提问。元素攻击补齐状态施加接口。
- Greedy策略考虑抗性、元素反应、资源、控制收益与标记收益；人类sink与AI策略对象分离注入。
- 所有命名选项的绘画图标非空、按钮文字完整且位于面板内；通过真实射线点击选非默认项并验证结果，含4/8人模式。

| 内容 | 实机截图 |
|---|---|
| 龙族血脉首次选元素 | [draconic-first-turn.png](draconic-first-turn.png) |
| 散打选择控制 | [open-hand-control.png](open-hand-control.png) |
| 四象选择元素 | [four-elements.png](four-elements.png) |
| 猎人选择标记目标 | [hunter-mark-target.png](hunter-mark-target.png) |
| 4人猎人目标 | [4p-hunter-targets.png](4p-hunter-targets.png) |
| 8人四象选择 | [8p-four-elements.png](8p-four-elements.png) |

本次接入原有绘画图标，未新增图片生成。选择为真实运行界面，AI为启发式，不代表平衡性或最优策略结论。
