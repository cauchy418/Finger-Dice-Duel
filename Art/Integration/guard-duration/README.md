# 攻击附加负面时长验收（2026-10-29）

范围与精确格挡分类见 [combat-defense](../../../Docs/combat-defense.md)。不仅元素状态，技能流血、战斗招式、散打控制、妖魔契约、惑控和征服恐慌同样逐段扣时长。圣光／剑歌／战恩等角色状态及异界疏离等独立技能效果保持原时长。攻击前选定的控制改为命中后施加，闪避／取消不附加。

- 全量功能回归：1,503通过，0失败；本次新增34项测试（含角色状态与异界疏离不减时长的回归），7项长胜率统计跳过。
- Unity条件编译路径（FDD_UNITY_TESTS）通过；只校验编译，未使用NUnit引擎运行。
- 表现层187脚本编译通过。
- 编辑器Play模式实际战斗UI验收7项通过，脚本与日志在all-attack-status目录；验收结束返回标题界面。
- 本次未重新采样胜率；dual-alignment-probes为功能回归生成的双将探针，不是胜率报告。散打招式不再先于同次伤害挂倒伏，探针中的该次伤害随之更新。

截图：

| 文件 | 验收结果 |
|---|---|
| [shield-one-turn](all-attack-status/shield-one-turn.png) | 火焰攻击被盾挡，燃烧和技能流血均显示1回合 |
| [shield-resistance-zero](all-attack-status/shield-resistance-zero.png) | 盾挡＋火焰抗性，两种附加均0回合，不显示新状态 |
| [multi-refresh-two](all-attack-status/multi-refresh-two.png) | 两段攻击，第一段盾挡1回合，第二段正常命中刷新为2回合 |

执行无头回归：Tools/HeadlessTests/run-tests.ps1 -SkipStatistics。编辑器重放all-attack-status/validate-ui.csx须使用Codely的execute_csharp_script（Play模式）；脚本创建测试对局、断言状态与UI文本并生成截图。
