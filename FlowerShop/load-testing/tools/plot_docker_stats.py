import argparse
import csv
import re
from pathlib import Path


CPU_RE = re.compile(r"([0-9.]+)%")
MEM_RE = re.compile(r"([0-9.]+)([KMG]iB)")


def to_mebibytes(value: str, unit: str) -> float:
    factor = {"KiB": 1 / 1024, "MiB": 1, "GiB": 1024}
    return float(value) * factor[unit]


def parse_cpu(raw: str) -> float:
    match = CPU_RE.match(raw.strip())
    return float(match.group(1)) if match else 0.0


def parse_mem_usage(raw: str) -> float:
    left = raw.split("/", 1)[0].strip()
    match = MEM_RE.match(left)
    if not match:
        return 0.0
    return to_mebibytes(match.group(1), match.group(2))


def load_stats(path: Path):
    per_container = {}
    with path.open(encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            container = row["container"]
            per_container.setdefault(container, {"cpu": [], "mem": []})
            per_container[container]["cpu"].append(parse_cpu(row["cpu_percent"]))
            per_container[container]["mem"].append(parse_mem_usage(row["mem_usage"]))
    return per_container


def aggregate_series(series_list):
    if not series_list:
        return []
    max_len = max(len(s) for s in series_list)
    aggregated = []
    for idx in range(max_len):
        values = [s[idx] for s in series_list if idx < len(s)]
        aggregated.append(sum(values) / len(values))
    return aggregated


def plot_series(out_dir: Path, per_container: dict, title: str, ylabel: str, key: str, filename: str):
    try:
        import matplotlib.pyplot as plt
    except ImportError:
        return

    for container, series in per_container.items():
        values = series[key]
        if not values:
            continue
        x = list(range(len(values)))
        plt.figure()
        plt.plot(x, values)
        plt.title(f"{title} - {container}")
        plt.xlabel("sample")
        plt.ylabel(ylabel)
        plt.grid(True)
        plt.savefig(out_dir / f"{filename}_{container}.png")
        plt.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-glob", required=True)
    parser.add_argument("--out-dir", required=True)
    args = parser.parse_args()

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    import glob

    input_glob = args.input_glob.strip("\"'")
    files = [Path(p) for p in glob.glob(input_glob, recursive=True)]
    if not files:
        print(f"No input files matched: {input_glob}")
        return

    aggregated = {}
    for path in files:
        per_container = load_stats(path)
        for container, series in per_container.items():
            entry = aggregated.setdefault(container, {"cpu": [], "mem": []})
            entry["cpu"].append(series["cpu"])
            entry["mem"].append(series["mem"])

    averaged = {}
    for container, series in aggregated.items():
        averaged[container] = {
            "cpu": aggregate_series(series["cpu"]),
            "mem": aggregate_series(series["mem"]),
        }

    plot_series(out_dir, averaged, "CPU usage (avg)", "CPU %", "cpu", "cpu_avg")
    plot_series(out_dir, averaged, "Memory usage (avg)", "MiB", "mem", "mem_avg")


if __name__ == "__main__":
    main()
