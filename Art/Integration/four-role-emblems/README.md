# 四个新增子职徽记验收

2026-10-24。团结编辑器Play模式68项检查通过，逐项见 [validation.txt](validation.txt)，可重放脚本 [review.cs.txt](review.cs.txt)。全量1151项逻辑测试通过，UI编译与Unity条件编译通过。

- 37角色全部加载正式子职绘画，四枚新图均512×512；旧ID别名查询得到同一Sprite。
- 四个新角色均在实际选将列表选中、滚动到可见区域，卡片Emblem匹配正式图。
- 四个图鉴详情的徽记分别匹配，术士/游侠/武僧职业筛选数量为2/2/4。
- 四个角色分别启动1v1，战斗Canvas使用正确徽记；另验证4人猎人和8人四象宗面板。
- 龙族血脉使用最终虚化龙影版，红雾与金色血脉在小尺寸仍能辨认，原版宝石与金框保留。
- 49枚职业/子职PNG通过统一尺寸、透明度、SHA256和独立图像校验。

## 实际界面截图

| 内容 | 文件 |
|---|---|
| 四枚新徽记选将 | [selection-new-four.png](selection-new-four.png) |
| 龙族血脉详情 | [codex-sorcerer_draconic_bloodline.png](codex-sorcerer_draconic_bloodline.png) |
| 猎人详情 | [codex-ranger_hunter.png](codex-ranger_hunter.png) |
| 散打宗详情 | [codex-monk_way_of_the_open_hand.png](codex-monk_way_of_the_open_hand.png) |
| 四象宗详情 | [codex-monk_way_of_the_four_elements.png](codex-monk_way_of_the_four_elements.png) |
| 龙族血脉1v1 | [battle-draconic.png](battle-draconic.png) |
| 猎人4人面板 | [4p-new-emblems.png](4p-new-emblems.png) |
| 四象宗8人面板 | [8p-new-emblems.png](8p-new-emblems.png) |

截图为运行时Canvas渲染，不是概念图。检查覆盖资源绑定、过滤和布局；不代表新的技能数值平衡结论。原始素材、生成与编辑提示词见 [制作记录](../../Production/four-role-emblems-v1/README.md)。
