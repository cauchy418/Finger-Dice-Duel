# 战斗动画基础素材 v1

2026-10-31。已交付21张透明动画素材：7张武器／投射物、10张魔法特效、2张通用轨迹／命中特效、2张碎盾裁片。另见[完整盾、碎盾、太极](../hand-states-v2/README.md)。本批已接入游戏基础动画播放，后续专属技能与常驻状态见[清单](../../../Docs/skill-and-status-animation-list.md)。

- [完整动作、类型和职业映射设计](../../../Docs/combat-animation-design.md)
- [武器预览](weapons-preview.png)／[元素与轨迹预览](effects-preview.png)
- [武器清单及完整提示词](weapons-manifest.json)／[特效清单及完整提示词](effects-manifest.json)
- [动画路由与参数](animation-profiles.json)

原图由内置 image_gen 逐张生成，保存在`source/`；保留透明通道，按先前授权用程序裁切透明外边、居中与缩放。游戏文件位于`Assets/Resources/Art/Battle/v1/Animation/`，512×512，主体最长边约82%，保留现有原图材质。握持点、飞行方向及锚点见设计文档。半透明电弧不强行转成不透明。常规武器使用白色Image tint；元素可在白色基础上调亮度，不统一再染红。

剑、锤、棍、拳、刺剑与箭朝右；弓竖直。特效保留原始美术方向，路由记录旋转修正。使用`BattleArt.Get("Animation/文件名")`可加载单张Sprite。19张绘制原图可通过[导出工具](../../../Tools/Art/export_combat_animation.py)重新生成；两张碎盾裁片由同一盾图按原裂缝方向分离，不能单独当作完整盾使用。

验证：22张绘制原图（含3状态）及24张运行时贴图（含2裁片）均检查RGBA、透明边缘、尺寸、SHA256与Sprite导入配置；代码通过1542项功能回归、真实引擎程序集UI编译。程序验证覆盖代挡发动、放弃、无完整盾拒绝、碎盾冲击与第三章各教学样本。基础事件队列已实现，当前验收见[动画接入](../../Integration/combat-animation/README.md)。
