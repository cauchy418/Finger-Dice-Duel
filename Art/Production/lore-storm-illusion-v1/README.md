# 逸闻学院、风暴术法与幻术学派徽记

2026-10-25，内置imagegen逐张生成3枚徽记，以勇气学院／狂野魔法／防护学派作风格参考，保持旧金双圆框、菱饰、黑铁底与绘画主体。

- 逸闻：书卷、银羽笔与小竖琴；风暴：蓝晶、闪电与紫色声波环；幻术：镜中虚像与银金面具。
- [三图预览](contact-sheet.jpg)、[完整提示词与原始生成路径](generation-manifest.json)、[正式文件／裁边／SHA256](selected-manifest.json)。
- `sources/`保留生成原图；`selected/Subclasses/`为512×512 RGBA。按Alpha>8裁边，主体等比480px，透明居中；沿用用户授权的裁切与缩放，未手工修改meta。
- 运行时：`Assets/Resources/Art/Battle/v1/Subclasses/`同ID三图。重导入`node Tools/ArtPipeline/import-lore-storm-illusion.cjs`，完整57枚校验`node Tools/ArtPipeline/validate-profession-icons.cjs`。
- [实机选择、图鉴及技能截图](../../Integration/lore-storm-illusion/README.md)。
