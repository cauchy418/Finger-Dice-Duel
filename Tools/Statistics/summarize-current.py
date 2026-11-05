"""Validate fresh seeded sampling against the runtime-exported roster."""
from pathlib import Path
import argparse, collections, csv, hashlib, json, subprocess

parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--date', default='2026-11-05')
parser.add_argument('--directory', help='Snapshot directory name, relative to Tools/Statistics')
parser.add_argument('--log-prefix', default='Tools/HeadlessTests/bin/druid-57-')
parser.add_argument('--optimized-ai', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
out = root / 'Tools/Statistics' / (args.directory or args.date)
roles = list(csv.DictReader((out / 'roster.tsv').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
ids = {r['id'] for r in roles}
assert len(ids) == len(roles) and len(roles) > 1
axes = collections.Counter(r['axis'] for r in roles)
n = len(roles)
pairs = n * (n - 1) // 2
duals = sum(k * (k - 1) // 2 for k in axes.values())
expected = {'1v1': pairs * 20, '2v2': pairs * 16, 'dual': duals * 8, 'eight': 600, 'identity': 600}
totals, counts = {}, {}
for mode, size in expected.items():
    path = out / (mode + '.csv')
    rows = list(csv.DictReader(path.open(encoding='utf-8-sig')))
    log = (root / (args.log_prefix + mode + '.log')).read_text(encoding='utf-8-sig')
    assert len(rows) == size and '通过 1 / 失败 0' in log and 'zero errors / forced aborts' in log, mode
    assert len({int(row['seed']) for row in rows}) == size, mode
    stats = {r['id']: {'games': 0, 'wins': 0, 'draws': 0} for r in roles}
    for row in rows:
        winner = int(row['winner'])
        assert winner in (-1, 0, 1) and int(row['turns']) >= 0
        for side, column in enumerate(('first', 'second')):
            for role in filter(None, row[column].split('+')):
                assert role in ids, (mode, role)
                stat = stats[role]
                stat['games'] += 1
                stat['wins'] += int(winner == side)
                stat['draws'] += int(winner == -1)
    if mode in ('1v1', '2v2'):
        assert {s['games'] for s in stats.values()} == {(n - 1) * (20 if mode == '1v1' else 32)}
    if mode == '1v1':
        match_counts = collections.Counter((r['first'], r['second']) for r in rows)
        assert len(match_counts) == pairs and set(match_counts.values()) == {20}
    elif mode in ('2v2', 'dual'):
        first_counts = collections.Counter(r['first'] for r in rows)
        assert len(first_counts) == (pairs if mode == '2v2' else duals)
        assert set(first_counts.values()) == {16 if mode == '2v2' else 8}
    for stat in stats.values():
        stat['losses'] = stat['games'] - stat['wins'] - stat['draws']
        stat['win_percent'] = round(100 * stat['wins'] / stat['games'], 3) if stat['games'] else None
    counts[mode] = stats
    totals[mode] = {'games': size, 'draws': sum(int(r['winner']) == -1 for r in rows), 'errors': 0,
                    'forced_aborts': 0, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    (out / (mode + '.log')).write_text(log, encoding='utf-8')

def source_hash(folder):
    digest = hashlib.sha256()
    for path in sorted((root / folder).rglob('*.cs')):
        digest.update(path.relative_to(root).as_posix().encode())
        digest.update(path.read_bytes())
    return digest.hexdigest()

report = {'date': args.date, 'roster_count': n, 'law_axis_counts': [axes[a] for a in ('守序', '中立', '混乱')],
          'legal_duals': duals, 'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
          'source_sha256': source_hash('Assets/Scripts'), 'test_source_sha256': source_hash('Assets/Tests'),
          'roster_sha256': hashlib.sha256((out / 'roster.tsv').read_bytes()).hexdigest(),
          'seeded_drunk_rng': True, 'totals': totals,
          'roles': [{**r, 'modes': {m: counts[m][r['id']] for m in counts}} for r in roles],
          'limitations': ['固定AI策略采样，不代表真人胜率或最优权重。',
                          f'2v2为固定队伍偏移配桌；双将为{duals}组合固定偏移抽样，并非全部组合两两循环。',
                          '八人两表各600局，角色出场不均衡，不宜比较小差异。',
                          '权重采用独立留出验证，五模式结果反映最终配置。' if args.optimized_ai
                          else '本轮只重新测量现有AI，没有调整角色数值或进行自身／针对权重的独立留出寻优。']}
(out / 'results.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
total = sum(expected.values())
description = '采用德鲁伊和幽影魔法新版专属策略、对策及通过留出验证的权重' if args.optimized_ai else '采用德鲁伊重构和孢子结社接入后的角色规则'
lines = [f'# {n}子职胜率重新采样（{args.date}）', '',
         f'{description}，重新采样 **{total:,}局**。完整样本中的对局异常和模拟器强停均为0。正式轮数上限内的平局计入分母。', '',
         '| 模式 | 局数 | 抽样方法 |', '|---|---:|---|',
         f'| 1v1 | {expected["1v1"]:,} | {pairs:,}个不同子职配对各20局，双方各占一半物理先手；每角色{(n-1)*20:,}次出场 |',
         f'| 2v2 | {expected["2v2"]:,} | {pairs:,}支双角色队伍，与固定偏移{pairs//2}的队伍配16局；每角色{(n-1)*32:,}次出场 |',
         f'| 双将 | {expected["dual"]:,} | {duals}种合法组合，与固定偏移{duals//2}的组合配8局；每组合16次出场，双方先手均衡 |',
         '| 八人阵营 | 600 | 单将3/3/2，角色与阵营轴一致；每类阵营各担任2人方200次 |',
         '| 八人身份 | 600 | 随机8个不同子职与隐藏身份，按身份阵线记录胜者 |', '',
         '胜率均为胜场／全部出场，平局不算胜。双将子职按其组合共同计胜；八人角色阵亡仍按所属阵线计分。固定配桌和随机选角限制了泛化；旧数据与本次角色池及规则不同，百分点差不能直接归因于某次技能修改。', '',
         '## 平局与异常', '', '| 模式 | 平局 | 异常 | 模拟器强停 |', '|---|---:|---:|---:|']
for mode, stat in totals.items():
    lines.append(f'| {mode} | {stat["draws"]} | 0 | 0 |')
lines += ['', '## 全角色胜率', '', '| 角色 | 1v1 | 2v2 | 双将 |', '|---|---:|---:|---:|']
for role in roles:
    values = [f'{counts[m][role["id"]]["win_percent"]:.1f}%' for m in ('1v1', '2v2', 'dual')]
    lines.append('| ' + role['name'] + ' | ' + ' | '.join(values) + ' |')
lines += ['', '## 复现与证据', '',
          '所有模式使用真实BoardController驱动，复用已校正胜者归属的测试台。固定起手、技能及AI随机源，每个CSV保存逐局种子、角色、胜者和行动数。roster.tsv由运行时角色表直接导出；results.json保存所有五模式逐角色胜／负／平与分母、源码及CSV指纹；五份日志均须通过才能汇总。', '',
          '```powershell', "$env:DOTNET_PROCESSOR_COUNT='1'", "$env:DOTNET_TieredCompilation='0'", "$env:COMPlus_TieredCompilation='0'",
          f"$env:FDD_REVISION_ROOT='Tools/Statistics/{out.name}'",
          "$env:FDD_REVISION_MODE='1v1'  # 依次换为2v2、dual、eight、identity",
          'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Only RevisionBalanceTests -OutputDirectory Tools/HeadlessTests/bin/druid-57-sample',
          '# 各模式日志保存到 Tools/HeadlessTests/bin/druid-57-<mode>.log',
          f'python Tools/Statistics/summarize-current.py --date {args.date} --directory {out.name} --log-prefix {args.log_prefix}'
          + (' --optimized-ai' if args.optimized_ai else ''), '```']
if args.optimized_ai:
    lines += ['', '策略、配对权重实验与前后对照见[优化记录](ai-tuning.md)。']
else:
    lines += ['', '执行记录：首次2v2测试进程发生原生中断，随后从首局完整重跑25,536局并通过。完整功能回归1611项通过、0失败，日志见regression.log。']
(out / 'README.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(f'PASS: {total:,} outcomes; {n} complete role rows; no errors or forced aborts.')
