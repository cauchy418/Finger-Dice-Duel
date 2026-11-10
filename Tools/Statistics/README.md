# 胜率采样

本目录只保留**一轮现行采样**，历史快照不再入库。

## 现行报告

| 内容 | 位置 |
|---|---|
| 现行汇总（58子职；身份新采2,600局，其余沿用） | [2026-11-10-identity/README.md](2026-11-10-identity/README.md) |
| 机器可读汇总 | [results.json](2026-11-10-identity/results.json) |
| 逐局记录 | 同目录 `1v1.csv`、`2v2.csv`、`dual.csv`、`eight.csv`、`identity.csv`、`identity-preferred.csv` |
| 角色名单与阵营 | `roster.tsv` |
| AI 逐角色审计 | `ai-audit.tsv` |
| 生成脚本 | [summarize-current.py](summarize-current.py) |
| 角色权重独立验证 | [summarize-role-tuning.py](summarize-role-tuning.py)，处理现有 `tune-1v1`／`tune-2v2` 测试入口的训练与留出样本 |

## 目录约定

- **一轮采样一个时间目录**：`YYYY-MM-DD-<主题>/`，主题写这一轮改了什么（`arcane`＝奥法五式与法系调整，`ranger`＝游侠调整）。
- 每个发布目录提供完整的六组表格。一般轮次全部重采；本次用户指定仅重采身份局时，允许原样沿用未受影响的1v1、2v2、双将及八人阵营数据，但必须逐模式记录原日期、源码提交与指纹，明确标注“沿用旧样本，未重测”，不能冒充本轮新样本。
- 新的一轮提交后，**旧目录在同一个提交里删除**。历史交给 git：`git log -- Tools/Statistics` 能翻回去，不需要留 5 份 CSV 占 30MB。
- 跨轮对比写成一张表放进当轮 README 的「与上一轮相比」小节，不要靠保留旧目录来做对比。

## 读取口径

- 胜率＝胜场／全部出场，平局不算胜但计入分母。
- 双将子职按组合共同计胜；八人角色阵亡仍按所属阵线计分。
- 固定配桌与随机选角限制了泛化，**不能拿新旧百分点差直接归因于某一次技能修改**。
- 这是固定 AI 的结果，不是真人榜单，也不是理论最优解。

## 复现与补表

完整重采按[现行报告的复现命令](2026-11-10-identity/README.md#复现与证据)逐一运行六组样本，保存对应日志。身份随机组必须先完成，再将身份分表同步为运行时偏好输入，最后采偏好验证组。

只从现有数据补全表格时：

```powershell
python Tools/Statistics/summarize-current.py --date 2026-11-10 --directory 2026-11-10-identity --identity-only --refresh-docs --update-roster
```

日志与逐局 CSV 必须随目录一起提交；生成脚本会核对名单规模、逐角色出场数与异常数。

补表模式还核对已发布汇总，保留原采样提交、日期与源码指纹；同步报告、机器汇总与图鉴中的八人表。阵营整体胜率按每局一次计；身份整体胜率按主公1、忠臣2、反贼4、内奸1的席位数折算，不能把忠臣与主公算成两个独立胜方。

## 权重验证工具

`RevisionBalanceTests` 保留 `tune-1v1`／`tune-2v2` 两个独立权重实验入口。将两组新采的CSV保存到明确指定的实验目录，对应日志保存为同一前缀后，运行：

```powershell
python Tools/Statistics/summarize-role-tuning.py --directory <实验目录> --log-prefix <日志前缀>
```

脚本按训练段选候选，使用独立留出段做配对精确检验与多重比较校正，输出 `tuning.json`。这类实验与正式模式胜率分开统计，不把旧实验当作新采样。当前已采用参数的旧验证数据可从精简前提交 `9947e7b` 的 `Tools/Statistics/2026-11-05-ai/` 取回。

## 统一运行环境

`run-current.ps1 -Run`默认以本机.NET 8.0.15完成编译、功能检查和全部采样，所有模式复用同一程序集；可用`-RuntimeVersion`显式指定已安装版本。版本与程序集SHA256保存于provenance.json。旧.NET 6.0.11在本机长任务中出现过原生崩溃，本轮不会拼接中断样本或跨运行时样本。游戏及Unity编译目标不变。

复现到新目录时显式传入`-Date`与`-Directory`，执行器拒绝覆盖已发布目录；每组前后核对源码，运行时身份偏好表必须来自同轮随机身份组。临时失败样本留在忽略的bin目录，不属于已发布统计。

## 仅重采身份局

游戏、测试、UI及工具修改全部完成后运行下列命令。省略`-Run`只显示计划，不编译或采样。`-Baseline`必须指向待替换现行轮的`results.json`，不是其更早的`baseline-results.json`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Statistics/run-identity.ps1 -Run -Date <新日期> -Directory <新日期>-identity -Baseline Tools/Statistics/2026-11-10-identity/results.json
```

运行器用.NET 8.0.15编译一次，校验AI状态测试，后续审计、两组身份采样与完整功能回归复用同一DLL，并保存程序集SHA256。运行时导出角色表，必须与旧轮名单指纹一致。随机身份固定2,000局、种子5000000–5001999；完成后将`identity-roles.tsv`逐字节同步到游戏运行时偏好表，再以独立种子5100000–5100599采600局偏好身份。没有执行其他四种模式的采样入口。

旧轮四组CSV及日志原样复制，新目录内的`baseline-results.json`保存直接来源汇总。`provenance.json.carried_forward`保存来源汇总和每个沿用文件的SHA256；`results.json.mode_provenance`保存逐模式状态、原采样日期、源码提交及源码／测试指纹。汇总器核对沿用文件指纹、总量、逐角色分母与胜负记录，并区分2,600局新采样和合并表的总量。旧轮AI审计不沿用，新审计与回归另有日志。发布新目录时仍在同一提交删除旧目录，保留一份现行汇总。

重新生成报告和图鉴表格（不会重采，保持原来源）：

```powershell
python Tools/Statistics/summarize-current.py --date 2026-11-10 --directory 2026-11-10-identity --identity-only --refresh-docs --update-roster
```

工具校验：`python -m unittest discover -s Tools/Statistics -p test_statistics_tools.py`。测试在忽略的bin目录使用夹具，验证来源保留、刷新幂等及沿用文件篡改拒绝，不发布或宣称新采样结果。
