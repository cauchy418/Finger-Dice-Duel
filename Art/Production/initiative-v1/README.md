# 先攻 1d20 绘画素材

使用内置 image_gen 生成一枚独立的黑曜石／旧金棱线二十面骰，真实透明背景，中央留白给游戏实时点数；完整提示词和处理约定见 `prompt.json`。

- 原始生成文件：`d20_obsidian-gold.source.png`（1280×1280 RGBA）。
- 正式资源：`Assets/Resources/Art/FrontEnd/v1/Illustrations/d20_initiative.png`（512×512 RGBA）。
- 处理：等比缩至 464×464，四周增加 24px 透明边距，保留原始 alpha；未重绘或重建背景。
- 引擎资源键：`Art/FrontEnd/v1/Illustrations/d20_initiative`。
- `D20DieView` 叠加 1–20 数字、名称与大成功／大失败标签；先攻界面从现有控制器读取最终点数，动画沿用 DiceRollAnimationPlan。
- 1v1 显示两枚，2v2 显示四枚。45 项拆分使用可拖动、可滚轮的受限区域，翻转按钮独立在底部。

生成用内置工具完成，没有 CLI/API fallback。程序缩放已由用户在本会话授权。原始图保留在工程内，后续可按需导出其他尺寸。
