# 德鲁伊形态验收（2026-10-24）

2枚结社徽记、7张形态图接入选将、战斗及图鉴。全量1184项逻辑测试通过；UI与Unity条件编译通过。11项专项在无头和实际团结引擎内通过；实际Canvas共83项检查通过，逐项见 [validation.txt](validation.txt)，重放见 [review.cs.txt](review.cs.txt)。

- 两结社的形态选择通过实际射线点击，确认前不代选；月亮4项、星辰3项，均可保留基础。
- 1v1右侧显示全部对应形态，当前形态高亮；图像非空，布局在玩家区域内。4人、8人各面板也显示。
- 巨龙可再次选同一只加数手，最终才结算效果；真实界面2+3+3得到8，只推进一次回合。
- 双将两列形态图与文字不重叠，分别高亮当前形态，基础射程读取聚合最大值；修正此前仅读取主将射程的旧问题。
- 双将形态叠加/分别解除、圣杯目标与放弃、狼吸血、蛇中毒、鹰6无攻击、凑10与AI关键分支由专项覆盖。
- 图鉴详情展示独立的4/3张绘画与名称、规则悬停；检查全部Sprite非空，无缺失资源日志。
- 51枚职业/子职徽记通过尺寸、透明度、哈希与独立性校验，7张形态图由专用导入校验。

| 内容 | 截图 |
|---|---|
| 月亮结社选形态 | [moon-choose.png](moon-choose.png) |
| 星辰结社选形态 | [stars-choose.png](stars-choose.png) |
| 狼与右侧提示 | [moon-wolf-right.png](moon-wolf-right.png) |
| 巨龙与右侧提示 | [stars-dragon-right.png](stars-dragon-right.png) |
| 双将两套形态提示 | [dual-druid-right.png](dual-druid-right.png) |
| 巨龙第二加数 | [dragon-second-addend.png](dragon-second-addend.png) |
| 多人提示 | [4p-druid-right.png](4p-druid-right.png)、[8p-druid-right.png](8p-druid-right.png) |
| 两结社图鉴 | [月亮](codex-druid_circle_of_the_moon.png)、[星辰](codex-druid_circle_of_the_stars.png) |

图为实际运行时Canvas；策略为启发式，不代表角色平衡结论。原图、正式导出及内置imagegen精确提示词见 [制作记录](../../Production/druid-forms-v1/README.md)。
