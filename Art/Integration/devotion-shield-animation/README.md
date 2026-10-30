# 奉献之誓、标题入口与动画素材验收

2026-10-31。

- 守护之誓在收集选择、AI策略与实际结算三处检查自己持有完整盾；仅有数字5／碎盾／太极均不满足。确认代挡后追加本人的卫护斩目标选择，选择范围重新按保护者计算；拒绝代挡不追加冲击，碎盾钩子才真正造成伤害。
- 第三章四战首段改为无盾不能援护，观察同袍12→8；后两段继续练习自己的盾代挡与队友AI援护。全章样本走实际BoardController验证。
- 标题页冒险模式副标题补上残旗战场，章节页仍沿用三章入口及原解锁条件。
- [状态图标预览](../../Production/hand-states-v2/states-preview.png)、[武器预览](../../Production/combat-animation-v1/weapons-preview.png)、[特效预览](../../Production/combat-animation-v1/effects-preview.png)；21张动画基础素材已导出，3张手牌状态图标已替换。动画播放控制器未接入，见[设计](../../../Docs/combat-animation-design.md)。

[功能回归](functional-validation.txt)：1542通过、0失败，8项独立统计未重采样；UI脚本通过真实引擎程序集编译。本轮未启动编辑器做播放验收。[24张RGBA资源验证](asset-validation.txt)检查尺寸、alpha、边缘、SHA256及导入配置，旧状态图标GUID保留。

第三章原有1983个运行时UI检查点和1200局终战AI报告属于本次守护之誓条件调整前版本，不能当作本轮重新运行的结果；本轮新增与修订的规则用上述功能回归验证。
