# 现行技能改版动画接入验收

35个技能变体与4个持续状态循环，各保存两张1280×720生产Canvas关键帧，共78张无损WebP。技能帧取0.28／0.62；状态循环使用0.5／2.2的受控相位，便于比较运动形态。全部WebP与原PNG像素一致，见[哈希与尺寸](manifest.json)。

原生团结引擎已运行[capture-native.csx](capture-native.csx)和[capture-status.csx](capture-status.csx)，[技能原生检查](native-review.txt)、[循环原生检查](native-status-review.txt)验证非空资源、可见效果与播放清理。这些受控事件用于画面验收；真实规则另由 `SecondaryAnimationTests` 验证进位、状态成功、消费、兽魂与灼伤后的实际回复，以及守望转换、变化莫测真实完成结果、诡术打击现行命名与狼形态吸血。

本轮全功能回归1724项通过、零失败，独立统计8项跳过；UI真实引擎程序集编译通过。全角色路线及共享循环范围见[现行清单](../../../Docs/skill-and-status-animation-list.md)。原图输出到忽略目录 `Temp/AnimationReworkFrames`，可重跑截图脚本复核。

首批画面验收源码 `87d54e5`；治疗补验源码 `415d8d8`（狼形态重新截图，新增圣杯、妖魔、混元体、古贤四组，共10张当前版本关键帧，其余68张沿用首批验收）。额外执行[实际注册表审计脚本](audit-literal-events.csx)，运行时调用 `SkillVisualProfiles.Key` 验证139个字面量事件名：108个有效注册入口、31个已有其他真实事件／面板快照覆盖的提示，零未分类。完整名称、资源键、替代呈现及来源文件见[审计结果](literal-event-audit.json)。

本轮58名角色逐一核对，17名补齐／更新、41名核对沿用，无重复角色。

治疗补验运行 [capture-healing.csx](capture-healing.csx)，通过 [74项原生检查](native-healing-review.txt)：五个场景均由真实技能结算产生事件，验证实际受疗者、毛回复量、独立灼伤量、治疗先于灼伤及连续快照，再使用生产播放器拍摄0.28／0.62关键帧，验证非空资源、可见效果和清理。狼形态回复2／灼伤1（7→6）；圣杯回复9／灼伤5（14→9）；妖魔回复3／灼伤2（8→6，攻击者本人受疗）；混元体回复4／灼伤2（6→4）；古贤回复2／灼伤1（8→7）。数值为受疗后到灼伤后的体力；画面是受控相位验收，不代表录制了一整场对战。

本轮补齐：知识获得／消费／力场／困惑，净化圣焰及免疫穿透、圣焰治疗、灼伤，野兽兽魂积累／消费成长，狂战士5／9转攻击、巨人双段与回复、元素战刃五元素准备／消费，战争／风暴／诡术治疗赐福和复仇之魂纯运算；守望誓约原类型转力场壳，变化莫测额外运算实际旧值与结果，魔术攻击旧名改为现行诡术打击，狼形态实际吸血回复。既有可用动画继续沿用，本表没有将全部角色都计作新定稿。

| 变体 | 起势／相位一 | 高峰／相位二 |
|---|---|---|
| beast-consume | [截图](beast-consume/windup.webp) | [截图](beast-consume/peak.webp) |
| beast-gain | [截图](beast-gain/windup.webp) | [截图](beast-gain/peak.webp) |
| berserker-five | [截图](berserker-five/windup.webp) | [截图](berserker-five/peak.webp) |
| berserker-heal | [截图](berserker-heal/windup.webp) | [截图](berserker-heal/peak.webp) |
| berserker-nine | [截图](berserker-nine/windup.webp) | [截图](berserker-nine/peak.webp) |
| confusion-apply | [截图](confusion-apply/windup.webp) | [截图](confusion-apply/peak.webp) |
| confusion-carry | [截图](confusion-carry/windup.webp) | [截图](confusion-carry/peak.webp) |
| confusion-loop | [截图](confusion-loop/windup.webp) | [截图](confusion-loop/peak.webp) |
| giant-blade-acid | [截图](giant-blade-acid/windup.webp) | [截图](giant-blade-acid/peak.webp) |
| giant-blade-cold | [截图](giant-blade-cold/windup.webp) | [截图](giant-blade-cold/peak.webp) |
| giant-blade-consume | [截图](giant-blade-consume/windup.webp) | [截图](giant-blade-consume/peak.webp) |
| giant-blade-fire | [截图](giant-blade-fire/windup.webp) | [截图](giant-blade-fire/peak.webp) |
| giant-blade-lightning | [截图](giant-blade-lightning/windup.webp) | [截图](giant-blade-lightning/peak.webp) |
| giant-blade-thunder | [截图](giant-blade-thunder/windup.webp) | [截图](giant-blade-thunder/peak.webp) |
| giant-recovery | [截图](giant-recovery/windup.webp) | [截图](giant-recovery/peak.webp) |
| giant-strike | [截图](giant-strike/windup.webp) | [截图](giant-strike/peak.webp) |
| holy-flame | [截图](holy-flame/windup.webp) | [截图](holy-flame/peak.webp) |
| holy-heal | [截图](holy-heal/windup.webp) | [截图](holy-heal/peak.webp) |
| holy-loop | [截图](holy-loop/windup.webp) | [截图](holy-loop/peak.webp) |
| holy-pierce | [截图](holy-pierce/windup.webp) | [截图](holy-pierce/peak.webp) |
| knowledge-carry | [截图](knowledge-carry/windup.webp) | [截图](knowledge-carry/peak.webp) |
| knowledge-consume | [截图](knowledge-consume/windup.webp) | [截图](knowledge-consume/peak.webp) |
| knowledge-force | [截图](knowledge-force/windup.webp) | [截图](knowledge-force/peak.webp) |
| knowledge-heal | [截图](knowledge-heal/windup.webp) | [截图](knowledge-heal/peak.webp) |
| knowledge-loop | [截图](knowledge-loop/windup.webp) | [截图](knowledge-loop/peak.webp) |
| scorch | [截图](scorch/windup.webp) | [截图](scorch/peak.webp) |
| scorch-loop | [截图](scorch-loop/windup.webp) | [截图](scorch-loop/peak.webp) |
| storm-heal | [截图](storm-heal/windup.webp) | [截图](storm-heal/peak.webp) |
| trickery-heal | [截图](trickery-heal/windup.webp) | [截图](trickery-heal/peak.webp) |
| vengeance-operation | [截图](vengeance-operation/windup.webp) | [截图](vengeance-operation/peak.webp) |
| war-heal | [截图](war-heal/windup.webp) | [截图](war-heal/peak.webp) |
| warden-force | [截图](warden-force/windup.webp) | [截图](warden-force/peak.webp) |
| transmutation-operation | [截图](transmutation-operation/windup.webp) | [截图](transmutation-operation/peak.webp) |
| magician-trickery | [截图](magician-trickery/windup.webp) | [截图](magician-trickery/peak.webp) |
| wolf-healing | [截图](wolf-healing/windup.webp) | [截图](wolf-healing/peak.webp) |
| chalice-healing | [截图](chalice-healing/windup.webp) | [截图](chalice-healing/peak.webp) |
| fiend-healing | [截图](fiend-healing/windup.webp) | [截图](fiend-healing/peak.webp) |
| harmony-healing | [截图](harmony-healing/windup.webp) | [截图](harmony-healing/peak.webp) |
| ancient-healing | [截图](ancient-healing/windup.webp) | [截图](ancient-healing/peak.webp) |
