from pathlib import Path
import csv,re
root=Path(__file__).resolve().parents[2]
rows=list(csv.DictReader((root/'Tools/AiTuning/results.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
assert len(rows)==100 and len({(r['mode'],r['id']) for r in rows})==100
accepted=[r for r in rows if r['accepted']=='True']
code='''using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    /// <summary>由独立留出实验生成，只采用多重比较校正后仍改善的角色／模式配置。</summary>
    public static class ValidatedAiTuning
    {
        public static void Apply(GreedyChoiceProvider ai,GameManager game,PlayerState actor)
        {
            ai.HandValueWeight=12;ai.DenyRatioPercent=43;ai.UseProjectedCounterplay=false;
            if(game==null||actor==null||actor.Character is ICharacterBehaviorContainer||actor.IsSummon)return;
'''
for mode,count in [('1v1',2),('2v2',4)]:
    selected=[r for r in accepted if r['mode']==mode]
    if not selected:continue
    code+=f'            if(game.Players.Count=={count})switch(actor.Definition.Id)\n            {{\n'
    for r in selected:
        m=re.match(r'projected(\d+)_(\d+)',r['candidate']);assert m
        code+=f'                case "{r["id"]}":ai.HandValueWeight={m[1]};ai.DenyRatioPercent={m[2]};ai.UseProjectedCounterplay=true;break;\n'
    code+='            }\n'
code+='        }\n    }\n}\n'
(root/'Assets/Scripts/Assistant/ValidatedAiTuning.cs').write_bytes(b'\xef\xbb\xbf'+code.replace('\n','\r\n').encode())
games=sum(int(r['games']) for r in rows)
s=f'''# 50角色 AI 权重对照（2026-10-26）

共{games:,}场实际 BoardController 对局，逐一覆盖50角色×1v1／2v2。每格10种配置各128场训练，选出训练最高者后，另用320个固定种子对照基线；训练与留出种子独立，同一对照共享阵容、座位、起手与各随机源。先修复运算后手值读取、借用队友数字误罚和队友技能选择；这些修复为两组共有。

起手从真实1d20合法拆分中按既有 DiceSplitAdvisor 选取，不自动生成护盾；受试者轮换座位，其余AI固定原权重，启用现有集火与安全连锁。50轮按正式总生命规则判胜，胜1分、平0.5分、负0分。该实验不包含真人操作、8人身份、双将或所有AI同时升级的强度保证。

候选：原策略12/43；新机会评分的12/0、12/25、12/43、12/67、12/100、8/43、16/43、8/67、16/25。前一个数是自身手值偏好的权重，后一个数是敌方机会评分的百分比；两项量纲不同，**不是整套策略的百分比分配**。签名技能、斩杀、治疗、距离、集火另行评分。新评分预览运算后两只手，枚举全部已知敌方可动手与对应运算结果、当前状态、射程及组合奖励，不修改局面。

采用门槛：独立留出集平均提升，前后160场均提升，配对标准误计算的单侧z≥3.30，按100项比较作保守Bonferroni筛选。当前采用{sum(r['mode']=='1v1' for r in accepted)}项1v1、{sum(r['mode']=='2v2' for r in accepted)}项2v2；其余保留基线。未达门槛不等于没有改善空间。实测只在本固定对手分布下成立，2v2支持技能仍使用独立协作判断。

## 逐角色结果

| 模式 | 角色 | 训练最佳 | 留出原分率 | 留出候选分率 | 差值（百分点） | z | 采用 |
|---|---|---|---:|---:|---:|---:|---|
'''
for r in rows:s+=f'| {r["mode"]} | {r["name"]} | {r["candidate"]} | {100*float(r["hold_baseline"]):.2f}% | {100*float(r["hold_candidate"]):.2f}% | {100*float(r["delta"]):+.2f} | {float(r["z"]):.2f} | {"是" if r["accepted"]=="True" else "否"} |\n'
s+='''
## 复现

先运行 `Tools/HeadlessTests/run-tests.ps1 -Only ArcaneFiveTests` 生成最新逻辑程序集；再运行 `Tools/AiTuning/run.ps1 -Train 128 -Holdout 320`。程序复制程序集到自己的bin，不覆盖测试DLL。`results.tsv`保存100格原始统计，再用 `export_profiles.py`生成受验证的生产配置与本页。运行期间不要更改候选集合、规则或随机种子；参数改变需重新做训练与留出验证。

生产入口 `GameBootstrap.MakeAI` 开启 `UseValidatedTuning`；默认构造保留可显式调参的基线，循环赛测试台明确开启生产配置。8人和双将暂不套用单将1v1验证结果。
'''
(root/'Tools/AiTuning/README.md').write_text(s,encoding='utf-8')
print('games',games,'accepted',len(accepted))
