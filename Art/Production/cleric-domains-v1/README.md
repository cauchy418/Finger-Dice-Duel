# 诡术／风暴领域与耳鸣美术

2026-10-28，使用内置 image_gen（built-in mode）生成。沿用暗色奇幻、绘画主体、旧金装饰框；职业徽记圆框，状态方框。诡术为狐面与交错钥匙，风暴为风暴圣杖，耳鸣为耳廓、破钟与声波。

完整生成提示词：[prompts.json](prompts.json)。原始PNG保存在sources/；透明导出与SHA-256清单：[selected-manifest.json](selected-manifest.json)。按用户已授权方式裁去透明空白、等比缩放、居中到512×512，徽记可见范围480px，状态448px。

正式资源：Assets/Resources/Art/Battle/v1/Subclasses/cleric_trickery_domain.png、cleric_tempest_domain.png；Status/tinnitus.png。职业／子职总清单新增两项，共66项。重导入运行Tools/ArtPipeline/import-cleric-domains.cjs，会验证尺寸、Alpha和哈希；不手写或覆盖引擎.meta。
