# 第三章：残旗战场验收

后续修订：2026-10-31守护之誓新增自身完整盾前提，四战首段已重写；本页截图及1200局数据保留为原版本记录，新版本验证见[奉献之誓修订](../devotion-shield-animation/README.md)。

2026-10-30。十战已接入，全局编号21～30。前九战23个独立样本，终战圣武士自选、正常经典2v2；权威配置见[章节与逐战文档](../../../Docs/Adventure/chapter-03.md)。

## 验证口径

- 全量功能回归：**1541通过、0失败**；跳过8项独立大规模统计，另执行本章1200局终战采样。第三章8项专项覆盖23个样本真实控制器、借数与目标分离、奉献双冲击、队友AI援护、12种束缚随机种子、奖励行动计数、隔离存档键、六子职与四种玩家席位。
- 真实引擎程序集UI编译、组件检查、`FDD_UNITY_TESTS`条件编译通过。`BoardController.CompletedActions`只为导演记录标准运算／休整／奖励运算，不推进原回合／轮次，不计纯运算。
- `guided-validation-21-24.txt`与`guided-validation-25-29.txt`记录实际界面逐步点击：所有23样本、NPC技能候选徽记、唯一高亮答案、相反选项仍显示但禁用、指引翻页不改战场、留场后日志可用、点试炼结束再出现结果。点击通过EventSystem射线验证，不只调用按钮回调。
- `finale-start-validation.txt`与`finale-finish-validation.txt`记录旧存档解锁、职业→六子职、双方预览、真实四人骰与分手值按钮、换位后人类／同袍引用、首席先行动、正常AI自由实战、按队伍胜负保存、结束留场及重选清理。
- `regression-validation.txt`记录未过样本恢复、同样本重试、离场清理、1280×720／1920×1080实际RenderTexture与CanvasScaler排版，以及第二章胜利页保留原三按钮并新增第三章转场。其第二章胜利使用明确的1生命敌人测试夹具，只验证结果界面，不用于难度统计。

实际界面检查合计**1983项**：前段462、后段1419、终战入口16、终战收尾35、续练／转场／分辨率51。检查点包含重复验证各步角色可见性，不等同1983个独立测试用例。

所有脚本临时改写的PlayerPrefs均在finally恢复；终战采样不写存档。图中进度是测试环境，不代表用户进度。布局截图用临时相机渲染，结束恢复画布状态。

末轮复验遇到本机IL后处理服务失联、编辑器窗口资源不足；在确认场景无未保存修改后，使用已安装团结引擎带图形设备的后台编辑器完成复验，显式开启桥接插件支持的`UNITY_TCP_ALLOW_BATCH=1`。游戏不依赖该环境变量；不修改插件包或系统设置。冒险模态正文改用原生折行并保留作者分段，避免窗口缩放后手工断行再次被折成短行。

## 终战AI代理采样

固定古贤之誓同袍，对手指挥官24生命、攻+1／防0、距1挥砍，弩手20生命、0／0、距2穿刺。六子职各200个相同种子，真实经典四人投骰、公共合法分配策略；四人困难AI，启用正式角色策略与集火，沿实际BoardController技能与多阶段请求运行。

| 玩家子职 | 玩家队胜 | 对手队胜 | 平局 | 玩家队胜率 |
|---|---:|---:|---:|---:|
| 奉献之誓 | 168 | 32 | 0 | 84.0% |
| 王冠之誓 | 87 | 113 | 0 | 43.5% |
| 破誓者 | 128 | 72 | 0 | 64.0% |
| 古贤之誓 | 125 | 75 | 0 | 62.5% |
| 征服之誓 | 115 | 84 | 1 | 57.5% |
| 复仇之誓 | 113 | 87 | 0 | 56.5% |
| 合计 | 736 | 463 | 1 | 61.3% |

安全中止0，原始数据含平均行动数见[CSV](finale-ai-probe.csv)。这是冒险终战首版难度观察，不是真人胜率，也不混入竞技角色胜率。近战王冠更依赖盾碎扩距，奉献代挡与冲击在本配置中更有利；六种子职均能获胜，未调整设计数值或加入隐藏保护。真人难度还需游玩反馈。

## 复验

```powershell
$env:DOTNET_PROCESSOR_COUNT='4'
./Tools/HeadlessTests/run-tests.ps1 -SkipStatistics
./Tools/UiCompileCheck/check-ui.ps1
./Tools/UiCompileCheck/check-components.ps1
./Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll --banners-probe Art/Integration/adventure-chapter-03/finale-ai-probe.csv
```

编辑器处于Play模式、已有本项目Codely bridge时，使用Python运行本目录`run-ui.py guided 21 24`、`guided 25 29`、`finale-start`、待实际骰子动画结束后`finale-finish`，以及`regression`。先攻开始和结束必须连续运行；过程中不关闭编辑器。`ui-common.csx`提供反射、射线点击、截图辅助，其余csx为可审阅的场景脚本。

## 界面截图

- [四人借数](borrow-four-players.png)
- [奉献援护选择](choice-24-1-1.png)
- [八人公开阵营](eight-faction-battlefield.png)
- [八人最后结果与结束按钮](result-29-3.png)
- [圣武士选角与队伍预览](finale-paladin-selection.png)
- [四人经典先攻](classic-two-on-two-dice.png)
- [真实落座后的终战](classic-two-on-two-battle.png)
- [1280×720援护](protection-1280x720.png)
- [1280×720八人援护](eight-protection-1280x720.png)
- [第二章胜利转场](tower-to-battlefield.png)
