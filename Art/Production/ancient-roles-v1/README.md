# 大地结社、古贤之誓与妖魔

2026-10-25，使用内置imagegen逐张绘制3枚新子职徽记和1枚虚假生命标记。以现有正式素材作图像参考，保留黑铁内底、旧金双层圆框／方框、绘画质感；主体分别为苔石橡根、古树银盾、炼狱契约。虚假生命按用户补充由红色精确编辑为绿色心核，金框与构图保持。

- [四图预览](contact-sheet.jpg)、[完整原始提示词及绿色编辑提示词](generation-manifest.json)、[裁边／SHA256／运行时对应清单](selected-manifest.json)。
- `sources/`保留4张采用原图与改绿前原图；`selected/`为512×512 RGBA导出。按Alpha>8裁边，圆徽等比缩放到480px、方标448px，透明居中，沿用用户授权的程序裁切／缩放。
- 正式目录为`Assets/Resources/Art/Battle/v1/Subclasses/`三图与`Effects/false_life.png`。绿色角标为当前防护点，最多10，不是回合。
- 重导入：`node Tools/ArtPipeline/import-ancient-role-art.cjs`；校验全部57枚职业／子职：`node Tools/ArtPipeline/validate-profession-icons.cjs`。导入链最后应用新增素材，meta由引擎生成。
- [实际引擎/UI验收](../../Integration/ancient-roles/README.md)。
