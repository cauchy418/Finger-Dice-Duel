# 法师学派、抗性与屏障素材

2026-10-23。使用内置 `image_gen` 生成/局部修正，统一暗色奇幻、旧金装饰框和绘画主体。提示词、原始生成路径和后续修正记录见 [prompts.json](prompts.json)。

## 最终资源

| 类别 | 数量 | 位置与用途 |
|---|---:|---|
| 子职徽记 | 4 | `selected/Subclasses/`：防护、预言、死灵及新塑能 |
| 抗性 | 13 | `selected/Resistance/`：十三种伤害类型对应盾形抗性图标 |
| 奥术屏障 | 1 | `selected/Effects/arcane_barrier.png` |
| 法师大类 | 1复用 | `selected/Professions/wizard.png`：旧塑能的书本与蓝晶法杖 |

共18张新绘、1张复用，19项512×512 RGBA导出。圆徽最大主体480px，方形状态图标448px，保留透明留白。`sources/`保存最终采用的原始生成图；`previous/`保留替换前法师与塑能徽记。复用不计入新绘数量。

防护以蓝色屏障与同心防护法阵表达；预言以沙漏与星轨表达；死灵以骨白、青绿灵魂与死灵书表达。新塑能以火焰、闪电、寒冰汇聚晶体，底部五指施法手与法师大类的书杖区分。

暗蚀抗性最终改为与现有 `Damage/necrotic` 一致的冷翡翠绿骨爪与枯叶。初始提示词中的紫色已被后续色彩修正覆盖；最终取 `generatedSource` 与 `selected-manifest.json` 指向的文件。骨爪最终为四根长指加一根独立拇指；塑能施法手最终为四根上伸手指加一根侧向拇指，均已按玩家反馈修正。

## 导入与接入

运行 `node Tools/ArtPipeline/import-wizard-school-icons.cjs`，按 [selected-manifest.json](selected-manifest.json) 校验尺寸、Alpha和SHA256后覆盖运行时PNG；不创建或编辑 `.meta`。该步骤也已挂入原新角色导入流程末尾。`validate-profession-icons.cjs` 校验当前12职业+30子职的42枚正式徽记与最终清单一致。

运行时位置：`Assets/Resources/Art/Battle/v1/`，资源键为 `Subclasses/<ID>`、`Professions/wizard`、`Resistance/resistance_<type>`、`Effects/arcane_barrier`。元数据由团结编辑器导入生成。

防护学派只可选物理整组、雷鸣、强酸、火焰、寒冷、闪电六项；其余抗性图标保留为通用资源，不加入其技能选项。手册第四页列完整图标并注明此限制。选将、职业筛选图鉴、1v1及多人面板直接使用这些资源，见 [引擎验收](../../Integration/wizard-schools/README.md)。
