# 手牌状态图标 v2

2026-10-31。完整盾、碎盾与太极改为绘画材质：黑铁旧金盾、斜向裂开的破盾、黑曜石／象牙阴阳盘。[预览](states-preview.png)。

内置 image_gen 分别绘制三张透明原图，`source/`保留原图；[清单](states-manifest.json)包含完整提示词、文件路径与SHA256。按已授权的程序裁切／缩放导出256×256，三张图使用一致的居中与主体占比。

已替换`Assets/Resources/Art/Battle/v1/Overlay/shield.png`、`shield_broken.png`、`taichi.png`，保留原GUID及资源键，所有使用这些手牌标记的界面自动读取新版。状态图标仍为原38×38布局，不遮数字；手型本身保持原有接入。
