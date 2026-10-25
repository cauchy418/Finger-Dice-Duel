# 所有模式：职业→子职选角验收

团结引擎2022.3.62t15实机，真实EventSystem射线点击；1920×1080渲染。共410项通过：[228项职业与全部子职检查](validation-roster.txt)、[182项模式与开局衔接检查](validation-modes.txt)。没有修改玩家通关记录或设置。

- 初始显示12个职业徽记及各自可选人数，进入后只显示该职业子职；返回保留已选角色，重新进入选角回到职业页。
- 全部45子职逐个实际点击，核对完整名单索引与详情；玩家和对手／队友可独立浏览。
- 1v1人机／真人，入门／经典均将所选子职带入对局；2v2入门直接开局，经典将正确的四名角色交给原先攻流程。
- 8人阵营与身份局均实际开局；身份局排除已选主公，默认索引0也可明确点击确认，刷新后保留所在职业。
- 双将1v1及8人：主将选毕，副将重新先选职业，只归类14名同阵营且不重复的候选；返回不清主将，实际组合可开局。
- 终战先显示唯一战士职业，进入后6个子职；所选战术家正确进入骑士挑战。

[角色卡10项](card-tests.txt)、[职业契约2项](contract-tests.txt)、[164脚本UI编译](ui-compile.txt)均通过。玩法、角色数值与合法范围沿用；完整1317项逻辑回归及胜率报告保持上一轮结果。

复现：[点击与渲染助手](helpers.cs.txt)＋[职业／子职脚本](review-roster.cs.txt)或[模式脚本](review-modes.cs.txt)，通过工程已有Codely的`execute_csharp_script`在Play模式执行。

- [职业大类界面](1v1-professions.png)
- [法师子职界面](1v1-wizard-subclasses.png)
- [2v2](2v2-Classic.png)
- [双将副将职业范围](dual-secondary-True.png)
- [终战职业](finale-profession.png)／[子职](finale-subclasses.png)
