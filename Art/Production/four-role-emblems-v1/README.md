# 新增四子职徽记（2026-10-24）

使用内置 imagegen，沿用暗色奇幻、黑铁底、旧金圆框和绘画质感。精确提示词见 [prompts.json](prompts.json)，生成源路径和导出哈希见 [selected-manifest.json](selected-manifest.json)。

| 子职 | 正式ID | 图案 |
|---|---|---|
| 术士·龙族血脉 | sorcerer_draconic_bloodline | 赤红雾气与金色血脉构成的虚化祖龙，保留原下方宝石 |
| 游侠·猎人 | ranger_hunter | 猎鹰、猎矛和瞄准环，深绿皮革 |
| 武僧·散打宗 | monk_way_of_the_open_hand | 缠布拳与冲击环，岩石浮雕 |
| 武僧·四象宗 | monk_way_of_the_four_elements | 四股元素流围绕玉色核心形成法印 |

`sources/`保留原始生成PNG，`selected/Subclasses/`为512×512 RGBA正式导出，按Alpha边界裁切后等比缩放至480px、透明居中。程序处理沿用用户已授权的裁切/缩放流程。

运行时复制至 `Assets/Resources/Art/Battle/v1/Subclasses/<正式ID>.png`。统一资源查询自动用于选将、图鉴和战斗；`Classes/<旧ID>`也可查询同一资源。资源不依赖默认生成目录。

`Tools/ArtPipeline/import-four-role-emblems.cjs`先验证全部文件的尺寸、Alpha与SHA256，再覆盖运行时PNG，不手工改meta；已挂入原新角色总导入末尾。四枚也加入职业/子职统一清单，当前为12职业+37子职共49枚独立正式徽记。界面截图和验收见 [接入记录](../../Integration/four-role-emblems/README.md)。

龙族血脉按用户反馈由实体龙首改为虚化龙影；中央琥珀与下方五颗宝石、边框保留。原版存previous，精确局部修改指令见[draconic-edit.json](draconic-edit.json)。
