# 战士人物立绘

战士七个子职各有男性、女性全身立绘，共14张。沿用工程暗色奇幻、黑铁与旧金、写实绘画质感，主体无文字或徽记外框。

| 子职 | 男性 | 女性 | 子职辨识与男女设计差异 |
|---|---|---|---|
| 战士·勇士 | [PNG](fighter_champion_male_fullbody.png) | [PNG](fighter_champion_female_fullbody.png) | 金狮与月桂、完整盾牌。男性为深色重甲战袍、长盾与低持直剑；女性为浅色战衣、圆盾与竖持直剑。 |
| 战士·破阵者 | [PNG](fighter_breaker_male_fullbody.png) | [PNG](fighter_breaker_female_fullbody.png) | 破损盾与宽刃直剑。男性为厚重方肩板甲、削损圆盾与低位宽刃剑；女性为利落黑甲、裂损长盾与斜持直剑。 |
| 战士·魔射手 | [PNG](fighter_arcane_archer_male_fullbody.png) | [PNG](fighter_arcane_archer_female_fullbody.png) | 蓝紫奥术箭与旧金弓。男性为银发精灵、长弓、短披风与背部箭袋；女性为深肤编发、紧凑反曲弓、长衣与腰侧箭袋。 |
| 战士·剑士 | [PNG](fighter_swordsman_male_fullbody.png) | [PNG](fighter_swordsman_female_fullbody.png) | 两把直剑与轻便战甲。男性为长发、青绿腰巾、侧身低位防守；女性为短赤发、赭金腰巾、双剑展开的进攻姿态。 |
| 战士·战术家 | [PNG](fighter_tactician_male_fullbody.png) | [PNG](fighter_tactician_female_fullbody.png) | 地图、罗盘与指挥官装备。男性为年长军官、蓝灰长衣、双手阅图；女性为深绿战衣、夹持地图册、抬手指挥。 |
| 战士·战斗大师 | [PNG](fighter_battle_master_male_fullbody.png) | [PNG](fighter_battle_master_female_fullbody.png) | 熟练使用多类兵器的老练战士。男性为魁梧重甲、双编胡须、斧锤枪一体长柄武器；女性为紧凑铆甲、白色肩布、直剑与战锤。 |
| 战士·奥法骑士 | [PNG](fighter_eldritch_knight_male_fullbody.png) | [PNG](fighter_eldritch_knight_female_fullbody.png) | 元素直剑与裂盾。男性为蓝披风、竖持雷电剑、棱角长盾；女性为银发盘辫、铜红战衣、低持火焰剑与圆盾。 |

## 文件与验收

全部为1024×1536 RGBA透明PNG，使用内置image_gen逐张生成；逐张检查全身、兵器轮廓、姿态和握持，并核对透明通道、文件数量与SHA256。原有勇士男性、破阵者女性文件及文件名保留，其余12张独立绘制；同子职男女版本具有装备、体型、年龄或姿态差异。

原始PNG逐字节复制到本目录，保留生成透明通道，未作程序切图、重绘或缩放。本目录供人物立绘审阅与后续界面接入使用，目前游戏仍使用原有徽记，本次未更改运行时资源或UI。

本次只增加美术素材与说明，完成图像和链接检查，不运行游戏逻辑回归或胜率采样。完整提示词、设计说明、参考素材、生成来源与SHA256见[生成清单](manifest.json)。
