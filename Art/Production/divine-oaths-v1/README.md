# 战恩、剑歌与圣武士誓言美术

2026-10-27。使用内置 **image_gen** 生成五张独立PNG，延续暗色石底、旧金边框和写实金属笔触。状态与标记用方框，子职徽记用八向装饰圆框；剑歌使用单把直剑，征服用铁冠与裂盾，复仇用直剑、暗羽和断链，仇敌誓言以红色靶环区分。

| 资源键 | 用途 | 导出 |
|---|---|---|
| `Status/war_blessing` | 战恩角色状态 | [war_blessing.png](selected/Status/war_blessing.png) |
| `Effects/bladesong` | 剑歌角色状态 | [bladesong.png](selected/Effects/bladesong.png) |
| `Subclasses/paladin_oath_of_conquest` | 征服之誓子职徽记 | [paladin_oath_of_conquest.png](selected/Subclasses/paladin_oath_of_conquest.png) |
| `Subclasses/paladin_oath_of_vengeance` | 复仇之誓子职徽记 | [paladin_oath_of_vengeance.png](selected/Subclasses/paladin_oath_of_vengeance.png) |
| `Effects/vow_of_enmity` | 仇敌誓言目标标记 | [vow_of_enmity.png](selected/Effects/vow_of_enmity.png) |

原图位于`sources/`；按可见Alpha>8的边界裁切后等比居中至512×512，徽记主体480px，状态主体448px，保留透明通道。此程序裁边／缩放沿用用户已授权方式，不修改绘画内容。正式副本已复制到`Assets/Resources/Art/Battle/v1/`，不依赖生成缓存路径。

完整逐图提示词及透明参数见[prompts.json](prompts.json)，生成源路径、资源键、SHA256见[selected-manifest.json](selected-manifest.json)。

导入：`node Tools/ArtPipeline/import-divine-oaths.cjs`，已挂入总导入链；Unity自动生成meta，不手工改写。征服／复仇同时登记到职业总清单，64枚职业／子职徽记通过唯一性、尺寸、透明及运行时一致性校验。接入效果与验证见[引擎验收](../../Integration/divine-oaths-v1/README.md)。
