# 伤害保护与8人局验收

2026-10-24。26张新绘易伤／免疫图标逐类型接入；原13类抗性保留。秘法手册五页，第四页三组各13图标，第五页为奥术屏障、气、猎人标记。元素反应目前九条（本批酸毒互刷两条，后续油腻补一条）。

## 规则与修复

- 免疫优先，其他情况先易伤×1.5再抗性减半，各自向上取整。落地后一起消耗临时效果，整次多段共享，完全盾挡／闪避／取消不消耗。免疫魔法也阻止本击负面效果与反应，不净化已有状态。
- 酸毒在命中后互刷原有状态至2回合，保留来源；伤害为0不妨碍命中反应，免疫与闪避阻止。原有防护学派仍只有六项赋予选项。
- 8人身份局的玩家冲击曾因控制器与对局使用不同答复容器而丢失选择；现复用真实容器，并从已收集答案读取选中目标。候选／落地均检查实际射程，不将超距加数角色当回退目标。被动破盾通知去重，恢复一次2伤；主动碎旧盾、合新盾仍分别冲击。
- 身份阵线独立于战斗Teams：开局主忠2队友／5对手，反贼3／4，内奸0／7；人数只计存活者。未知身份座位无关系后缀，仅已知关系显示队友／对手，不改变友伤。

## 验证

新增`DamageAffinityTests`16项、`EightPlayerPresentationAndShockTests`6项。最初1247项完整回归通过；后续束缚／油腻的最终完整回归与编译日志集中在[状态验收](../movement-statuses/README.md)。

实际引擎最终 **214项通过**，见[逐项结果](validation.txt)。包括22项真实引擎规则测试、39图标加载、手册实际滚动和文字高度、8人阵营及身份玩家真正射线点击冲击、选中邻居只掉2血、超距加数角色不受伤、四种身份视角及全部临时效果图标。没有将测试局配置写入普通对局；恢复AI等待时长并返回主菜单。

## 画面与复验

- [抗性](manual-resistances.png)／[易伤](manual-vulnerabilities.png)／[免疫](manual-immunities.png)／[角色标记](manual-character-marks.png)／[元素反应](manual-eight-reactions.png)。最后一张文件名沿用首版，内容已为九条。
- [阵营选择冲击](faction-shock-choice.png)／[冲击落地](faction-shock-landed.png)；[身份选择冲击](identity-shock-choice.png)／[冲击落地](identity-shock-landed.png)。
- [主公视角](identity-relations-0.png)／[忠臣视角](identity-relations-1.png)／[反贼视角](identity-relations-3.png)／[内奸视角](identity-relations-7.png)。
- 通过本地Codely执行`helpers.cs.txt`＋`review.cs.txt`，Play模式；底层配置是明确的验收场景，点击与结算走正式UI和控制器。美术提示词和原图对应见[制作记录](../../Production/damage-affinities-v1/README.md)。
