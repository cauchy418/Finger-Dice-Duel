"""Validate fresh seeded sampling against the runtime-exported roster."""
from pathlib import Path
import argparse, collections, csv, hashlib, json, subprocess
from statistics_common import comparison, verify, source_hash

parser = argparse.ArgumentParser(__doc__)
parser.add_argument('--date', default='2026-11-09')
parser.add_argument('--directory', help='Snapshot directory name, relative to Tools/Statistics')
parser.add_argument('--log-prefix', default='Tools/HeadlessTests/bin/druid-57-')
parser.add_argument('--optimized-ai', action='store_true')
parser.add_argument('--balance', action='store_true')
parser.add_argument('--ranger', action='store_true')
parser.add_argument('--arcane', action='store_true')
parser.add_argument('--state-ai', action='store_true', help='Current state/AI round; requires captured provenance and 58 roles / 532 duals')
parser.add_argument('--identity-only', action='store_true', help='Resample identity modes; validate unchanged modes against the explicitly carried baseline')
parser.add_argument('--baseline', type=Path, help='Previous results.json to embed as a comparison in the current report')
parser.add_argument('--refresh-docs', action='store_true',
                    help='Validate published data and preserve its original sampling provenance')
parser.add_argument('--update-roster', action='store_true', help='Synchronize the eight-player tables in Docs/roster.md')
args = parser.parse_args()
if args.identity_only:
    args.state_ai = True
full_identity = args.arcane or args.state_ai
root = Path(__file__).resolve().parents[2]
out = root / 'Tools/Statistics' / (args.directory or (args.date + '-state-ai' if args.state_ai else args.date))
if args.refresh_docs and json.loads((out / 'results.json').read_text(encoding='utf-8')).get('sampling_scope') == 'identity-only':
    args.identity_only = args.state_ai = full_identity = True
roles = list(csv.DictReader((out / 'roster.tsv').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
ids = {r['id'] for r in roles}
assert len(ids) == len(roles) and len(roles) > 1
axes = collections.Counter(r['axis'] for r in roles)
n = len(roles)
pairs = n * (n - 1) // 2
duals = sum(k * (k - 1) // 2 for k in axes.values())
if args.state_ai:
    assert (n, duals) == (58, 532), (n, duals)
    assert args.baseline or args.refresh_docs, '--state-ai requires --baseline'
expected = {'1v1': pairs * 20, '2v2': pairs * 16, 'dual': duals * 8, 'eight': 600, 'identity': 600}
if full_identity:
    expected.update({'identity': 2000, 'identity-preferred': 600})
totals, counts, mode_rows, identity_roles = {}, {}, {}, {}
for mode, size in expected.items():
    path = out / (mode + '.csv')
    rows = list(csv.DictReader(path.open(encoding='utf-8-sig')))
    log_path = out / (mode + '.log') if args.refresh_docs or args.state_ai else root / (args.log_prefix + mode + '.log')
    log = log_path.read_text(encoding='utf-8-sig')
    mode_rows[mode] = rows
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
    if full_identity and mode.startswith('identity'):
        by_role_path = out / (mode + '-roles.tsv')
        role_rows = list(csv.DictReader(by_role_path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        identity_roles[mode] = role_rows
        seat_totals = {'Lord': 1, 'Loyalist': 2, 'Rebel': 4, 'Traitor': 1}
        assert len({(r['role'], r['id']) for r in role_rows}) == len(role_rows)
        for role, seats in seat_totals.items():
            entries = [r for r in role_rows if r['role'] == role]
            assert sum(int(r['games']) for r in entries) == size * seats
            assert all(r['id'] in ids and 0 <= int(r['wins']) <= int(r['games']) for r in entries)
        if mode == 'identity':
            assert by_role_path.read_bytes() == (root / 'Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt').read_bytes()
        for row in rows:
            picks = [i for col in ('first', 'second') for i in row[col].split('+') if i]
            assert len(picks) == len(set(picks)) == 8
        for role_id, stat in stats.items():
            entries = [r for r in role_rows if r['id'] == role_id]
            assert sum(int(r['games']) for r in entries) == stat['games']
            assert sum(int(r['wins']) for r in entries) == stat['wins']
    for stat in stats.values():
        stat['losses'] = stat['games'] - stat['wins'] - stat['draws']
        stat['win_percent'] = round(100 * stat['wins'] / stat['games'], 3) if stat['games'] else None
    counts[mode] = stats
    totals[mode] = {'games': size, 'draws': sum(int(r['winner']) == -1 for r in rows), 'errors': 0,
                    'forced_aborts': 0, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    if not args.refresh_docs and log_path != out / (mode + '.log'):
        (out / (mode + '.log')).write_text(log, encoding='utf-8')

def rate(wins, games):
    return {'wins': wins, 'games': games, 'win_percent': round(100 * wins / games, 3) if games else None}

# 阵营按每局一次计分，不能按存活人数或三个角色各算一场。
axis_by_id = {r['id']: r['axis'] for r in roles}
factions = {a: {size: {'wins': 0, 'games': 0} for size in (2, 3)} for a in axes}
for row in mode_rows['eight']:
    picks = [i for col in ('first', 'second') for i in row[col].split('+') if i]
    teams = collections.Counter(axis_by_id[i] for i in picks)
    assert len(picks) == len(set(picks)) == 8 and sorted(teams.values()) == [2, 3, 3]
    winner = None
    if int(row['winner']) != -1:
        assert int(row['winner']) == 0 and row['first']
        winners = row['first'].split('+')
        winner = axis_by_id[winners[0]]
        assert set(winners) == {i for i in picks if axis_by_id[i] == winner}
    for axis, size in teams.items():
        factions[axis][size]['games'] += 1
        factions[axis][size]['wins'] += int(axis == winner)
faction_summary = {}
for axis, sizes in factions.items():
    assert sizes[2]['games'] == 200 and sizes[3]['games'] == 400
    faction_summary[axis] = {'overall': rate(sum(s['wins'] for s in sizes.values()), 600),
                             'two_seats': rate(**sizes[2]), 'three_seats': rate(**sizes[3])}
assert sum(s['overall']['wins'] for s in faction_summary.values()) + totals['eight']['draws'] == 600

identity_summary = {}
for mode, records in identity_roles.items():
    summary = {}
    for role, seats in seat_totals.items():
        entries = [r for r in records if r['role'] == role]
        wins = sum(int(r['wins']) for r in entries)
        games = sum(int(r['games']) for r in entries)
        assert wins % seats == games % seats == 0
        summary[role] = rate(wins // seats, games // seats)
    assert summary['Lord'] == summary['Loyalist']
    assert sum(summary[r]['wins'] for r in ('Lord', 'Rebel', 'Traitor')) + totals[mode]['draws'] == expected[mode]
    identity_summary[mode] = summary

report = {'date': args.date, 'roster_count': n, 'law_axis_counts': [axes[a] for a in ('守序', '中立', '混乱')],
          'legal_duals': duals, 'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
          'source_sha256': source_hash(root, 'Assets/Scripts'), 'test_source_sha256': source_hash(root, 'Assets/Tests'),
          'roster_sha256': hashlib.sha256((out / 'roster.tsv').read_bytes()).hexdigest(),
          'seeded_drunk_rng': True, 'totals': totals,
          'roles': [{**r, 'modes': {m: counts[m][r['id']] for m in counts}} for r in roles],
          'limitations': ['固定AI策略采样，不代表真人胜率或最优权重。',
                          f'2v2为固定队伍偏移配桌；双将为{duals}组合固定偏移抽样，并非全部组合两两循环。',
                          '八人身份偏好以独立的2,000局随机身份样本拟合，另用600局验证实际候选偏好。角色出场不均衡。' if full_identity
                          else '八人两表各600局，角色出场不均衡，不宜比较小差异。',
                          f'反映本轮状态AI和角色规则更新，并复查{n}子职策略和开局对策。' if args.state_ai
                          else '反映本轮九个子职的规则和AI更新，并复查57子职策略和开局对策。' if args.arcane
                          else '五模式反映游侠职业调整及相关策略和对策更新后的配置。' if args.ranger
                          else '五模式反映八子职调整和全角色策略复查后的配置。' if args.balance
                          else '权重采用独立留出验证，五模式结果反映最终配置。' if args.optimized_ai
                          else '本轮只重新测量现有AI，没有调整角色数值或进行自身／针对权重的独立留出寻优。']}
if args.state_ai and not args.refresh_docs:
    provenance = json.loads((out / 'provenance.json').read_text(encoding='utf-8'))
    verify(root, provenance)
    report.update(provenance)
    report['identity_preference_sha256'] = hashlib.sha256((out / 'identity-roles.tsv').read_bytes()).hexdigest()
    audit = list(csv.DictReader((out / 'ai-audit.tsv').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    assert len(audit) == n and {r['id'] for r in audit} == ids
    audit_log = (out / 'audit.log').read_text(encoding='utf-8-sig')
    assert f'{n} roles, {n*n*3} legal opening decisions' in audit_log and '通过 1 / 失败 0' in audit_log
    regression_log = (out / 'regression.log').read_text(encoding='utf-8-sig')
    assert '失败 0' in regression_log and '通过 0 /' not in regression_log
    report['ai_audit'] = {'roles': n, 'legal_opening_checks': n*n*3,
                          'sha256': hashlib.sha256((out / 'ai-audit.tsv').read_bytes()).hexdigest()}
if args.identity_only:
    provenance = json.loads((out / 'provenance.json').read_text(encoding='utf-8'))
    carry = provenance['carried_forward']
    baseline_path = out / 'baseline-results.json'
    assert hashlib.sha256(baseline_path.read_bytes()).hexdigest() == carry['baseline_sha256']
    baseline_data = json.loads(baseline_path.read_text(encoding='utf-8'))
    assert baseline_data['roster_sha256'] == report['roster_sha256']
    prior_roles = {r['id']: r for r in baseline_data['roles']}
    report['sampling_scope'] = 'identity-only'
    report['newly_sampled_games'] = 2600
    report['mode_provenance'] = {}
    for mode in expected:
        fresh = mode.startswith('identity')
        if not fresh:
            assert totals[mode] == baseline_data['totals'][mode], mode
            assert all(counts[mode][r['id']] == prior_roles[r['id']]['modes'][mode] for r in roles), mode
            for suffix in ('.csv', '.log'):
                name = mode + suffix
                assert hashlib.sha256((out / name).read_bytes()).hexdigest() == carry['files'][name], name
        original = baseline_data.get('mode_provenance', {}).get(mode, baseline_data)
        report['mode_provenance'][mode] = {
            'status': 'newly_sampled' if fresh else 'carried_forward',
            'date': args.date if fresh else original['date'],
            'source_commit': report['source_commit'] if fresh else original['source_commit'],
            'source_sha256': report['source_sha256'] if fresh else original['source_sha256'],
            'test_source_sha256': report['test_source_sha256'] if fresh else original['test_source_sha256'],
            'sampler_assembly_sha256': (report if fresh else original).get('sampler_assembly_sha256'),
            'sampler_runtime_version': (report if fresh else original).get('sampler_runtime_version'),
            'csv_sha256': totals[mode]['sha256'],
        }
    for mode, base_seed in (('identity', 5000000), ('identity-preferred', 5100000)):
        assert [int(row['seed']) for row in mode_rows[mode]] == list(range(base_seed, base_seed + expected[mode]))
    report['limitations'][-1] = '本轮仅重采移除忠臣替死后的身份随机及偏好组；1v1、2v2、双将和八人阵营沿用旧来源，不代表本次代码的重新测量。'
if args.refresh_docs:
    published = json.loads((out / 'results.json').read_text(encoding='utf-8'))
    assert published['date'] == args.date and published['roles'] == report['roles']
    assert published['totals'] == totals and published['roster_sha256'] == report['roster_sha256']
    report = published  # 补表不能把当前提交或源码指纹冒充为原始采样版本。
report['eight_player_summary'] = {'factions': faction_summary, 'identities': identity_summary}
if args.baseline:
    baseline = json.loads(args.baseline.read_text(encoding='utf-8'))
    report['previous_round_comparison'] = comparison(report, baseline)
    report['previous_round_comparison']['baseline_sha256'] = hashlib.sha256(args.baseline.read_bytes()).hexdigest()
if args.identity_only:
    previous_identities = baseline_data['eight_player_summary']['identities']
    report['identity_aggregate_comparison'] = {
        mode: {role: {
            'previous': previous_identities[mode][role], 'current': stat,
            'delta_percentage_points': round(stat['win_percent'] - previous_identities[mode][role]['win_percent'], 3),
        } for role, stat in identity_summary[mode].items()}
        for mode in identity_summary
    }
(out / 'results.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
total = sum(expected.values())
description = ('采用最新状态AI与角色规则，并复查全角色策略和对策' if args.state_ai else
               '采用新版龙族、风暴、奉献、战争及法师技能，并更新防护学派和相关AI' if args.arcane
               else '采用新版游侠面板、技能和相关AI策略' if args.ranger else
               '采用八子职平衡调整和全角色AI复查后的配置' if args.balance else
               '采用德鲁伊和幽影魔法新版专属策略、对策及通过留出验证的权重' if args.optimized_ai
               else '采用德鲁伊重构和孢子结社接入后的角色规则')
lines = [f'# {n}子职胜率重新采样（{args.date}）', '',
         f'{description}，重新采样 **{total:,}局**。完整样本中的对局异常和模拟器强停均为0。正式轮数上限内的平局计入分母。', '',
         '| 模式 | 局数 | 抽样方法 |', '|---|---:|---|',
         f'| 1v1 | {expected["1v1"]:,} | {pairs:,}个不同子职配对各20局，双方各占一半物理先手；每角色{(n-1)*20:,}次出场 |',
         f'| 2v2 | {expected["2v2"]:,} | {pairs:,}支双角色队伍，与固定偏移{pairs//2}的队伍配16局；每角色{(n-1)*32:,}次出场 |',
         f'| 双将 | {expected["dual"]:,} | {duals}种合法组合，与固定偏移{duals//2}的组合配8局；每组合16次出场，双方先手均衡 |',
         '| 八人阵营 | 600 | 单将3/3/2，角色与阵营轴一致；每类阵营各担任2人方200次 |',
         *(['| 八人身份（随机） | 2,000 | 主公随机8候选，其余各6；从各自候选均匀选一名 |',
            '| 八人身份（偏好） | 600 | 独立种子，同样随机发牌，再按身份内的平滑胜率加权 |']
           if full_identity else ['| 八人身份 | 600 | 随机8个不同子职与隐藏身份，按身份阵线记录胜者 |']), '',
         '胜率均为胜场／全部出场，平局不算胜。双将子职按其组合共同计胜；八人角色阵亡仍按所属阵线计分。固定配桌和随机选角限制了泛化；旧数据与本次角色池及规则不同，百分点差不能直接归因于某次技能修改。', '',
         '## 平局与异常', '', '| 模式 | 平局 | 异常 | 模拟器强停 |', '|---|---:|---:|---:|']
for mode, stat in totals.items():
    lines.append(f'| {mode} | {stat["draws"]} | 0 | 0 |')
lines += ['', '## 全角色胜率', '', '| 角色 | 1v1 | 2v2 | 双将 |', '|---|---:|---:|---:|']
for role in roles:
    values = [f'{counts[m][role["id"]]["win_percent"]:.1f}%' for m in ('1v1', '2v2', 'dual')]
    lines.append('| ' + role['name'] + ' | ' + ' | '.join(values) + ' |')

def cell(stat):
    return f'{stat["win_percent"]:.1f}%（{stat["wins"]}/{stat["games"]}）' if stat['games'] else '—（0场）'

def eight_tables(level):
    heading = '#' * level
    tables = ['', f'{heading} 八人阵营局：角色胜率', '',
              '角色按所属阵营共同计胜，阵亡仍计胜；下列括号为胜场／出场。每个角色的出场数不同。', '',
              '| 角色 | 阵营 | 阵营局胜率（胜／场） |', '|---|---|---:|']
    for role in roles:
        tables.append(f'| {role["name"]} | {role["axis"]} | {cell(counts["eight"][role["id"]])} |')
    tables += ['', f'{heading} 八人身份局：角色胜率', '',
               '随机组用于拟合身份选角偏好；偏好组使用独立种子验证。两组均先随机发候选，再选择角色，分母包含平局。', '',
               '| 角色 | 随机选角（胜／场） | 身份偏好选角（胜／场） |', '|---|---:|---:|']
    for role in roles:
        tables.append('| ' + role['name'] + ' | ' + ' | '.join(
            cell(counts[m][role['id']]) for m in identity_roles) + ' |')
    tables += ['', f'{heading} 阵营整体胜率', '',
               '每个阵营每局只计一次。三类阵营各参加600局，其中3人方400局、2人方200局；整体胜率不等于逐角色胜率的简单平均。', '',
               '| 阵营 | 全部（胜／局） | 3人方（胜／局） | 2人方（胜／局） |', '|---|---:|---:|---:|']
    for axis in ('守序', '中立', '混乱'):
        tables.append('| ' + axis + ' | ' + ' | '.join(
            cell(faction_summary[axis][k]) for k in ('overall', 'three_seats', 'two_seats')) + ' |')
    tables += ['', f'{heading} 身份整体胜率', '',
               '主公1席、忠臣2席、反贼4席、内奸1席。表中将同身份席位折算为每局一次：主公与忠臣共用胜负，不能把两行相加；已阵亡角色按其阵线结局计胜。', '',
               '| 身份 | 随机选角（胜／局） | 身份偏好选角（胜／局） |', '|---|---:|---:|']
    for role, label in (('Lord', '主公／明主／霸王／暴君'), ('Loyalist', '忠臣'), ('Rebel', '反贼'), ('Traitor', '内奸')):
        tables.append('| ' + label + ' | ' + ' | '.join(cell(identity_summary[m][role]) for m in identity_roles) + ' |')
    tables += ['', f'阵营局平局{totals["eight"]["draws"]}局；随机身份平局{totals["identity"]["draws"]}局，偏好身份平局{totals["identity-preferred"]["draws"]}局。角色出场少时波动较大，不宜据小幅差异排名。']
    return tables

if full_identity:
    lines += eight_tables(2)
if 'previous_round_comparison' in report:
    previous = report['previous_round_comparison']
    lines += ['', '## 与上一轮相比', '',
              f'上一轮汇总发布于{previous["previous_date"]}，{previous["previous_roster_count"]}子职、{previous["previous_legal_duals"]}种双将、{sum(s["games"] for s in previous["previous_totals"].values()):,}局。各模式原采样日期及源码见下表。{previous["caution"]}', '',
              '| 模式 | 原采样日期 | 原源码提交 | 原源码SHA256 |', '|---|---|---|---|',
              *[f'| {mode} | {p["date"]} | `{p["source_commit"]}` | `{p["source_sha256"]}` |'
                for mode, p in previous.get('previous_mode_provenance', {}).items()], '',
              '| 角色 | 1v1差（百分点） | 2v2差（百分点） | 双将差（百分点） | 阵营差（百分点） | 随机身份差（百分点） | 偏好身份差（百分点） |',
              '|---|---:|---:|---:|---:|---:|---:|']
    for role in previous['roles']:
        deltas = [role['modes'][mode]['delta_percentage_points'] for mode in counts]
        lines.append('| ' + role['name'] + ' | ' + ' | '.join('新增／无可比样本' if d is None else f'{d:+.1f}' for d in deltas) + ' |')
    lines += ['', '机器汇总保留上一轮各模式总局数、源码指纹，以及各角色原胜率、当前胜率、出场数和百分点差。新增角色不填旧轮胜率。']
if args.update_roster:
    assert full_identity, 'Eight-player identity tables require --arcane or --state-ai'
    roster_doc = root / 'Docs/roster.md'
    text = roster_doc.read_text(encoding='utf-8')
    marker = '<!-- eight-player-statistics:start -->'
    end_marker = '<!-- eight-player-statistics:end -->'
    block = marker + '\n' + '\n'.join(eight_tables(3)) + '\n' + end_marker + '\n'
    if marker in text:
        start, rest = text.split(marker, 1)
        _, end = rest.split(end_marker, 1)
        text = start + block + end.lstrip('\n')
    else:
        text = text.rstrip() + '\n\n' + block
    roster_doc.write_text(text, encoding='utf-8')
lines += ['', '## 复现与证据', '',
          '所有模式使用真实BoardController驱动，复用已校正胜者归属的测试台。固定起手、技能及AI随机源，每个CSV保存逐局种子、角色、胜者和行动数。roster.tsv由运行时角色表直接导出；results.json保存五类模式（身份含随机和偏好两组）的逐角色胜／负／平与分母、阵营及身份汇总、源码及CSV指纹；各模式日志均须通过才能汇总。', '',
          '```powershell', "$env:DOTNET_PROCESSOR_COUNT='1'", "$env:DOTNET_TieredCompilation='0'", "$env:COMPlus_TieredCompilation='0'",
          f"$env:FDD_REVISION_ROOT='Tools/Statistics/{out.name}'",
          "foreach ($mode in @('1v1','2v2','dual','eight','identity'" + (",'identity-preferred'" if args.arcane else '') + ')) {',
          *(["  if ($mode -eq 'identity-preferred') {",
             f"    Copy-Item 'Tools/Statistics/{out.name}/identity-roles.tsv' 'Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt'",
             '  }'] if args.arcane else []),
          '  $env:FDD_REVISION_MODE=$mode',
          '  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Only RevisionBalanceTests -OutputDirectory Tools/HeadlessTests/bin/revision-sample |',
          f'    Tee-Object -FilePath "{args.log_prefix}$mode.log"',
          '  if ($LASTEXITCODE -ne 0) { throw "Sampling failed: $mode" }',
          '}',
          f'python Tools/Statistics/summarize-current.py --date {args.date} --directory {out.name} --log-prefix {args.log_prefix}'
          + (' --arcane' if args.arcane else ' --ranger' if args.ranger else ' --balance' if args.balance else ' --optimized-ai' if args.optimized_ai else ''), '```']
if args.state_ai:
    # The orchestrator captures provenance before sampling, then validates it between modes.
    start = lines.index('```powershell', lines.index('## 复现与证据'))
    lines = lines[:start] + ['```powershell',
        (f'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Statistics/run-current.ps1 -Run '
         f'-Date {args.date} -Directory {out.name}-replay '
         f'-Baseline "Tools/Statistics/{out.name}/baseline-results.json" '
         f'-RuntimeVersion {report.get("sampler_runtime_version", "8.0.15")}'
         + (f' -ProcessorAffinity {report["sampler_processor_affinity"]}'
            if report.get('sampler_processor_affinity') else '')),
        '```', '',
        f'{n}子职共检查{n*n*3:,}个合法起手，见[AI审计](ai-audit.tsv)与[测试日志](regression.log)。', '',
        '运行器在首局前保存provenance.json，并在每组前后核对游戏源码、测试源码、无头入口及采样工具SHA-256。指纹变化时拒绝拼接不同版本的数据。source_dirty标识采样时存在未提交改动，source_commit与源码指纹共同标识版本。', '',
        '身份随机组完成后，将identity-roles.tsv逐字节复制到Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt；偏好组使用独立种子。汇总要求运行时表与随机组完全一致，记录identity_preference_sha256。', '',
        '仅重新生成报告：', '', '```powershell',
        f'python Tools/Statistics/summarize-current.py --date {args.date} --directory {out.name} --state-ai --refresh-docs',
        '```']
elif args.arcane:
    lines += ['', '身份偏好表按主公、忠臣、反贼、内奸分别计胜／出场，并用同身份总体胜率和24局先验平滑；只对随机分发到该席的候选加权。原始数据见[随机身份分角色](identity-roles.tsv)及[偏好验证分角色](identity-preferred-roles.tsv)。',
              '', '57子职均检查了自身权重、已有对策以及双方57×57配对的三档起手，共9,747个合法起手检查。逐角色运行时导出见[AI审计](ai-audit.tsv)，功能回归见[测试日志](regression.log)。']
    lines += ['', '仅补全现有样本的文档表格时，使用以下命令。它核对全部CSV、身份分表与已发布汇总，保留原采样日期、源码提交及指纹，不重采样。', '',
              '```powershell',
              f'python Tools/Statistics/summarize-current.py --date {args.date} --directory {out.name} --arcane --refresh-docs --update-roster',
              '```']
elif args.ranger:
    lines += ['', '游侠的策略和对策见[AI策略清单](../../../Docs/ai-strategy-reference.md)。功能回归见[测试日志](regression.log)。']
elif args.balance:
    lines += ['', '八子职的胜率前后对照和57子职的策略复查见[平衡复测记录](balance-review.md)。']
elif args.optimized_ai:
    lines += ['', '策略、配对权重实验与前后对照见[优化记录](ai-tuning.md)。']
else:
    lines += ['', '执行记录：首次2v2测试进程发生原生中断，随后从首局完整重跑25,536局并通过。完整功能回归1611项通过、0失败，日志见regression.log。']
if args.identity_only:
    lines[0] = f'# {n}子职胜率汇总：身份局重新采样（{args.date}）'
    lines[2] = ('移除忠臣替死后，仅重新采样八人身份随机2,000局及偏好600局，共 **2,600局新样本**。'
                f'其余四组沿用旧来源；合并展示{total:,}局，不表示全部重测。底层扩展接口保留，游戏内替死按钮停用。')
    lines[4:4] = ['## 数据来源', '',
        '| 模式 | 本轮处理 | 原采样日期 | 原源码提交 |', '|---|---|---|---|',
        *[f'| {mode} | {"新采样" if p["status"] == "newly_sampled" else "沿用旧样本，未重测"} | {p["date"]} | `{p["source_commit"]}` |'
          for mode, p in report['mode_provenance'].items()], '',
        '逐模式源码指纹见results.json的mode_provenance；旧数据与日志逐字节保留，来源汇总见baseline-results.json。', '']
    lines = [(f'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Statistics/run-identity.ps1 -Run '
              f'-Date {args.date} -Directory {out.name}-replay -Baseline "Tools/Statistics/{out.name}/baseline-results.json"')
             if line.startswith('powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Statistics/run-current.ps1 -Run')
             else line
             for line in lines]
    lines = [line
             .replace('--state-ai --refresh-docs', '--identity-only --refresh-docs') for line in lines]
    lines += ['', '本轮运行器仅执行audit、identity、identity-preferred和回归；不会重新执行1v1、2v2、dual、eight。随机身份种子5000000–5001999，偏好身份种子5100000–5100599，与上一轮保持一致。']
    lines += ['', '## 身份整体胜率与直接上一轮比较', '',
              '保持相同样本数与逐局种子；数字是固定AI的描述性结果。主公与忠臣共用胜方，不能将两行相加。', '',
              '| 模式 | 身份 | 上轮胜／局 | 本轮胜／局 | 差（百分点） |', '|---|---|---:|---:|---:|']
    for mode, records in report['identity_aggregate_comparison'].items():
        for role, stat in records.items():
            lines.append(f'| {mode} | {role} | {cell(stat["previous"])} | {cell(stat["current"])} | {stat["delta_percentage_points"]:+.1f} |')
(out / 'README.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(f'PASS: {total:,} outcomes; {n} complete role rows; no errors or forced aborts.')
