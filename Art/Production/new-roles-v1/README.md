# 新角色绘画徽记

2026-10-22。使用内置 **image_gen**，3 张新绘画原图与完整提示词均保留，未使用 CLI/API fallback。按用户已有授权执行透明边界裁切、缩放与居中，导出 512×512；金框完整，透明外部，黑铁内部。引擎 `.meta` 均由资产刷新生成。

| 角色 | 采用方案 | 正式资源 |
|---|---|---|
| 圣武士·破誓者 | 紫焰、碎誓盾、断剑 | `Assets/Resources/Art/Battle/v1/Subclasses/paladin_oathbreaker.png` |
| 野蛮人·狂战士道途 | 用户选定：原野蛮人·守望者道途的双刃战斧 | `Assets/Resources/Art/Battle/v1/Subclasses/barbarian_path_of_the_berserker.png` |
| 战士·战术家 | 旅帽、披风、弯刀、地图 | `Assets/Resources/Art/Battle/v1/Subclasses/fighter_tactician.png` |
| 野蛮人·守望者道途 | 用户选定：新绘兽角、兽皮图腾 | `Assets/Resources/Art/Battle/v1/Subclasses/barbarian_path_of_the_warden.png` |

`source/` 保存未修改的生成原图；`prompts/` 为完整生成提示词；`candidates/` 保存选定导出和原双刃斧副本；`manifest.json` 保存边界、原图尺寸、正式路径、哈希与用户选择。正式徽记使用 Simple Image、白色，不额外加圆环。

重新导入并校验 4 张选定文件：

```text
node Tools/ArtPipeline/import-new-role-icons.cjs
```

旧 `import-painted-icons.cjs` 在结束时也会应用本批选定徽记，防止旧图集还原野蛮人·守望者道途。素材与 Unity 实际截图的区别见 [界面验收](../../Integration/new-roles/README.md)。
