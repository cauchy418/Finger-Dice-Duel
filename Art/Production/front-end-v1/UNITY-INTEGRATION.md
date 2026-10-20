# Unity / 团结引擎接入清单

本文件对应 80 个独立 PNG 和已有职业／伤害／状态图标的复用。已由 FrontEndArt、GameBootstrap.FrontEnd、CharacterRosterView、CodexScreen 和 D20DieView 接入运行时。实际截图、复现菜单与检查范围见 [运行时接入记录](../../Integration/front-end/README.md)。下文保留资源使用与维护约定。

## 加载与导入

1. 打开工程让引擎导入资源并生成 `.meta`。不要从普通 Unity 工程手工拼接或改写本工程的 GUID。
2. 根路径为 `Resources/Art/FrontEnd/v1/`，调用 Resources.Load 时去掉 `Resources/` 与 `.png`。
3. 该工程的既有资源可能导入为普通 Texture2D。可以沿用 BattleArt 的做法：`Resources.Load<Texture2D>`，然后用 `Sprite.Create` 创建 Sprite；新增缓存应采用 FrontEnd 根目录，避免被 BattleArt 固定的 Battle 根路径前缀覆盖。
4. 原尺寸、资源键、透明属性、九宫格 border、文件哈希和来源均在 asset-manifest.json。`border` 顺序为 **左、下、右、上**，单位为源图像素；同一状态族的尺寸和边距一致。

```csharp
var texture = Resources.Load<Texture2D>("Art/FrontEnd/v1/UI/tab_selected");
var sprite = Sprite.Create(texture,
    new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f),
    100f, 0, SpriteMeshType.FullRect, new Vector4(40, 32, 40, 32));
image.sprite = sprite;
image.type = Image.Type.Sliced;
image.color = Color.white;
```

生产接线时统一缓存 Sprite，缺失资源输出诊断并保留可用回退。此代码片段仅示范清单中 `tab_selected` 的具体参数。

## 显示规则

- **背景**：按 16:9 等比 Cover 裁切，保持 Canvas 内主体构图；超宽或 4:3 下优先保住中央安全区。使用独立背景层。
- **框体、按钮、页签、列表行、角色格**：Image.Type.Sliced；背景填充启用 fillCenter。避免把整张图直接拉伸。
- **职业、伤害、状态、模式绘画、书卷插画**：Image.Type.Simple、preserveAspect=true、Image.color=white。职业徽记已经带旧金圆框，勿再套 badge_ring。
- **羊皮纸**：Simple，保持 2:3 比例；正文与书卷插画分别叠加。正文推荐深棕色，安全区按清单留边。无需把它染成黑铁色。
- **工具图标**：Simple，建议显示为 24–40 UI 像素；模式图标建议 96–144；职业网格建议 112–156，详情大徽记建议 256–344。
- **title_banner 和 divider**：使用清单边距的 Sliced；页码、名称单独画 Text。
- 字体：标题 UiFont.Title；正文 UiFont.Regular；属性和重要数字 UiFont.Bold。动态数据不写入背景或按钮纹理。

## 交互状态

`mode_button_*`、`tab_*`、`list_row_*`、`roster_tile_*`、`icon_button_*` 均提供 normal、hover、pressed、selected、disabled。

鼠标临时状态与持续选择状态分别处理：悬停／按下可用 SpriteSwap；持久选中由实际页面或角色选择数据决定。离开悬停后，已选角色仍应使用 selected。禁用态同时关闭对应交互，不能只换灰色图片。

蓝色底配亮金边表达当前选择，暖金边表达普通可用项，灰边表达禁用。给状态切换换 Sprite，避免整体乘色把旧金和绘画材质一起染色。

## 逐屏接线

| 页面 | 入口 | 需要改造的显示部分 |
|---|---|---|
| 主菜单 | GameBootstrap.BuildMainMenuScreen | 装入背景、三种绘画模式图标和按钮五态；保留原模式回调、图鉴／魔法球／规则／退出功能 |
| 选将 | CharacterRosterView、GameBootstrap 的角色选择构建 | 已改为六列四行绘画徽记网格；复用完整 24 人名单和 SelectionChanged；详情读取 CharacterDefinition 与注入的技能摘要 |
| 图鉴 | CodexScreen、GameBootstrap.BuildCodexIfNeeded | 复用现有 CodexCardData、阵营筛选、翻页和说明；新增左栏／徽记／正文容器；总览跳详情已有独立的显示状态 |
| 指南 | 既有伤害／状态资料入口 | 复用原 13 / 9 图标和 Core 的名称、颜色、说明，不采用参考图里未实现的状态 |

本批 `settings`、`melee/ranged/magic/support/special` 是预留图标。接线时按真实功能决定是否展示；现有代码不能据这些图片自动推导职业类别，也不应放出无响应入口。

## 验收

建议至少覆盖 1920×1080、1280×720、1600×1200；检验标题与三种模式按钮、全部 24 人可见或可滚动到、选中／禁用反馈、长中文说明、阵营过滤、页签切换、返回层级、滚动与模态遮挡。沿用独立画布排序，避免菜单背景盖住战斗画布。
