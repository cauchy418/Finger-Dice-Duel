# 束缚与油腻

2026-10-24，使用内置imagegen逐张绘制。束缚按最终要求为绿色带刺藤蔓缠绕皮革护手；油腻为倾倒铜油壶与黏稠油池。旧金方框、黑铁背景、绘画质感与现有图标一致。最初铁链方案未采用。

[manifest.json](manifest.json)保存最终精确提示词、生成来源、Alpha>8裁边范围、导出文件与SHA256；sources保留生成原图，selected为448px主体居中的512px透明PNG。运行时文件位于`Assets/Resources/Art/Battle/v1/Status/restrained.png`与`greased.png`。

可运行`node Tools/ArtPipeline/import-movement-status-icons.cjs`校验并重新导入，不改引擎meta。圣光的同尺寸修正记录在`cleric-martial-v1/selected-manifest.json`，没有重绘。实际手册、按钮及战斗截图见[验收](../../Integration/movement-statuses/README.md)。
