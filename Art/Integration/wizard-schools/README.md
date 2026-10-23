# 三学派与防护效果接入验收

2026-10-23，团结编辑器Play模式 **95项检查通过**。可重放脚本 [review.cs.txt](review.cs.txt)，逐项结果 [validation.txt](validation.txt)。全部1122项无头逻辑测试通过，Unity条件编译和UI编译通过；42枚职业/子职徽记及19项本批素材的尺寸、透明度、哈希与运行时拷贝检查通过。

- 30角色均能加载512px正式徽记；选将三所新学派徽记匹配，法师筛选有5个学派。
- 法师大类使用旧塑能书杖；新塑能独立徽记，五指施法手已修正。暗蚀抗性与现有暗蚀伤害均为冷翡翠绿，骨爪五指已修正。
- 死灵回合开始有可用1直接显示角色目标与「放弃攻击」，不再选发动手/目标手。放弃保留正常行动；免费攻击防守和死亡0～2对手值交换由真实测试类执行验证。
- 防护2先选角色、再显示六项抗性；已实际检查五种禁止项不在界面，并通过射线点击寒冷选项。物理整组同时给三类物理抗性。
- 实际点击屏障存储按钮，保留运算数字、累计4点且不进入普通攻击目标/盾选择。
- 秘法手册四页，第三页只有原六条元素反应；第四页为13类通用抗性及屏障，注明防护学派六项限制。
- 1v1名字旁显示13类抗性与屏障，屏障显示点数；4/8人环形布局截图确认，图标超出可见区可滚动。本批图标计数不是防护学派可赋予范围。
- `HandValueProfiles` 的30角色当前权重由引擎导出到 [AI配置表](../../../Tools/ai-strategy-profiles.tsv)。AI完整检查见 [报告](../../../Tools/ai-strategy-audit.md)。

## 截图

| 内容 | 文件 |
|---|---|
| 三学派选将 | [selection-three-schools.png](selection-three-schools.png) |
| 防护图鉴 | [wizard-codex.png](wizard-codex.png) |
| 抗性手册（可滚动） | [resistance-manual.png](resistance-manual.png) |
| 死灵目标与放弃 | [necrotic-target.png](necrotic-target.png) |
| 六项抗性选型 | [resistance-selection.png](resistance-selection.png) |
| 1v1效果栏 | [battle-protections.png](battle-protections.png) |
| 2v2效果栏 | [4p-protections.png](4p-protections.png) |
| 8人效果栏 | [8p-protections.png](8p-protections.png) |

截图由运行时Canvas渲染至1920×1080，不是概念图。脚本对选型、放弃、存储按钮使用EventSystem射线命中与点击；多人截图用于布局验收，整局行为另由逻辑回归和统计覆盖。样本不构成所有角色搭配数值平衡的保证。
