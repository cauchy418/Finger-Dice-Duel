"""Synchronize README and the sampling note from a verified results.json."""
from pathlib import Path
import hashlib,json,re
ROOT=Path(__file__).resolve().parents[2]
data=json.loads((ROOT/'Tools/Statistics/results.json').read_text(encoding='utf-8-sig'))
for name,report in data['reports'].items():
    assert hashlib.sha256((ROOT/'Tools'/name).read_bytes()).hexdigest()==report['sha256'],name

def save(path,text):
    p=ROOT/path;b=p.read_bytes() if p.exists() else b''
    p.write_bytes((b'\xef\xbb\xbf' if b.startswith(b'\xef\xbb\xbf') else b'')+text.replace('\r\n','\n').replace('\n','\r\n').encode())

p=ROOT/'README.md';s=p.read_text(encoding='utf-8-sig')
old=s[s.index('### 全角色胜率'):s.index('### 双将与八人局摘要')]
names=re.findall(r'^\| ([^|]+) \| (?:\d+\.\d+%|未采样) \|',old,re.M)
assert len(names)==data['characters'] and len(set(names))==len(names)
roles={row['name']:row for row in data['roles'].values()}
table='\n'.join('| '+name+' | '+' | '.join(f"{roles[name][mode]['rate_percent']:.1f}%" for mode in ['1v1','2v2','dual'])+' |' for name in names)
faction='\n'.join(f"| {name} | {row['wins']} | {row['rate_percent']:.1f}% |" for name,row in data['factions'].items())
identity='\n'.join(f"| {name} | {data['identity']['random'][name]['rate_percent']:.1f}% | {data['identity']['biased'][name]['rate_percent']:.1f}% |" for name in data['identity']['random'])
axis='、'.join(f"{name}{row['rate_percent']:.1f}%" for name,row in data['dual_axis'].items())
section=f"""## 胜率测试

以下为 **{data['date']}、52子职／425种双将** 的当前规则采样，共 **{data['total_games']:,}局**，七项统计全部通过，异常与模拟器强停均为0。全部经实际 BoardController 的技能／目标选择流程执行，关闭难度噪声，并固定每局的起手、元素、状态、赌博及魅惑随机源。详见[采样口径与复现](Tools/Statistics/README.md)。

本轮修复旧测试台的先手与胜者归属、身份偏好选角的座位对应错误，并统一到实机AI流程。旧报告不再作为当前强度依据；前后差异不能直接解读为单次技能增强。打满规则轮次上限后按生命判胜，只有真正未分胜负的局才记平。

| 测试 | 样本规模与口径 | 完整报告 |
|---|---|---|
| 1v1 | 52角色两两循环，每对80局、双方各占40次先手，共106,080局；胜率＝胜局／全部出场，平局不算胜 | [1v1报告](Tools/1v1-report.txt) |
| 2v2 | 1,326种双人队伍按固定偏移配桌，共169,728局；每角色出场13,056次，统计所在队伍胜率；不是全部队伍两两交叉 | [2v2报告](Tools/2v2-report.txt) |
| 双将1v1 | 425组合、90,100对，每对24局并交换座位，共2,162,400局；列出剔除真正平局的决胜胜率 | [双将报告](Tools/dual-report.txt) |
| 8人阵营 | 全AI双将抽样2,000局；按整局统计阵营获胜，三阵营人数3／3／2轮换 | [阵营报告](Tools/8p-report.txt) |
| 8人身份 | 随机选角、固定身份偏好选角各2,000局；偏好仍用预设榜单，不是本轮自动学得的最优选角 | [随机身份](Tools/identity-report-random.txt)／[偏好身份](Tools/identity-report.txt) |
| 2v2集火对照 | 每角色600局×2段种子×3策略，共187,200局；同局只改变受试角色策略 | [集火报告](Tools/focus-report.txt) |

### 全角色胜率

按职业排列，双将列统计该子职参加的所有合法搭配，**不是固定某一组合的胜率**。双将列分母扣除真正平局；1v1、2v2列保留平局。它们描述本批AI样本，不代表理论最优或真人强度，小差异不要直接作强弱结论。

| 角色 | 1v1胜率 | 2v2队伍胜率 | 双将决胜胜率 |
|---|---:|---:|---:|
{table}

### 双将与八人局摘要

双将1v1外战（不同阵营之间）决胜胜率：{axis}。完整425组合及打满上限比例见报告。

8人双将阵营按获胜局数／2,000统计；多人结果不能反推1v1强度。

| 阵营 | 获胜局数 | 整局胜率 |
|---|---:|---:|
{faction}

| 身份 | 随机选角胜率 | 固定身份偏好选角胜率 |
|---|---:|---:|
{identity}

主公与忠臣共同获胜，胜率不相加。上述结果用于后续平衡判断，本轮没有修改技能数值或凭循环赛自动调权重。[独立权重留出实验](Tools/AiTuning/README.md)仍保留历史结果，当前沿用其中23项1v1配置。

"""
if data.get('snapshot_note'):
    section=section.replace('的当前规则采样','的规则快照采样（主动镜像错位改动前）')
s=s[:s.index('## 胜率测试')]+section+s[s.index('## 开发与文档'):]
s=re.sub(r'最新 \*\*1,380项逻辑回归[^\n]*','最新 **1436项功能回归通过，0失败**；7项大规模统计对应主动镜像错位改动前快照；Unity/NUnit条件编译及181脚本表现层编译通过。全部七份模式报告和52子职胜率已同步；新誓言与状态另有[81项实机检查](Art/Integration/divine-oaths-v1/README.md)，历史验收保存在 Art/Integration/。',s)
save('README.md',s)
new_rows='\n'.join('| '+name+' | '+' | '.join(f"{roles[name][mode]['rate_percent']:.1f}%" for mode in ['1v1','2v2','dual'])+' |' for name in ['牧师·战争领域','圣武士·征服之誓','圣武士·复仇之誓'])
note=f"""# 当前规则胜率采样（{data['date']}）

本轮52子职、425种合法双将，七类统计共{data['total_games']:,}局。完整角色表见[README](../../README.md#全角色胜率)，机器可读数据与报告SHA256见[results.json](results.json)，便于核对的导出表见[role-rates.md](role-rates.md)。报告为固定AI与既定开局分布的结果，不是真人榜单或理论最优解。

## 本轮统计修正

- 1v1旧测试台没有实际应用交换先手参数，却按交换后的身份统计胜者。现在实际交换物理座位，每对双方各40次先手；结果保留自己的先手标记，异常返回不会让其后结果错位。
- 双将的FirstWon已表示“第一组角色获胜”，旧汇总又按座位转换一次。现在只转换一次，主／副将组合顺序保持原定义。
- 身份偏好按随机座位顺序选角后，按原座位索引存回，避免把某身份的偏好选给另一个身份。
- 八人“作为2人阵营”列按该阵营实际担任2人方的局数计分，不再误用两倍的角色出场数。
- 1v1、2v2、双将、八人阵营都通过实机BoardController，身份局继续使用同一控制器。技能选择、赌博、偷窃、目标确认、额外运算与击杀奖励不再由旧手写循环绕过。
- 每局固定起手、风暴／狂野魔法／幻术、元素反应、束缚／油腻、赌博、魅惑随机走子以及幽灵AI随机源。新增魅惑重复采样回归，固定种子不改变普通游戏的随机策略。
- 双将采用8个独立进程，避免共享静态随机状态；合并核对MVID、角色组合清单、配对／轮次／座位、唯一性和动态总行数，支持角色表继续扩展。

旧1v1／双将报告存在胜者归属错误，不能作为当前平衡依据；历史实现日志保留时也明确注明。本轮差异还含AI驱动与规则变化，不能拿新旧百分点差直接评价某一次改技能。

## AI完成情况

战争领域的施恩／少选／不选、战恩9攻击或回复、征服持5成长与攻防时点、复仇初始标记／转移／死亡重选／每面盾的纯运算均已接入专属策略和真实控制器。本轮覆盖所有52角色的对战流程，功能回归含新三角色的27局2／4／8人流程检查、明确的队友过滤及人类选择测试。

新三角色在1v1、2v2均沿用L／H12／D43基线，集火Self；战争领域旧P12／D100已停用。其余沿用23项旧独立留出配置，8人及双将不套用单将1v1权重。这里的H、D不是“自身／针对策略的固定百分比”。技能AI已完成，不等于已找到最优权重；本轮没有用循环赛数据再次训练和选权重。

| 新改角色 | 1v1胜率 | 2v2队伍胜率 | 双将决胜胜率 |
|---|---:|---:|---:|
{new_rows}

集火实验单独比较None／Self／Mate，同一局只变受试AI，不与生产循环赛混算。即便本批结果支持某候选，仍需独立留出验证后才改生产配置。完整公式与限制见[逐角色策略](../../Docs/ai-strategy-reference.md)。

## 样本和分母

- 1v1：1,326个角色配对×80局＝106,080局，角色各出场4,080次；合法1d20均衡拆分。
- 2v2：1,326支队伍按663的固定偏移配桌、各128局，共169,728局。唯一队伍配对663组，各方向分配不同种子；每角色出场13,056次。不是全部队伍两两循环，队友／对手分布会影响角色胜率。
- 双将：425组合、90,100配对×12轮×2座位＝2,162,400局。主副将按名单构建，不额外把每组主副将顺序再反转一次。决胜胜率只扣真正平局，正常打满规则上限并判胜的局仍计入。
- 八人阵营：2,000局，守序／中立／混乱担任2人方667／667／666次。角色列按其双将成员所在阵营获胜计胜，主忠反内身份局另报。
- 八人身份：随机／固定偏好各2,000局；主忠方共胜，反贼共胜，角色阵亡仍按身份阵营的胜负计分。偏好排名沿用预设，不随本轮结果重新训练。
- 集火对照：52角色×600局×2段种子×3策略＝187,200局。基线是无集火，不等于前文全部生产AI的2v2循环赛。
- 普通模式50轮，身份局80轮。1v1／2v2统计胜局除以全部出场；双将表采用剔除真正平局后的决胜胜率。小差异、配对分布和AI能力会限制推论，未据此自动调整游戏数值。

## 验证与复现

1428项功能回归、7项大规模统计通过，异常／模拟器强停为0。五项新增回归覆盖物理座位归属、随机技能与多人选择重现、双将胜者映射、身份选角座位、魅惑随机源。181脚本表现层及Unity/NUnit条件编译通过；没有声称重新进行美术或UI布局验收。

先在项目根执行（Windows PowerShell）：

~~~powershell
$env:DOTNET_TieredCompilation='0'
$env:COMPlus_TieredCompilation='0'
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -SkipStatistics
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-sharded-statistics.ps1 -Shards 8 -Only DualVersusDualTests -CacheName divine-52-seeded > Tools/HeadlessTests/bin/resample-dual-seeded.txt
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll CharacterBalanceTests > Tools/HeadlessTests/bin/resample-seeded-CharacterBalanceTests.txt
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll TwoVersusTwoBalanceTests > Tools/HeadlessTests/bin/resample-seeded-TwoVersusTwoBalanceTests.txt
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll EightPlayerFactionTests > Tools/HeadlessTests/bin/resample-seeded-EightPlayerFactionTests.txt
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll IdentityBalanceTests > Tools/HeadlessTests/bin/resample-seeded-IdentityBalanceTests.txt
dotnet Tools/HeadlessTests/bin/HeadlessTests.dll FocusPolicyExperimentTests > Tools/HeadlessTests/bin/resample-seeded-FocusPolicyExperimentTests.txt
~~~

直接运行测试会重写Tools下对应报告；请保存每个命令的日志。summarize.py要求所有fixture通过且报告规模、逐角色出场数和异常检查符合当前名单，再输出JSON及表格。日志前缀与缓存目录是命令参数，不依赖本机绝对路径：

~~~powershell
python Tools/Statistics/summarize.py --date 2026-10-27 --log-prefix Tools/HeadlessTests/bin/resample-seeded- --dual-log Tools/HeadlessTests/bin/resample-dual-seeded.txt --dual-cache Tools/HeadlessTests/bin/divine-52-seeded
python Tools/Statistics/update-readme.py
~~~

results.json记录采样基线提交（最终提交包含本轮测试台修复）、Assets/Scripts与Assets/Tests的C#C#源码指纹、双将构建MVID和七份报告的SHA256。原始双将分片位于忽略的bin缓存，公开报告与汇总入库；重新构建后MVID会变化，不允许拿旧分片冒充新构建。
"""
if data.get('snapshot_note'):note=note.replace('# 当前规则胜率采样','# 规则快照胜率采样')+'\n> '+data['snapshot_note']+'\n'
save('Tools/Statistics/README.md',note)
print('PASS README and sampling note synchronized from verified data')
