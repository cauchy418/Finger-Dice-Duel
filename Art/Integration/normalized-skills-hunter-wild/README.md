# 规范技能与猎人／狂野魔法集成验收

2026-10-29。实现口径见[规范技能正文](../../../Docs/character-skill-copy-draft.md)、RULES与CODELY。

- 50个非武僧子职已接入规范正文；技能只显示中文，标签仅“主动技能／被动技能”，职业与子职身份中英文行保留。
- 猎人灭族者提供角色目标按钮和结束入口；追加不增加回合，正常消费印记并进入独立防守。
- 狂野魔法各段独立类型、对应状态与防御规则；截图用确定性火焰→闪电的两段测试上下文展示状态，合法双将巨人＋狂野的真实运算另有功能测试。
- 全量功能1525通过，0失败；新增专项22项，7项长胜率统计跳过。189脚本表现层编译、Unity NUnit条件编译通过（后者仅编译）。本轮未重采样胜率。
- Play实机32项通过，详见validation.txt与validate-ui.csx。含8角色图鉴实际排版检查、四武僧原格式保留、实际2v2按钮射线点击及混合状态图标。结束后回到标题界面。

截图：codex-fighter_arcane_archer.png、codex-ranger_hunter.png、codex-wizard_school_of_abjuration.png、hunter-extra-target-2v2.png、hunter-extra-result-2v2.png、wild-mixed-statuses.png。

文案同步检查：python Tools/generate-skill-descriptions.py；更新正文后使用--write。功能回归：Tools/HeadlessTests/run-tests.ps1 -SkipStatistics。
