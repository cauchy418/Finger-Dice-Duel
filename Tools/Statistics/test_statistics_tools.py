"""Tool-only checks; outputs stay under the ignored headless bin directory."""
from pathlib import Path
import copy
import collections
import csv
import itertools
import json
import shutil
import subprocess
import tempfile
import unittest
from statistics_common import capture, comparison, fingerprints, verify, source_hash, latest_published, comparison_baseline

ROOT = Path(__file__).resolve().parents[2]
BIN = ROOT / 'Tools/HeadlessTests/bin'


class StatisticsToolsTests(unittest.TestCase):
    def test_fingerprint_ignores_build_and_cache_directories(self):
        BIN.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='statistics-hash-test-', dir=BIN) as temporary:
            fixture = Path(temporary)
            source = fixture / 'Tools/HeadlessTests'
            source.mkdir(parents=True)
            (source / 'Program.cs').write_text('source', encoding='utf-8')
            before = source_hash(fixture, 'Tools/HeadlessTests')
            for name in ('bin', 'obj', '__pycache__', 'BIN'):
                output = source / name / 'nested'
                output.mkdir(parents=True, exist_ok=True)
                (output / 'Generated.cs').write_text('ignored', encoding='utf-8')
            self.assertEqual(before, source_hash(fixture, 'Tools/HeadlessTests'))
            (source / 'Program.cs').write_text('changed source', encoding='utf-8')
            self.assertNotEqual(before, source_hash(fixture, 'Tools/HeadlessTests'))

    def test_published_discovery_works_after_previous_directory_removal(self):
        BIN.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='statistics-discovery-test-', dir=BIN) as temporary:
            fixture = Path(temporary)
            published = fixture / 'Tools/Statistics/2026-11-09-state-ai/results.json'
            published.parent.mkdir(parents=True)
            published.write_text('{}', encoding='utf-8')
            self.assertEqual(published, latest_published(fixture))
            self.assertEqual(published, comparison_baseline(fixture))
            baseline = published.with_name('baseline-results.json')
            baseline.write_text('{"roster_count": 57}', encoding='utf-8')
            self.assertEqual(baseline, comparison_baseline(fixture))

    def test_comparison_keeps_previous_denominators_and_new_roles(self):
        baseline = json.loads(comparison_baseline(ROOT).read_text(encoding='utf-8'))
        current = copy.deepcopy(baseline)
        current['roles'][0]['modes']['1v1']['win_percent'] += 1.25
        previous_games = baseline['roles'][0]['modes']['1v1']['games']
        current['roles'][0]['modes']['1v1']['games'] = previous_games + 20
        current['roles'].append({**copy.deepcopy(current['roles'][0]), 'id': 'new-role'})
        result = comparison(current, baseline)
        first = result['roles'][0]['modes']['1v1']
        self.assertEqual(1.25, first['delta_percentage_points'])
        self.assertEqual(previous_games, first['previous_games'])
        self.assertEqual(previous_games + 20, first['current_games'])
        self.assertTrue(result['roles'][-1]['new_role'])
        self.assertIsNone(result['roles'][-1]['modes']['1v1']['delta_percentage_points'])

    def test_provenance_rejects_changed_game_source(self):
        manifest = fingerprints(ROOT)
        verify(ROOT, manifest)
        manifest['source_sha256'] = 'changed'
        with self.assertRaisesRegex(AssertionError, 'source_sha256'):
            verify(ROOT, manifest)

    def test_previous_published_round_can_still_refresh_in_bin(self):
        BIN.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='statistics-tool-test-', dir=BIN) as temporary:
            snapshot = Path(temporary) / 'snapshot'
            shutil.copytree(latest_published(ROOT).parent, snapshot)
            before = json.loads((snapshot / 'results.json').read_text(encoding='utf-8'))
            profile = '--state-ai' if 'ai_audit' in before else '--arcane'
            result = subprocess.run([
                'python', str(ROOT / 'Tools/Statistics/summarize-current.py'),
                '--date', before['date'], '--directory', str(snapshot), profile, '--refresh-docs'
            ], cwd=ROOT, capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            after = json.loads((snapshot / 'results.json').read_text(encoding='utf-8'))
            self.assertEqual(before, after)
            self.assertIn(f'{sum(stat["games"] for stat in before["totals"].values()):,}', result.stdout)

    def test_current_round_synthetic_csv_validation_and_refresh(self):
        # Synthetic draws exercise the reporting pipeline without playing any games.
        BIN.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='statistics-58-fixture-', dir=BIN) as temporary:
            fixture = Path(temporary)
            tools = fixture / 'Tools/Statistics'
            tools.mkdir(parents=True)
            for name in ('summarize-current.py', 'statistics_common.py', 'run-current.ps1'):
                shutil.copyfile(ROOT / 'Tools/Statistics' / name, tools / name)
            (fixture / 'Assets/Scripts').mkdir(parents=True)
            (fixture / 'Assets/Tests').mkdir(parents=True)
            (fixture / 'Tools/HeadlessTests').mkdir(parents=True)
            (fixture / 'Tools/HeadlessTests/run-tests.ps1').write_text('# fixture', encoding='utf-8')
            out = tools / '2026-11-09-state-ai'
            out.mkdir()
            roles = []
            for axis, size in (('守序', 19), ('中立', 20), ('混乱', 19)):
                for _ in range(size):
                    roles.append({'id': f'role-{len(roles)}', 'name': f'角色{len(roles)}', 'axis': axis})
            with (out / 'roster.tsv').open('w', encoding='utf-8', newline='') as stream:
                writer = csv.DictWriter(stream, fieldnames=('id', 'name', 'axis'), delimiter='\t')
                writer.writeheader()
                writer.writerows(roles)
            pairs = list(itertools.combinations([r['id'] for r in roles], 2))
            duals = [(a['id'], b['id']) for a, b in itertools.combinations(roles, 2) if a['axis'] == b['axis']]
            for mode, teams, repeat in (('1v1', pairs, 20), ('2v2', pairs, 16), ('dual', duals, 8)):
                with (out / (mode + '.csv')).open('w', encoding='utf-8', newline='') as stream:
                    writer = csv.writer(stream)
                    writer.writerow(('seed', 'first', 'second', 'winner', 'turns'))
                    for index, pair in enumerate(teams):
                        opponent = teams[(index + len(teams)//2) % len(teams)]
                        for trial in range(repeat):
                            first, second = pair if mode == '1v1' else ('+'.join(pair), '+'.join(opponent))
                            writer.writerow((index*repeat+trial, first, second, -1, 1))
            factions = [[r['id'] for r in roles if r['axis'] == axis] for axis in ('守序', '中立', '混乱')]
            seats = ['Lord', 'Loyalist', 'Loyalist', 'Rebel', 'Rebel', 'Rebel', 'Rebel', 'Traitor']
            for mode, size in (('eight', 600), ('identity', 2000), ('identity-preferred', 600)):
                rates = collections.Counter()
                with (out / (mode + '.csv')).open('w', encoding='utf-8', newline='') as stream:
                    writer = csv.writer(stream)
                    writer.writerow(('seed', 'first', 'second', 'winner', 'turns'))
                    for game in range(size):
                        picks = [r['id'] for r in (roles*2)[game%58:game%58+8]]
                        if mode == 'eight':
                            picks = [role for index, pool in enumerate(factions) for role in pool[:2 if index == game%3 else 3]]
                        writer.writerow((game, '', '+'.join(picks), -1, 1))
                        rates.update(zip(seats, picks))
                if mode != 'eight':
                    with (out / (mode + '-roles.tsv')).open('w', encoding='utf-8', newline='') as stream:
                        writer = csv.writer(stream, delimiter='\t')
                        writer.writerow(('role', 'id', 'wins', 'games'))
                        writer.writerows((role, id_, 0, games) for (role, id_), games in sorted(rates.items()))
            runtime = fixture / 'Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt'
            runtime.parent.mkdir(parents=True)
            shutil.copyfile(out / 'identity-roles.tsv', runtime)
            for mode in ('1v1', '2v2', 'dual', 'eight', 'identity', 'identity-preferred'):
                (out / (mode + '.log')).write_text('通过 1 / 失败 0\nzero errors / forced aborts', encoding='utf-8')
            (out / 'ai-audit.tsv').write_text('id\n' + '\n'.join(r['id'] for r in roles), encoding='utf-8')
            (out / 'audit.log').write_text('58 roles, 10092 legal opening decisions\n通过 1 / 失败 0', encoding='utf-8')
            (out / 'regression.log').write_text('通过 1 / 失败 0', encoding='utf-8')
            (out / 'provenance.json').write_text(json.dumps(capture(fixture)), encoding='utf-8')
            baseline = comparison_baseline(ROOT)
            command = ['python', str(tools / 'summarize-current.py'), '--date', '2026-11-09', '--directory', out.name, '--state-ai']
            result = subprocess.run(command + ['--baseline', str(baseline)], capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            report = json.loads((out / 'results.json').read_text(encoding='utf-8'))
            self.assertEqual(66964, sum(s['games'] for s in report['totals'].values()))
            self.assertEqual(532, report['legal_duals'])
            self.assertEqual(json.loads(baseline.read_text(encoding='utf-8'))['roster_count'], report['previous_round_comparison']['previous_roster_count'])
            result = subprocess.run(command + ['--refresh-docs'], capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(report, json.loads((out / 'results.json').read_text(encoding='utf-8')))


if __name__ == '__main__':
    unittest.main()
