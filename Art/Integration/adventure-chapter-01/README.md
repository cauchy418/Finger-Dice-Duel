# 第一章：边境哨站 — 双教学与专用背景验收

2026-10-24，团结引擎Play模式真实EventSystem射线点击，**118项检查通过**。逐项见[validation.txt](validation.txt)，复验脚本见[review.cs.txt](review.cs.txt)。测试前备份、结束恢复两战PlayerPrefs标记，不代替玩家通关；结束回到标题。

## 实际结果

- 剧情冒险→章节列表“第一章：边境哨站”→章内“边境哨站·初战／次战”；两级返回、开场取消均正常。
- 初战7步完整回归、旧存档键保留，胜利可继续次战；初战完成不会解锁次战跳过。
- 次战普通6造成7伤、教官5＋6→1；重击4在确认目标后自动造成7伤，双手4/6、技能计数1，战报出现“十点重击”；教官8＋4→2后进入自由练习，普通8终结。
- 点击白名单、手牌高亮与旁白布局正常，指南不遮挡手牌；完整章节名、开场及胜利文字均在面板内，倍率×1.5保持完整。
- 各战独立通关、重打、跳过、直接进入/取消；次战重打恢复1/6对5/8、生命20对18，第三按钮恢复“已通关·自由练习”，不串成初战。
- 初战、次战共用新绘制的边境哨站庭院背景；重打保留章节背景，普通对战恢复原桌面；无教学旁白或NPC休整权限泄漏。
- 初战/次战各8项逻辑专项（各32组长自由练习），全量1200项通过、0失败，Unity/NUnit条件编译与UI编译通过。全量报告已重算，README胜率表同步当前输出。

## 截图

| 场景 | 图片 |
|---|---|
| 剧情章节列表 | [章节](chapter-select.png) |
| 章节内战斗列表 | [初战／次战](chapter-battles.png) |
| 初战改名 | [开场](first-intro.png) |
| 初战继续次战 | [胜利](first-victory-next.png) |
| 次战开场与章节背景 | [开场](second-intro.png) |
| 普通攻击对照 | [第1步](second-01-ordinary.png) |
| 十点重击指引 | [第3步](second-03-heavy.png) |
| 确认攻击 | [确认](second-heavy-confirm.png) |
| 重击结算 | [伤害](second-heavy-resolved.png) |
| 自由练习 | [第5步](second-05-free.png) |
| 次战要点 | [胜利](second-victory.png) |
| 重打与自由练习入口 | [重打](second-replay.png) |
| 两战完成状态 | [进度](chapter-completed.png) |

[次战设计](../../../Docs/Adventure/level-02.md) · [背景原图与完整生成提示词](../../Production/border-outpost-v1/README.md)。素材使用内置imagegen生成；截图是引擎实际渲染，不是概念图。
