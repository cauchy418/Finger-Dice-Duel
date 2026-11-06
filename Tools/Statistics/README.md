# 胜率采样

本目录只保留**一轮现行采样**，历史快照不再入库。

## 现行报告

| 内容 | 位置 |
|---|---|
| 最新一轮（2026-11-06，57 子职 64,768 局） | [2026-11-06-arcane/README.md](2026-11-06-arcane/README.md) |
| 机器可读汇总 | [results.json](2026-11-06-arcane/results.json) |
| 逐局记录 | 同目录 `1v1.csv`、`2v2.csv`、`dual.csv`、`eight.csv`、`identity.csv`、`identity-preferred.csv` |
| 角色名单与阵营 | `roster.tsv` |
| AI 逐角色审计 | `ai-audit.tsv` |
| 生成脚本 | [summarize-current.py](summarize-current.py) |

## 目录约定

- **一轮采样一个时间目录**：`YYYY-MM-DD-<主题>/`，主题写这一轮改了什么（`arcane`＝奥法五式与法系调整，`ranger`＝游侠调整）。
- 每个目录**必须是完整的一轮**：五个模式全部重采，不允许只补一两个模式。做不到完整就等下一轮，不要产出半份。
- 新的一轮提交后，**旧目录在同一个提交里删除**。历史交给 git：`git log -- Tools/Statistics` 能翻回去，不需要留 5 份 CSV 占 30MB。
- 跨轮对比写成一张表放进当轮 README 的「与上一轮相比」小节，不要靠保留旧目录来做对比。

## 读取口径

- 胜率＝胜场／全部出场，平局不算胜但计入分母。
- 双将子职按组合共同计胜；八人角色阵亡仍按所属阵线计分。
- 固定配桌与随机选角限制了泛化，**不能拿新旧百分点差直接归因于某一次技能修改**。
- 这是固定 AI 的结果，不是真人榜单，也不是理论最优解。

## 复现

~~~powershell
$env:DOTNET_TieredCompilation = '0'
$env:COMPlus_TieredCompilation = '0'
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -SkipStatistics
$env:FDD_REVISION_ROOT = 'Tools/Statistics/<本轮的目录名>'
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll RevisionBalanceTests
python Tools/Statistics/summarize-current.py --directory <本轮的目录名>
~~~

日志与逐局 CSV 必须随目录一起提交；生成脚本会核对名单规模、逐角色出场数与异常数。
