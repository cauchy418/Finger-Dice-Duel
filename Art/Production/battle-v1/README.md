# 战斗界面素材包 v1

2026-10-20。依据已确认的手型版战斗概念，采用黑铁、旧金细框与奇幻桌游道具。

本批交付为独立素材文件，可以导入团结引擎；尚未把素材接入现有游戏界面。`battle-assembly-preview.png` 是由实际导出文件拼装的静态检查图，不是编辑器运行截图。

## 文件

正式 PNG 位于 `Assets/Resources/Art/Battle/v1/`，共 97 张：

| 目录 | 数量 | 内容与规格 |
|---|---:|---|
| Hands | 20 | 左右手各 0～9；512×640，透明，无数字；手腕基线 y=610 |
| Backgrounds | 1 | 无文字、无手牌、无 UI 的桌面背景；保持生成原始分辨率 |
| UI | 19 | 手牌底板与四态边框、四态按钮、战报/提示面板、血条轨道与填充、数字标签底、徽记环、分隔线、状态计数底、回合标记、d20 装饰 |
| Damage | 13 | 所有伤害类型图标；256×256；键沿用现有伤害类型体系 |
| Status | 9 | 所有负面效果图标；256×256；与伤害图标区分轮廓 |
| Classes | 24 | 所有可玩角色职业徽记；256×256；无人物立绘 |
| Overlay | 11 | 盾、碎盾、锁定、太极、暗置、潜行、攻击、防御、距离、回复、阵亡标识 |

全部 PNG 均不烘焙名字、数字或游戏文案。正式界面必须继续从规则数据绘制这些内容。基础手型与状态叠层分离，不以普通数值 5 自动显示护盾。

## 手型修正与检查

- 原图集 4 有多指、6 缺少一根收拢手指，已隔离，不用于正式输出。内置图像工具分别重新绘制了 `hand-4-repaired.png` 与 `hand-6-repaired.png`。
- 最终 4：四根长指伸出、一个拇指收拢；最终 6：小指与拇指伸出，另外三根手指收拢。
- 原图集中的 0 和 9 朝向与其余手型不同，已先镜像为解剖学左手，再生成对应右手。左手的拇指根部位于画面右侧。
- 按用户明确授权进行图集切分、尺寸统一、左右镜像；切图时隔离邻格残片和透明背景上的红黄遮罩杂点。按护腕宽度统一比例，保留各姿态真实高度差。
- 手型 7 的捏合部分有手指互相遮挡；单独母版和总览均保留，后续可按实际显示大小继续优化。

`hands-source-v1.png` 和 `raw-hands/` 是原始生成及裁切资料，包含已被拒绝的旧 4/6，不应导入游戏。正式使用 `Assets/Resources/Art/Battle/v1/Hands/`。

## 源文件与总览

- `svg/`：76 个矢量母版，对应 UI、伤害、状态、职业与叠层图标；由 `Tools/ArtPipeline/build-battle-assets.cjs` 原创绘制并导出 PNG。
- `prompts/`：内置 image_gen 的完整生成与修正提示词；本次没有使用 CLI/API fallback。
- `hands-contact.png`：上排左手 0～9，下排右手 0～9。
- `classes-contact.png`、`damage-contact.png`、`status-contact.png`、`overlay-contact.png`、`ui-contact.png`：对应独立素材总览。
- `battle-assembly-preview.png`：使用真实 PNG 拼装的 1920×1080 战斗预览。
- `small-size-check.png`：伤害、状态与职业图标在 28/40/64 像素的并排检查图。
- `asset-manifest.json`：每个文件的尺寸、资源键、校验哈希和九宫格边距。
- `hand-manifest.json`：手型来源、4/6 修正记录、0/9 朝向修正与腕部基线。
- `validation-report.json`：本批文件检查记录。

图标采用便于 28～64 像素辨识的简化矢量纹章，属于第一版可编辑资源；材质与线条细节比概念图里的绘画装饰更克制。

## 导入与组装

1. 由团结引擎生成 `.meta`，本次没有手工创建或修改它们。
2. 手型、图标、面板设置 Sprite (2D and UI)、Single、sRGB、Alpha Is Transparency；关闭 Mip Maps 和 Read/Write，初始使用 Bilinear，先关闭有损压缩以检查细线。
3. 手型使用统一画布和手腕锚点；`asset-manifest.json` 中 pivot 为 [0.5, 0.046875]（Unity 左下原点），对应源图 y=610。默认中心锚点同样可用，但不要按每张可见轮廓重新居中。
4. 可拉伸边框与面板采用 Sliced；`border` 顺序为左、下、右、上，单位为源图像素。手型和 `hand_plate` 用 Simple 并保持比例，避免扭曲手指或底板纹样。
5. 血条填充单独使用 Filled / Horizontal，血量文字独立绘制；轨道放在填充后方，必要时为填充增加独立遮罩，不能让轨道不透明中心盖住填充。
6. 推荐手牌层级：底板 → 手型 → 状态标识 → 下方数字/左右手文字 → 边框。白框表示可点，金框表示选中；普通框较暗。技能状态标识与可点击射线区域分别处理。
7. 职业徽记复用现有 CharacterDefinition.Id（含 `arcane_archer`）；双将组合显示两个徽记。负面效果计数、图鉴说明和伤害飘字继续由现有数据驱动，不修改规则层。

本次未更改 C#、场景或项目设置。素材检查覆盖文件、透明通道、尺寸、镜像一致性、总览与静态组装；发布前补跑了工程要求的三项检查：978 项逻辑测试全部通过、Unity 侧测试代码编译通过、UI 编译与组件检查通过。实际点击、多人布局和 Unity Sprite 导入效果仍需接入后验证。

## 重建

使用 Node.js 和 sharp。脚本优先使用 Codex 自带 sharp，也可以通过 FDD_SHARP_PATH 指定本机模块位置。按顺序执行：

```text
node Tools/ArtPipeline/build-battle-assets.cjs
node Tools/ArtPipeline/finalize-hands.cjs
node Tools/ArtPipeline/preview-battle-assets.cjs
node Tools/ArtPipeline/validate-pack.cjs
```

原始图集裁切只在需要重做时执行 `prepare-hands.cjs <图集路径>`；不要用原图集 4/6 覆盖修正版。背景文件须保留在正式资源目录中。
