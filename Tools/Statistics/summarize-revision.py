"""Summarize the reproducible 55-subclass revision sample; never reuse old outcome rows."""
from pathlib import Path
import csv,json,hashlib,re,math
root=Path(__file__).resolve().parents[2]
out=root/'Tools/Statistics/2026-10-30'
export=root/'Art/Integration/monks-portents-rebalance/runtime-roles.tsv'
text=export.read_text(encoding='utf-8-sig').replace('\\t','\t')
roles=list(csv.DictReader(text.splitlines(),delimiter='\t'))
assert len(roles)==55 and len({r['id'] for r in roles})==55
by_id={r['id']:r for r in roles}
totals={};counts={}
expected={'1v1':29700,'2v2':23760,'dual':3816,'eight':600,'identity':600}
for mode,n in expected.items():
 path=out/(mode+'.csv');rows=list(csv.DictReader(path.open(encoding='utf-8-sig')))
 log=(root/f'Tools/HeadlessTests/bin/revision-{mode}.log').read_text(encoding='utf-8-sig')
 assert len(rows)==n and '通过 1 / 失败 0' in log and 'zero errors / forced aborts' in log,mode
 stats={r['id']:{'games':0,'wins':0,'draws':0} for r in roles}
 assert len({int(row['seed']) for row in rows})==n,mode
 for row in rows:
  winner=int(row['winner']);assert winner in (-1,0,1)
  for side in (0,1):
   ids=row['first' if side==0 else 'second'].split('+')
   if ids==['']:continue
   for role in ids:
    assert role in stats,(mode,role)
    stat=stats[role];stat['games']+=1;stat['wins']+=int(winner==side);stat['draws']+=int(winner==-1)
 for v in stats.values():
  v['win_percent']=round(100*v['wins']/v['games'],3) if v['games'] else None
  # Normal approximation is descriptive; correlated team samples are not independent trials.
 if mode in ('1v1','2v2'):assert {s['games'] for s in stats.values()}=={1080 if mode=='1v1' else 1728}
 counts[mode]=stats
 totals[mode]={'games':n,'draws':sum(int(r['winner'])==-1 for r in rows),'errors':0,'forced_aborts':0,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
source=hashlib.sha256()
for p in sorted((root/'Assets/Scripts').rglob('*.cs')):
 source.update(str(p.relative_to(root)).replace('\\','/').encode());source.update(p.read_bytes())
report={'date':'2026-10-30','roster_count':55,'law_axis_counts':[18,18,19],'legal_duals':477,'source_sha256':source.hexdigest(),'seeded_drunk_rng':True,'totals':totals,'roles':[{**r,'modes':{m:counts[m][r['id']] for m in counts}} for r in roles],
 'limitations':['固定AI策略采样，不代表真人胜率或最优权重。','2v2为固定队伍偏移配桌；双将为477组合固定偏移抽样，并非所有组合两两循环。','八人两表各600局，角色出场不均衡，不宜比较小差异。','本轮未重做集火None/Self/Mate或自身/针对权重的独立留出寻优。']}
(out/'results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# 55子职重新采样（2026-10-30）','','采用最终武僧、预兆替换、持续抗性、猎人追加与狂野逐段类型规则；与旧52角色快照分开。总计 **58,476局**，异常／模拟器强停均为0。普通50轮按正式规则结算，真正平局计入分母。','','| 模式 | 局数 | 抽样方法 |','|---|---:|---|','| 1v1 | 29,700 | 1,485个不同角色配对各20局，双方各占一半物理先手；每角色1,080次出场 |','| 2v2 | 23,760 | 1,485支双角色队伍，每队与固定偏移742的队伍配16局；每角色1,728次出场 |','| 双将 | 3,816 | 477种合法组合各与固定偏移238的组合配8局；每组合16次出场，双方先手均衡 |','| 八人阵营 | 600 | 单将3/3/2，角色与阵营轴一致；每类阵营各担任2人方200次 |','| 八人身份 | 600 | 随机8个不同子职与隐藏身份，按身份阵线记录胜者；不是偏好选角实验 |','','1v1和2v2按胜场／全部出场计，平局不算胜。双将角色值按其组合的结果共同计胜；八人角色阵亡仍按所属阵线计分。固定配桌、共享队友与随机选角限制了泛化，不与旧快照直接作技能强弱因果比较。','','## 全角色胜率','','| 角色 | 1v1 | 2v2 | 双将 |','|---|---:|---:|---:|']
for role in roles:
 values=[f"{counts[m][role['id']]['win_percent']:.1f}%" for m in ('1v1','2v2','dual')]
 lines.append('| '+role['name']+' | '+' | '.join(values)+' |')
lines+=['','## 复现与证据','','使用 `Assets/Tests/EditMode/RevisionBalanceTests.cs`，每次通过真实BoardController驱动。1v1／2v2／双将复用已校正胜者归属的测试台；风暴、狂野、醉拳、状态、赌博、魅惑和幽灵AI均固定种子。每个CSV保留种子、双方角色、胜者和行动数；JSON记录逐角色分母、平局、源码及CSV的SHA256。','','```powershell',"$env:FDD_REVISION_MODE='1v1'  # 依次换为2v2、dual、eight、identity",'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Only RevisionBalanceTests -OutputDirectory Tools/HeadlessTests/bin/revision-sample',"# 每种模式日志保存到 Tools/HeadlessTests/bin/revision-<mode>.log",'python Tools/Statistics/summarize-revision.py','```','','旧的2026-10-27七类大规模快照仍保留在上级目录，用作历史记录。本轮替代其作为README当前规则的胜率入口；未声称重跑了全部双将交叉赛、偏好身份选角或集火寻优。']
(out/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('PASS: 58,476 outcomes; 55 complete role rows; no errors or forced aborts.')
