# 气与猎人标记（2026-10-24）

使用内置 imagegen 绘制两枚独立图标，沿用暗色奇幻、黑铁底、旧金装饰方框与绘画主体。精确提示词见 [prompts.json](prompts.json)，生成源路径、正式路径与 SHA256 见 [selected-manifest.json](selected-manifest.json)。

| 图标 | 画面 | 正式资源 |
|---|---|---|
| 气 | 玉色核心与象牙白、青绿色流动气息 | `Effects/ki` |
| 猎人标记 | 银色箭头、琥珀瞄准环、深绿羽饰与红色刻痕 | `Effects/hunters_mark` |

`sources/` 保存生成原图；`selected/Effects/` 保存512×512 RGBA导出。按透明边界裁切、等比缩放至448px后透明居中，沿用用户已授权的程序裁切与缩放流程。

运行时文件位于 `Assets/Resources/Art/Battle/v1/Effects/`。`Tools/ArtPipeline/import-martial-resource-icons.cjs` 先验证全部尺寸、Alpha与哈希，再写入运行时PNG，不手动改meta；已接入新角色总导入流程。

气显示在武僧姓名旁，角标为当前资源点数，0点仍显示。猎人标记显示在被标记者姓名旁，悬停可查来源，由对应猎人的攻击消耗；多名猎人标记同一目标时角标为来源人数，不表示持续回合。两项也加入秘法手册第四页的「资源与标记」分组。实机截图见 [验收记录](../../Integration/martial-resource-icons/README.md)。
