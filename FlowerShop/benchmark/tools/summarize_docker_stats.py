import csv
import json
import re
import sys
from pathlib import Path

CPU_RE = re.compile(r"([0-9.]+)%")
MEM_RE = re.compile(r"([0-9.]+)([KMG]iB)")
IO_RE = re.compile(r"([0-9.]+)([KMG]B)")


def to_mebibytes(value: str, unit: str) -> float:
    factor = {"KiB": 1 / 1024, "MiB": 1, "GiB": 1024}
    return float(value) * factor[unit]


def to_megabytes(value: str, unit: str) -> float:
    factor = {"KB": 1 / 1024, "MB": 1, "GB": 1024}
    return float(value) * factor[unit]


def parse_mem_usage(raw: str) -> float:
    left = raw.split("/", 1)[0].strip()
    match = MEM_RE.match(left)
    if not match:
        return 0.0
    return to_mebibytes(match.group(1), match.group(2))


def parse_cpu(raw: str) -> float:
    match = CPU_RE.match(raw.strip())
    return float(match.group(1)) if match else 0.0


def parse_io(raw: str) -> float:
    left = raw.split("/", 1)[0].strip()
    match = IO_RE.match(left)
    if not match:
        return 0.0
    return to_megabytes(match.group(1), match.group(2))


def summarize(path: Path) -> dict:
    metrics = {}
    with path.open(encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            container = row["container"]
            entry = metrics.setdefault(container, {"cpu": [], "mem_mb": [], "net_mb": [], "block_mb": []})
            entry["cpu"].append(parse_cpu(row["cpu_percent"]))
            entry["mem_mb"].append(parse_mem_usage(row["mem_usage"]))
            entry["net_mb"].append(parse_io(row["net_io"]))
            entry["block_mb"].append(parse_io(row["block_io"]))

    summary = {}
    for container, values in metrics.items():
        summary[container] = {
            "cpu_percent": summarize_series(values["cpu"]),
            "mem_mb": summarize_series(values["mem_mb"]),
            "net_mb": summarize_series(values["net_mb"]),
            "block_mb": summarize_series(values["block_mb"]),
        }
    return summary


def summarize_series(series):
    if not series:
        return {"min": 0.0, "max": 0.0, "avg": 0.0}
    return {
        "min": min(series),
        "max": max(series),
        "avg": sum(series) / len(series),
    }


def main():
    if len(sys.argv) < 2:
        print("Usage: summarize_docker_stats.py <docker-stats.csv> [output.json]")
        sys.exit(1)
    input_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2]) if len(sys.argv) > 2 else input_path.with_suffix(".summary.json")
    summary = summarize(input_path)
    output_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
