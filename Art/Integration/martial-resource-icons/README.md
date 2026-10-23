# 气与猎人标记接入验收

2026-10-24。全量1153项逻辑测试通过，UI编译及Unity条件编译通过。团结编辑器Play模式33项检查通过，逐项见 [validation.txt](validation.txt)，重放脚本见 [review.cs.txt](review.cs.txt)。

- 两枚512×512透明图标实际加载，1v1、4人、8人状态栏绑定正确。
- 两种武僧读取各自当前气点；真实行动消耗2气后从2变为0，保留图标。
- 猎人标记显示于目标，来源提示正确；对应攻击消耗标记后图标消失。
- 气点角标完整位于裁切区域内，修正底部数字被截断。
- 秘法手册第四页新增两条资源说明，所有该页条目检查无标点孤立行首。

| 内容 | 实际Canvas截图 |
|---|---|
| 1v1气点与目标标记 | [ki-and-mark.png](ki-and-mark.png) |
| 气点归零、标记已消耗 | [spent-ki-mark-consumed.png](spent-ki-mark-consumed.png) |
| 4人资源栏 | [4p-resource.png](4p-resource.png) |
| 8人标记栏 | [8p-resource.png](8p-resource.png) |
| 秘法手册 | [manual-resources.png](manual-resources.png) |

显示通过Core只读接口发现技能，兼容双将部件；未调整技能数值、条件或AI策略。素材、原图与精确提示词见 [制作记录](../../Production/martial-resource-icons-v1/README.md)。
