import json
import sys
from pathlib import Path


def load_summary(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def fmt(value: float) -> str:
    return f"{value:.2f}"


def summarize(container: str, metric: str, data: dict) -> str:
    entry = data.get(container, {}).get(metric, {})
    return f"{fmt(entry.get('min', 0.0))} / {fmt(entry.get('avg', 0.0))} / {fmt(entry.get('max', 0.0))}"


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: compare_resource_profiles.py <baseline.summary.json> <profile.summary.json> [output.md]")
        return 1

    baseline_path = Path(sys.argv[1])
    profile_path = Path(sys.argv[2])
    output_path = Path(sys.argv[3]) if len(sys.argv) > 3 else profile_path.with_suffix(".compare.md")

    baseline = load_summary(baseline_path)
    profile = load_summary(profile_path)

    containers = sorted(set(baseline.keys()) | set(profile.keys()))

    lines = [
        "# Resource profile comparison",
        "",
        f"Baseline: `{baseline_path}`",
        f"Profile: `{profile_path}`",
        "",
        "| container | metric | baseline (min/avg/max) | profile (min/avg/max) | delta avg |",
        "| --- | --- | --- | --- | --- |",
    ]

    for container in containers:
        for metric in ("cpu_percent", "mem_mb", "net_mb", "block_mb"):
            b_avg = baseline.get(container, {}).get(metric, {}).get("avg", 0.0)
            p_avg = profile.get(container, {}).get(metric, {}).get("avg", 0.0)
            delta = p_avg - b_avg
            lines.append(
                f"| {container} | {metric} | {summarize(container, metric, baseline)} | "
                f"{summarize(container, metric, profile)} | {fmt(delta)} |"
            )

    output_path.write_text("\n".join(lines), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
