# 指骰对决标题接入验证

- UI编译通过。
- Unity实际MainTitle读取UI/title_logo_zh，等比显示，raycastTarget=false。
- 旧SubTitle和MainDivider节点已移除，不重复绘制英文名和装饰。
- 临时相机渲染原生菜单画布，分别验证1920×1080与1280×720；标题未与冒险按钮重叠。相机、RenderTexture和画布模式在验证后恢复。
- 可编辑文档中旧中文游戏名已无残留；历史截图中的烘焙文字作为旧版图像保留。

[1920×1080](main-menu-1920x1080.png) · [1280×720](main-menu-1280x720.png)

复现脚本：validate-title.csx，在已运行的项目内通过本地Codely桥接执行。不属于游戏运行代码。
