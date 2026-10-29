# 残旗战场背景

2026-10-30，使用内置 image_gen 绘制。第三章整章共用的战斗背景，沿用黑铁、旧金与绘画质感；破损军旗、盾牌与营垒集中在画面边缘，中央保留多人角色面板和操作提示的空间。图中不包含角色、文字或界面。

- `sources/tattered_banners_native.png`：原始生成图，1672×941。
- `selected/Backgrounds/tattered_banners_chapter_03.png`：1920×1080 RGB运行时选用图，由原图等比放大后居中裁切；并非原生高清重绘。
- `prompt.txt`：完整生成提示词。
- `manifest.json`：生成方式、文件尺寸、SHA-256及资源键。

运行时副本：`Assets/Resources/Art/Battle/v1/Backgrounds/tattered_banners_chapter_03.png`，资源键 `Backgrounds/tattered_banners_chapter_03`。编辑器已刷新并生成独立纹理元数据，刷新返回0错误、0警告。只准备素材，尚未修改章节入口或战斗背景选择代码；构图仍需在实际四人／八人布局中验收。

生成源来自 `.codex/generated_images/01a1586e-64f7-7d33-954d-9314aa85037d/exec-f1cdbe22-6118-4b68-abff-37767af0287f.png`。保留完整原图，使用已获授权的 Pillow 缩放／裁切导出选用版本。设计见[第三章](../../../Docs/Adventure/chapter-03.md)。
