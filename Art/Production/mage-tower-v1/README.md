# 法师高塔素材

2026-10-26，使用内置 image_gen 绘制。黑铁、旧金边框与油画质感沿用当前战斗美术。原始生成图存于 sources，提示词与来源文件名记录在 generation-manifest.json。

| 文件 | 用途 |
|---|---|
| selected/Backgrounds/mage_tower_chapter_02.png | 第二章整章战斗背景 |
| selected/Adventure/npc_arcane_guard.png | 奥法护卫 |
| selected/Adventure/npc_mage_phantom.png | 法师幻影 |
| selected/Adventure/guide_arcane_codex.png | 秘法典籍 |

三枚徽记为512×512 RGBA，主体等比缩至480像素内并居中。背景保留原始16:9构图。可用 Pillow 运行 `python Tools/ArtPipeline/prepare-mage-tower.py` 重建选用版本；selected-manifest.json 记录尺寸、资源键与校验和。

运行时版本已导入 Assets/Resources/Art/Battle/v1，专用资源已接入第二章；Unity 元数据由编辑器生成。关卡设计见 [第二章](../../../Docs/Adventure/chapter-02.md)。
