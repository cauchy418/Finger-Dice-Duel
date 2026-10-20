# 1v1 战斗美术接入

2026-10-21。正式 1v1 棋盘已经接入本工程现有素材，普通开局流程直接使用新界面。

## 查看效果

团结引擎打开工程，Play → 进入 1v1 → 选角色并开局，即可正常游玩。无需拖资源、配置场景或另行去其他工具接入。

也可执行编辑器菜单 `Finger Dice Duel → Preview → 1v1 Battle Art`。它启动真实的士兵／法师对局，验证资源与点击流程，输出截图和 `validation.txt`，最后重开一局正常的双人同屏 1v1。菜单会重置当前运行中的对局，适合在测试时使用。

## 已接入

- 暗色桌面背景；黑铁底、旧金边的提示、战报、按钮与结算面板。
- 左右各 0–9 共 20 个手型，数值变化时换图；手型下方保留辅助数字和左右手文字。
- 已选中用金框，可点击用亮边；点击仍通过原有 `BoardController` 处理。
- 24 个职业绘画徽记；9 种负面效果的图标与剩余回合；图标和徽记可悬停查看说明。
- 10 种魔法伤害选择面板；13 类攻击的绘画图标用于伤害／格挡飘字，回复使用回复叠层。
- 护盾、破盾、太极、伏击与锁定由真实 Hand 状态驱动；初始手值 5 不会自动显示成型护盾。
- 血条按实际生命比例填充；右侧战报保留最近 200 条，支持滚轮和拖动，阅读历史时不强制跳到底部。
- 中文标题：静态思源宋体 CN SemiBold；正文沿用 Noto Sans CJK SC Medium，数字沿用 Bold。字体许可均随工程保留。

1v1 使用 CanvasScaler Expand 保持参考布局完整；其他人数继续使用原有环形手牌与坐标，返回 1v1 时恢复右侧战报和两人面板。双将沿用主将徽记，名称保留双将文字。

## 实现位置

- `Assets/Scripts/UI/BattleArt.cs`：Resources 加载、缓存与九宫格边距；运行时从 Texture2D 创建 Sprite，不改动 `.meta`。
- `Assets/Scripts/UI/GameBoardUI.BattleSkin.cs`：1v1 布局、手型、徽记、血条与滚动战报。
- `Assets/Scripts/UI/BattleTooltip.cs`：悬停说明。
- `Assets/Scripts/UI/StatusIconBar.cs`、`DamageTypePanel.cs`：绘画图标与选择控件。
- `Assets/Editor/BattleArtPreview.cs`：仅编辑器使用的可重复验证与截图入口。

## 截图与验证

本目录的 `battle-*.png`、`damage-choice.png` 由 Unity 相机直接渲染运行时 Canvas，包含用于展示 4／6 手型与九个状态的测试局面，不是正常开局存档。`game-view.png` 为正常开局后 Game View 的截图。`validation.txt` 记录本次引擎内检查。

本次 140 项引擎检查通过，覆盖资源映射、真实 UI 射线命中、点击进入控制器并完成运算、九状态不遮挡标题、护盾／锁定、血量比例、战报历史保留、1920×1080／1280×720／1600×1200 手牌边界，以及多人切换后的战报、按钮和两人面板恢复。正常 Game View 也已目视复核；战斗 Canvas 显式高于菜单背景，避免截图切换渲染模式后被同序号背景遮住。

978 项无头逻辑测试、FDD_UNITY_TESTS 条件编译和 UI 编译／组件冲突检查均通过。编辑器刷新编译为 0 错误、0 警告。

逻辑测试、Unity 条件编译检查和 UI 静态编译仍使用根目录 CODELY.md 的三条命令；资源完整性使用 `Tools/ArtPipeline/validate-pack.cjs`。规则与 AI 未作修改。
