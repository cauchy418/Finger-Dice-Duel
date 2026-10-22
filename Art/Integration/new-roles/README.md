> 历史接入记录：截图与日志保留该阶段旧名。2026-10-23 的职业·子职重构验收见 [最新记录](../profession-subclass/README.md)。

# 新角色、图鉴分页与转移攻击接入

2026-10-22。27 名角色共用正式 Resources 徽记，三名新角色在选将拖动列表、图鉴总览、详情和战斗中正常显示。野蛮人使用用户选定的兽角图腾，狂战士复用原双刃斧。

图鉴总览每页 12 名、4 列×3 行，共 3 页；筛选重置页码，详情返回保留该角色所在页。末页 3 名新角色均通过真实射线点击打开详情，末行选将预留边距。破誓者 5、狂战士 9 的攻击标记按真实牌面显示。

破誓者持 5 受到攻击时，显示按角色去重的【转移至】按钮与【不发动】；候选必须为持完整盾的存活者，排除自己及攻击者。2v2 与 8 人界面均已真实点击，转移后继续询问圣武士代挡，最后实际目标的盾抵挡并破碎，破誓者不受伤。按钮使用绘画职业徽记与现有黑铁旧金按钮，1920×1080、1366×768 截图均已检查。

## 验证

- 实际编辑器共 **146 项检查通过**（前台 116 项，战斗／转移 30 项）。
- `front-end-validation.txt`：徽记加载、27 人选将、三页总览、详情、筛选与真实按钮点击。
- `engine-logic-validation.txt`：12 项转移回归在实际编辑器内同样全部通过，包含过期代挡答案隔离。
- `transfer-validation.txt`：2v2 / 8 人实际转移、6 名独立候选、按钮标签、射线点击、圣武士后续与盾牌结算。
- `review-front-end.cs.txt`、`review-transfer.cs.txt`：可通过当前编辑器的 `execute_csharp_script`、Play 模式重复执行；生成本目录的截图和检查记录。
- 无头逻辑 **1047 通过 / 1 项既有失败**；新增 `OathbreakerTransferTests` **12 项通过**。Unity 条件编译、UI 编译与组件检查全部通过。

### 既有失败复核

`DualVersusDualTests.All_dual_general_matchups_run_without_crashing`：咒术师+巨人对野蛮人+妖术师，种子 **21639**，模拟器被迫中断。

已用修改前提交 **f9d8c59** 单独导出代码和测试，再运行同一测试，得到相同组合、种子和强停结果；本次新增转移、分页与绘画均不在该快照中。保留此已复现问题，未放宽断言或改动无关对局规则。

## 截图

- `codex-page-1/2/3.png`：12 / 12 / 3 个角色。
- `codex-oathbreaker/berserker/wanderer.png`：大徽记详情。
- `selection-oathbreaker/berserker/wanderer.png`：选将末行可达。
- `battle-oathbreaker/berserker/wanderer.png`：三名新角色的 1v1 徽记。
- `transfer-2v2.png`、`transfer-paladin.png`、`transfer-8p.png`：转移及后续代挡。
- `transfer-2v2-1366.png`、`transfer-8p-1366.png`：小分辨率。
