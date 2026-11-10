"""Sampling provenance and cross-round comparisons (no gameplay dependencies)."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
from datetime import datetime, timezone


def source_hash(root, folder, pattern='*.cs'):
    digest = hashlib.sha256()
    paths = []
    for directory, children, _ in os.walk(root / folder):
        children[:] = [name for name in children if name.lower() not in ('bin', 'obj', '__pycache__')]
        paths.extend(Path(directory).glob(pattern))
    for path in sorted(path for path in paths if path.is_file()):
        digest.update(path.relative_to(root).as_posix().encode())
        digest.update(path.read_bytes())
    return digest.hexdigest()


def latest_published(root):
    candidates = sorted((root / 'Tools/Statistics').glob('*/results.json'))
    if not candidates:
        raise FileNotFoundError('No published Statistics round with results.json')
    return candidates[-1]


def comparison_baseline(root):
    published = latest_published(root)
    previous = published.with_name('baseline-results.json')
    return previous if previous.is_file() else published


def fingerprints(root):
    result = {
        'source_sha256': source_hash(root, 'Assets/Scripts'),
        'test_source_sha256': source_hash(root, 'Assets/Tests'),
        'headless_source_sha256': source_hash(root, 'Tools/HeadlessTests'),
        'headless_runner_sha256': hashlib.sha256((root / 'Tools/HeadlessTests/run-tests.ps1').read_bytes()).hexdigest(),
        'statistics_tool_sha256': source_hash(root, 'Tools/Statistics', '*.py'),
        'statistics_runner_sha256': hashlib.sha256((root / 'Tools/Statistics/run-current.ps1').read_bytes()).hexdigest(),
    }
    identity_runner = root / 'Tools/Statistics/run-identity.ps1'
    if identity_runner.is_file():
        result['identity_runner_sha256'] = hashlib.sha256(identity_runner.read_bytes()).hexdigest()
    return result


def capture(root):
    return {
        'captured_utc': datetime.now(timezone.utc).isoformat(),
        'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
        'source_dirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=root, text=True).strip()),
        **fingerprints(root),
    }


def verify(root, manifest):
    for key, value in fingerprints(root).items():
        assert manifest[key] == value, f'Source changed during sampling: {key}; rerun all modes'


def comparison(current, baseline):
    old = {r['id']: r for r in baseline['roles']}
    previous_modes = {
        mode: dict(baseline.get('mode_provenance', {}).get(mode, {
            key: baseline[key] for key in ('date', 'source_commit', 'source_sha256', 'test_source_sha256')
            if key in baseline
        })) for mode in baseline['totals']
    }
    roles = []
    for role in current['roles']:
        prior = old.get(role['id'])
        changes = {}
        for mode, stat in role['modes'].items():
            before = prior['modes'].get(mode) if prior else None
            changes[mode] = {
                'previous_date': previous_modes.get(mode, {}).get('date'),
                'previous_source_sha256': previous_modes.get(mode, {}).get('source_sha256'),
                'previous_games': before['games'] if before else None,
                'previous_win_percent': before['win_percent'] if before else None,
                'current_games': stat['games'],
                'current_win_percent': stat['win_percent'],
                'delta_percentage_points': round(stat['win_percent'] - before['win_percent'], 3)
                    if before and before['win_percent'] is not None and stat['win_percent'] is not None else None,
            }
        roles.append({'id': role['id'], 'name': role['name'], 'new_role': prior is None, 'modes': changes})
    return {'previous_date': baseline['date'], 'previous_source_commit': baseline['source_commit'],
            'previous_source_sha256': baseline['source_sha256'],
            'previous_mode_provenance': previous_modes,
            'previous_roster_count': baseline['roster_count'], 'previous_legal_duals': baseline['legal_duals'],
            'previous_totals': baseline['totals'], 'roles': roles,
            'removed_roles': sorted(set(old) - {r['id'] for r in current['roles']}),
            'caution': '角色池、规则和AI可能同时改变；百分点差是描述性比较，不能归因于单次修改。'}


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('action', choices=('capture', 'verify', 'baseline'))
    parser.add_argument('manifest', type=Path, nargs='?')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    if args.action == 'baseline':
        print(latest_published(root))
        raise SystemExit(0)
    if args.manifest is None:
        parser.error('capture and verify require a manifest path')
    if args.action == 'capture':
        assert not args.manifest.exists(), 'Refusing to overwrite sampling provenance'
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(capture(root), indent=2) + '\n', encoding='utf-8')
    else:
        verify(root, json.loads(args.manifest.read_text(encoding='utf-8')))
    print('PASS: sampling provenance ' + args.action)
