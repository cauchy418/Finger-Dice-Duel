# 2026-11-05 角色技能动画更新

**63个变体，126张1280×720关键帧。** 每个变体保存起势 0.28 和高峰 0.62。截图由团结引擎 Canvas 与生产播放器实拍，无损 WebP 已逐像素与原 PNG 核对。

形态进入和击破用实际战局事件生成。其余截图使用受控事件审阅落点、图案和文字。六项实战规则事件另经引擎播放队列验证，包括跳过后恢复输入与最终面板同步。

代码版本 `ca1e41e`。功能回归 1,627 项通过，UI 与 Unity／NUnit 条件编译通过。[原生截图验证](native-review.txt) · [规则事件播放验证](runtime-validation.txt) · [截图哈希](manifest.json)

使用已载入 SampleScene 的团结引擎播放模式，通过 Codely 依次执行[截图脚本](capture-native.csx)和[规则事件验证脚本](validate-runtime.csx)。截图脚本将原 PNG 写入 Temp/AnimationNovemberFrames。

| 片段 | 技能与变体 | 起势 | 高峰 |
|---|---|---|---|
| wild-bear-enter | 荒野形态 · 熊 · 进入 | [截图](wild-bear-enter/windup.webp) | [截图](wild-bear-enter/peak.webp) |
| wild-bear-exit | 解除荒野 · 熊 · 主动解除 | [截图](wild-bear-exit/windup.webp) | [截图](wild-bear-exit/peak.webp) |
| wild-bear-break | 解除荒野 · 熊 · 击破 | [截图](wild-bear-break/windup.webp) | [截图](wild-bear-break/peak.webp) |
| wild-wolf-enter | 荒野形态 · 狼 · 进入 | [截图](wild-wolf-enter/windup.webp) | [截图](wild-wolf-enter/peak.webp) |
| wild-wolf-exit | 解除荒野 · 狼 · 主动解除 | [截图](wild-wolf-exit/windup.webp) | [截图](wild-wolf-exit/peak.webp) |
| wild-wolf-break | 解除荒野 · 狼 · 击破 | [截图](wild-wolf-break/windup.webp) | [截图](wild-wolf-break/peak.webp) |
| wild-eagle-enter | 荒野形态 · 鹰 · 进入 | [截图](wild-eagle-enter/windup.webp) | [截图](wild-eagle-enter/peak.webp) |
| wild-eagle-exit | 解除荒野 · 鹰 · 主动解除 | [截图](wild-eagle-exit/windup.webp) | [截图](wild-eagle-exit/peak.webp) |
| wild-eagle-break | 解除荒野 · 鹰 · 击破 | [截图](wild-eagle-break/windup.webp) | [截图](wild-eagle-break/peak.webp) |
| wild-snake-enter | 荒野形态 · 蛇 · 进入 | [截图](wild-snake-enter/windup.webp) | [截图](wild-snake-enter/peak.webp) |
| wild-snake-exit | 解除荒野 · 蛇 · 主动解除 | [截图](wild-snake-exit/windup.webp) | [截图](wild-snake-exit/peak.webp) |
| wild-snake-break | 解除荒野 · 蛇 · 击破 | [截图](wild-snake-break/windup.webp) | [截图](wild-snake-break/peak.webp) |
| wild-owlbear-enter | 荒野形态 · 枭熊 · 进入 | [截图](wild-owlbear-enter/windup.webp) | [截图](wild-owlbear-enter/peak.webp) |
| wild-owlbear-exit | 解除荒野 · 枭熊 · 主动解除 | [截图](wild-owlbear-exit/windup.webp) | [截图](wild-owlbear-exit/peak.webp) |
| wild-owlbear-break | 解除荒野 · 枭熊 · 击破 | [截图](wild-owlbear-break/windup.webp) | [截图](wild-owlbear-break/peak.webp) |
| wild-panther-enter | 荒野形态 · 黑豹 · 进入 | [截图](wild-panther-enter/windup.webp) | [截图](wild-panther-enter/peak.webp) |
| wild-panther-exit | 解除荒野 · 黑豹 · 主动解除 | [截图](wild-panther-exit/windup.webp) | [截图](wild-panther-exit/peak.webp) |
| wild-panther-break | 解除荒野 · 黑豹 · 击破 | [截图](wild-panther-break/windup.webp) | [截图](wild-panther-break/peak.webp) |
| stars-archer-enter | 荒野形态 · 射手座 · 进入 | [截图](stars-archer-enter/windup.webp) | [截图](stars-archer-enter/peak.webp) |
| stars-archer-exit | 解除荒野 · 射手座 · 主动解除 | [截图](stars-archer-exit/windup.webp) | [截图](stars-archer-exit/peak.webp) |
| stars-archer-break | 解除荒野 · 射手座 · 击破 | [截图](stars-archer-break/windup.webp) | [截图](stars-archer-break/peak.webp) |
| stars-chalice-enter | 荒野形态 · 圣杯座 · 进入 | [截图](stars-chalice-enter/windup.webp) | [截图](stars-chalice-enter/peak.webp) |
| stars-chalice-exit | 解除荒野 · 圣杯座 · 主动解除 | [截图](stars-chalice-exit/windup.webp) | [截图](stars-chalice-exit/peak.webp) |
| stars-chalice-break | 解除荒野 · 圣杯座 · 击破 | [截图](stars-chalice-break/windup.webp) | [截图](stars-chalice-break/peak.webp) |
| stars-dragon-enter | 荒野形态 · 巨龙座 · 进入 | [截图](stars-dragon-enter/windup.webp) | [截图](stars-dragon-enter/peak.webp) |
| stars-dragon-exit | 解除荒野 · 巨龙座 · 主动解除 | [截图](stars-dragon-exit/windup.webp) | [截图](stars-dragon-exit/peak.webp) |
| stars-dragon-break | 解除荒野 · 巨龙座 · 击破 | [截图](stars-dragon-break/windup.webp) | [截图](stars-dragon-break/peak.webp) |
| stars-dragon-operation | 巨龙座 · 巨龙座 · 纯运算 | [截图](stars-dragon-operation/windup.webp) | [截图](stars-dragon-operation/peak.webp) |
| stars-chalice-heal | 圣杯座 · 圣杯座 · 额外治疗 | [截图](stars-chalice-heal/windup.webp) | [截图](stars-chalice-heal/peak.webp) |
| spore-apply | 孢子环绕 · 施加 | [截图](spore-apply/windup.webp) | [截图](spore-apply/peak.webp) |
| spore-gain | 一体共生 | [截图](spore-gain/windup.webp) | [截图](spore-gain/peak.webp) |
| spore-absorb | 孢子生命 · 吸收 | [截图](spore-absorb/windup.webp) | [截图](spore-absorb/peak.webp) |
| spore-break | 孢子生命 · 击破 | [截图](spore-break/windup.webp) | [截图](spore-break/peak.webp) |
| spore-restore | 真菌感染 | [截图](spore-restore/windup.webp) | [截图](spore-restore/peak.webp) |
| overflow | 余伤 | [截图](overflow/windup.webp) | [截图](overflow/peak.webp) |
| hunter-consume | 巨像屠夫 · 消耗印记 | [截图](hunter-consume/windup.webp) | [截图](hunter-consume/peak.webp) |
| hunter-guard | 巨像屠夫 · 格挡附伤 | [截图](hunter-guard/windup.webp) | [截图](hunter-guard/peak.webp) |
| gambler-reroll | 恶之强运 · 付血重掷 | [截图](gambler-reroll/windup.webp) | [截图](gambler-reroll/peak.webp) |
| gambler-fatal | 恶之强运 · 扣血阵亡 | [截图](gambler-fatal/windup.webp) | [截图](gambler-fatal/peak.webp) |
| shadow-darkness | 黑暗术 | [截图](shadow-darkness/windup.webp) | [截图](shadow-darkness/peak.webp) |
| shadow-grave | 坟冢之力 | [截图](shadow-grave/windup.webp) | [截图](shadow-grave/peak.webp) |
| shadow-eyes | 幽暗之瞳 · 暗蚀 | [截图](shadow-eyes/windup.webp) | [截图](shadow-eyes/peak.webp) |
| blessed-healing | 神佑医者 · 溢出祝福 | [截图](blessed-healing/windup.webp) | [截图](blessed-healing/peak.webp) |
| dragon-extend-fire | 龙族体魄 · 延长状态 · 火焰 | [截图](dragon-extend-fire/windup.webp) | [截图](dragon-extend-fire/peak.webp) |
| dragon-extend-cold | 龙族体魄 · 延长状态 · 寒冷 | [截图](dragon-extend-cold/windup.webp) | [截图](dragon-extend-cold/peak.webp) |
| dragon-extend-lightning | 龙族体魄 · 延长状态 · 闪电 | [截图](dragon-extend-lightning/windup.webp) | [截图](dragon-extend-lightning/peak.webp) |
| dragon-extend-poison | 龙族体魄 · 延长状态 · 毒素 | [截图](dragon-extend-poison/windup.webp) | [截图](dragon-extend-poison/peak.webp) |
| dragon-extend-acid | 龙族体魄 · 延长状态 · 强酸 | [截图](dragon-extend-acid/windup.webp) | [截图](dragon-extend-acid/peak.webp) |
| storm-pierce-lightning | 暴风狂怒 · 穿透免疫 · 闪电 | [截图](storm-pierce-lightning/windup.webp) | [截图](storm-pierce-lightning/peak.webp) |
| storm-pierce-thunder | 暴风狂怒 · 穿透免疫 · 雷鸣 | [截图](storm-pierce-thunder/windup.webp) | [截图](storm-pierce-thunder/peak.webp) |
| gloom-enter | 伏击 · 暗置 | [截图](gloom-enter/windup.webp) | [截图](gloom-enter/peak.webp) |
| gloom-reveal | 阴影闪避 · 明手恢复 | [截图](gloom-reveal/windup.webp) | [截图](gloom-reveal/peak.webp) |
| land-hand | 大地行者 · 土印 | [截图](land-hand/windup.webp) | [截图](land-hand/peak.webp) |
| monk-ki-fire | 四象门徒 · 火焰 | [截图](monk-ki-fire/windup.webp) | [截图](monk-ki-fire/peak.webp) |
| monk-ki-cold | 四象门徒 · 寒冷 | [截图](monk-ki-cold/windup.webp) | [截图](monk-ki-cold/peak.webp) |
| monk-ki-lightning | 四象门徒 · 闪电 | [截图](monk-ki-lightning/windup.webp) | [截图](monk-ki-lightning/peak.webp) |
| monk-ki-thunder | 四象门徒 · 雷鸣 | [截图](monk-ki-thunder/windup.webp) | [截图](monk-ki-thunder/peak.webp) |
| monk-ki-acid | 四象门徒 · 强酸 | [截图](monk-ki-acid/windup.webp) | [截图](monk-ki-acid/peak.webp) |
| illusion-guard | 幻影实体 | [截图](illusion-guard/windup.webp) | [截图](illusion-guard/peak.webp) |
| sheep-enter | 变形术 · 进入 | [截图](sheep-enter/windup.webp) | [截图](sheep-enter/peak.webp) |
| sheep-expire | 变形术 · 到期解除 | [截图](sheep-expire/windup.webp) | [截图](sheep-expire/peak.webp) |
| sheep-break | 变形术 · 击破 | [截图](sheep-break/windup.webp) | [截图](sheep-break/peak.webp) |
| specter-dismiss | 咒缚幽灵 · 消散 | [截图](specter-dismiss/windup.webp) | [截图](specter-dismiss/peak.webp) |
