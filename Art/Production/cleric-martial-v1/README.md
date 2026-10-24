# 光明、战争与战斗大师素材

2026-10-24，内置imagegen生成，保持黑铁、旧金框与绘画质感。四项512×512透明PNG：光明为翼日圣焰，战争为战锤与红战旗，战斗大师为交叉剑锤与战术图，圣光为方框圣焰。原始生成图保存在sources；selected为按Alpha裁边、缩放居中后的导出。三张徽记主体480px、状态448px。

源路径、资源键和SHA256见[selected-manifest.json](selected-manifest.json)。使用 `node Tools/ArtPipeline/import-cleric-martial-icons.cjs` 导入，已挂入新角色总导入末尾；不手工编辑meta。正式45枚职业/子职徽记通过统一验证。Unity自动导入后用于图鉴、选将和战斗。生成网络中断后只重试未完成的战斗大师与圣光，没有替换已选图。

生成方向摘要见 [prompts.json](prompts.json)，原始源图及其生成路径见清单。

2026-10-24尺寸复核：圣光（圣火）原图的极低透明度外点导致主体仅约350px。以Alpha>8的可见主体重新裁边，按448px居中到512px；原始sources保留，selected、SHA256及运行时图片同步。与其他状态使用相同UI尺寸，见[验收](../../Integration/movement-statuses/README.md)。
