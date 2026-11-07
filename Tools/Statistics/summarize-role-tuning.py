"""Select a configuration on training games and validate on separate paired games."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path

parser = argparse.ArgumentParser(__doc__)
parser.add_argument("--directory", required=True, help="Directory containing fresh tune-1v1/2v2 CSV files")
parser.add_argument("--log-prefix", default="Tools/HeadlessTests/bin/role-ai-")
args = parser.parse_args()
out = Path(args.directory)
variants = ("current", "L67", "P67", "L100")
results, hashes = [], {}
for mode, trials in (("1v1", 224), ("2v2", 320)):
    name = "tune-" + mode
    path = out / (name + ".csv")
    log = Path(args.log_prefix + name + ".log").read_text(encoding="utf-8-sig")
    assert "通过 1 / 失败 0" in log and "zero errors / forced aborts" in log, mode
    rows = {}
    seeds = {}
    with path.open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            key = (row["role"], int(row["phase"]), int(row["trial"]))
            seed = int(row["seed"])
            assert seeds.setdefault(key, seed) == seed
            outcomes = rows.setdefault(key, {})
            assert row["variant"] in variants and row["variant"] not in outcomes
            assert row["won"] in ("0", "1")
            outcomes[row["variant"]] = int(row["won"])
    roles = sorted({key[0] for key in rows})
    assert len(roles) == 5 and len(rows) == len(set(seeds.values())) == 5 * trials * 3
    assert all(set(outcomes) == set(variants) for outcomes in rows.values())
    for role in roles:
        training = [rows[role, 0, i] for i in range(trials)]
        holdout = [rows[role, 1, i] for i in range(trials * 2)]
        train_wins = {v: sum(row[v] for row in training) for v in variants}
        chosen = max(variants, key=train_wins.get)
        gains = sum(row[chosen] > row["current"] for row in holdout)
        losses = sum(row[chosen] < row["current"] for row in holdout)
        discordant = gains + losses
        p_value = min(1, 2 * sum(math.comb(discordant, i) for i in range(min(gains, losses) + 1)) / 2 ** discordant)
        improvement = (gains - losses) / len(holdout)
        selected = chosen if improvement >= 0.02 and p_value <= 0.005 else "current"
        results.append({
            "role": role, "mode": mode, "train_games_per_variant": len(training),
            "holdout_games_per_variant": len(holdout), "train_wins": train_wins,
            "train_candidate": chosen, "holdout_current_wins": sum(row["current"] for row in holdout),
            "holdout_candidate_wins": sum(row[chosen] for row in holdout),
            "paired_gains": gains, "paired_losses": losses, "improvement_pp": round(improvement * 100, 3),
            "p_value": p_value, "selected": selected,
        })
    hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    (out / (name + ".log")).write_text(log, encoding="utf-8")

(out / "tuning.json").write_text(json.dumps({
    "games": 32640, "min_gain_pp": 2, "bonferroni_alpha": 0.005,
    "csv_sha256": hashes, "results": results
}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
for result in results:
    print(result["mode"], result["role"], result["train_candidate"], result["improvement_pp"],
          f'p={result["p_value"]:.4g}', "=>", result["selected"])
