# 盾牌专用并拢五指

使用内置 imagegen 编辑现有 `hand_left_5.png`，保留绘画肤色与黑铁旧金护腕，改成四根长指并拢、拇指贴紧掌侧的五指手势。原始生成结果保存在 `shield-left.source.png`。

运行 `node Tools/ArtPipeline/export-shield-hands.cjs` 会按原 5 的可见高度统一缩放、透明留边，并镜像导出右手；处理方式沿用用户已授权的切图、缩放与左右镜像。输出及 SHA-256 见 `manifest.json`。

运行时资源：

- `Assets/Resources/Art/Battle/v1/Hands/hand_left_5_shield.png`
- `Assets/Resources/Art/Battle/v1/Hands/hand_right_5_shield.png`

两张 PNG 均为 512×640、真实透明背景。只有数值为 5 且 `Hand.IsShield`（完整盾牌）使用新图；普通 5、碎盾、太极，以及圣武士的 4 / 6 盾保留原数字手型。所有战斗模式共用此映射。

## 实际生成提示词

Use case: precise-object-edit. Asset type: transparent 2D game hand sprite for a dark fantasy finger dice game, shielding number five. Edit target/reference image: the supplied palm-facing left hand with worn black iron and aged gold wrist cuff. Change ONLY the gesture: five straight fingers HELD TOGETHER, side by side, a firm upright closed-palm stop gesture, not a clenched fist. Four long fingers touch along their sides, thumb straight and tucked tightly alongside the palm/index side (thumb visible on image right as in reference); exactly FIVE anatomically correct digits total, four upright long fingers plus one thumb, no extra finger, no missing finger. Preserve the same realistic painterly skin tone, warm highlights, detailed palm creases, black iron/antique gold engraved wrist cuff, palm facing camera, upright wrist, centered isolated silhouette, and comparable proportions. Show entire fingertips and wrist cuff with clear transparent margin. Real alpha transparency, no background, no shield object, no UI frame, no typography or numerals. The narrow cohesive silhouette should read as a defensive hand, clearly distinct from the original spread-finger five. Production sprite, clean edges.
