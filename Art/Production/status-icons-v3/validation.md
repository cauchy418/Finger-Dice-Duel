# 状态图标与秘法手册验收（2026-10-21）

## 素材与手册

- 内置 image_gen 新绘制 9 张；完整冰封手／精神惊惧画面复用为冻结／恐慌，18 种实际状态均有不同的规范资源与画面。
- 导出脚本检查 18 张 512×512 PNG、透明边缘、非空主体与 SHA-256 唯一性，通过。原始图、正式导出和完整提示词均已保存在工程内。
- 秘法手册专项 13 项全部通过：三页顺序、18 种状态、资源键唯一、枚举兼容、持续与刷新、六条元素反应。元素反应页不含心灵概率说明；该说明仍保留在伤害类型页。
- `FDD_UNITY_TESTS` 条件编译通过；UI 编译与组件冲突检查通过。
- 引擎实际截图见 `Art/Integration/front-end/` 与 `Art/Integration/multiplayer/`；全部 `.meta` 均由引擎导入生成。
- 前台 808 项检查、多人 3089 项检查全部通过；确认三页循环翻页、六条反应完整可见、18 张图标准确加载，且状态栏不遮挡名字和手牌。

## 全量逻辑检查的既有失败（2026-10-21 历史记录）

全量 997 项中，994 项通过、3 项失败。为区分本次美术接入与既有问题，在忽略目录中解出修改前的 `2d79d3e` 源码，分别运行相同测试，确认以下三项也会失败：

| 测试 | 失败原因 | 修改前复现 |
|---|---|---|
| `BattleReadabilityTests.Rules_describe_current_opening_modes_and_health_tiebreak` | 规则提示缺少「冲击」用词 | 相同用词断言失败 |
| `MatchTests.Full_character_matrix_can_all_be_played_to_completion` | soldier vs sorcerer 未能在模拟上限内结束 | 相同配对失败 |
| `CharacterBalanceTests.All_characters_round_robin_win_rates` | 循环赛出现模拟器强停 | 修改前 243 局、本轮 249 局；随机赛数字会变化 |

未改动角色技能、状态结算、模拟器或放宽这些断言。测试产生的随机统计战报已恢复，避免把临时随机结果当成规则或平衡变更。

引擎刷新另报告 1 个既有编辑器异常：`UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager.OnBeforeSerialize` 的 NullReferenceException；没有 C# 编译错误。真实运行时界面检查可正常执行。

## 2026-10-22 后续修复

冻结／无合法运算误判结束、跳过未推进状态、震慑阻断全部运算及攻击目标过滤现已修复，规则说明补回冲击。当前 1013 项测试全部通过，上述三项失败均已消除；实际按钮 264 项检查通过，详见 [回合出口验收](../../Integration/turn-exits/README.md)。
