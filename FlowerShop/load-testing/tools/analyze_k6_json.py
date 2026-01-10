import argparse
import csv
import json
import math
import glob
import re
from pathlib import Path


def load_samples(paths, metric="http_req_duration"):
    samples = []
    for path in paths:
        start_time = None
        with path.open(encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                try:
                    record = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if record.get("type") != "Point":
                    continue
                if record.get("metric") != metric:
                    continue
                data = record.get("data", {})
                timestamp = data.get("time")
                value = data.get("value")
                if timestamp is None or value is None:
                    continue
                if start_time is None:
                    start_time = timestamp
                samples.append((path.name, timestamp, float(value), start_time))
    return samples


def normalize_time(samples):
    per_file = {}
    for name, timestamp, value, _ in samples:
        per_file.setdefault(name, [])
        per_file[name].append((timestamp, value))

    normalized = []
    for name, entries in per_file.items():
        entries.sort(key=lambda item: item[0])
        parsed_entries = []
        failed = False
        for ts, value in entries:
            parsed_ts = parse_time(ts)
            if (
                parsed_ts == 0.0
                and isinstance(ts, str)
                and ts not in ("0", "0.0")
            ):
                failed = True
            parsed_entries.append((ts, value, parsed_ts))

        if failed:
            for idx, (_, value, _) in enumerate(parsed_entries):
                normalized.append((name, float(idx), value))
            continue

        start_parsed = parsed_entries[0][2]
        for _, value, parsed_ts in parsed_entries:
            seconds = max(0.0, parsed_ts - start_parsed)
            normalized.append((name, seconds, value))
    return normalized


def aggregate_time_series(samples, bucket_seconds=1):
    per_file = {}
    for name, timestamp, value, _ in samples:
        per_file.setdefault(name, [])
        per_file[name].append((timestamp, value))

    per_file_series = []
    for name, entries in per_file.items():
        entries.sort(key=lambda item: item[0])
        parsed_entries = []
        failed = False
        for ts, value in entries:
            parsed_ts = parse_time(ts)
            if (
                parsed_ts == 0.0
                and isinstance(ts, str)
                and ts not in ("0", "0.0")
            ):
                failed = True
            parsed_entries.append((ts, value, parsed_ts))

        series = {}
        if failed:
            for idx, (_, value, _) in enumerate(parsed_entries):
                bucket = int(idx // bucket_seconds)
                series.setdefault(bucket, []).append(value)
        else:
            start_parsed = parsed_entries[0][2]
            for _, value, parsed_ts in parsed_entries:
                seconds = max(0.0, parsed_ts - start_parsed)
                bucket = int(seconds // bucket_seconds)
                series.setdefault(bucket, []).append(value)

        averaged = {bucket: sum(values) / len(values) for bucket, values in series.items()}
        per_file_series.append(averaged)

    merged = {}
    for series in per_file_series:
        for bucket, value in series.items():
            merged.setdefault(bucket, []).append(value)

    return [(bucket, sum(values) / len(values)) for bucket, values in sorted(merged.items())]


def _normalize_iso(timestamp: str) -> str:
    if timestamp.endswith("Z"):
        timestamp = timestamp[:-1] + "+00:00"

    match = re.search(r"([+-]\d\d:\d\d)$", timestamp)
    offset = ""
    if match:
        offset = match.group(1)
        base = timestamp[: -len(offset)]
    else:
        base = timestamp

    if "." in base:
        date_part, frac = base.split(".", 1)
        frac = "".join(ch for ch in frac if ch.isdigit())
        frac = (frac + "000000")[:6]
        base = f"{date_part}.{frac}"

    return base + offset


def parse_time(timestamp):
    if isinstance(timestamp, (int, float)):
        return float(timestamp)
    try:
        from datetime import datetime

        normalized = _normalize_iso(timestamp)
        return datetime.fromisoformat(normalized).timestamp()
    except ValueError:
        return 0.0


def compute_percentiles(values, percentiles):
    if not values:
        return {p: 0.0 for p in percentiles}
    values_sorted = sorted(values)
    result = {}
    for p in percentiles:
        idx = int(math.ceil(p * len(values_sorted))) - 1
        idx = max(0, min(idx, len(values_sorted) - 1))
        result[p] = values_sorted[idx]
    return result


def compute_time_series(normalized, bucket_seconds=1):
    series = {}
    for _, seconds, value in normalized:
        bucket = int(seconds // bucket_seconds)
        series.setdefault(bucket, []).append(value)
    return [(bucket, sum(values) / len(values)) for bucket, values in sorted(series.items())]


def compute_histogram(values, buckets=20):
    if not values:
        return []
    min_v = min(values)
    max_v = max(values)
    if min_v == max_v:
        return [(min_v, len(values))]
    step = (max_v - min_v) / buckets
    counts = [0 for _ in range(buckets)]
    for value in values:
        idx = int((value - min_v) / step)
        if idx == buckets:
            idx -= 1
        counts[idx] += 1
    histogram = []
    for i, count in enumerate(counts):
        bucket_start = min_v + i * step
        histogram.append((bucket_start, count))
    return histogram


def write_csv(path, headers, rows):
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(headers)
        writer.writerows(rows)


def plot_series(out_dir, series, histogram, percentiles):
    try:
        import matplotlib.pyplot as plt
    except ImportError:
        return

    if series:
        x = [item[0] for item in series]
        y = [item[1] for item in series]
        plt.figure()
        plt.plot(x, y)
        plt.title("http_req_duration average over time")
        plt.xlabel("time (s)")
        plt.ylabel("duration (ms)")
        plt.grid(True)
        plt.savefig(out_dir / "time_series.png")
        plt.close()

    if histogram:
        x = [item[0] for item in histogram]
        y = [item[1] for item in histogram]
        plt.figure()
        plt.bar(x, y, width=(x[1] - x[0]) if len(x) > 1 else 1.0)
        plt.title("http_req_duration histogram")
        plt.xlabel("duration (ms)")
        plt.ylabel("count")
        plt.grid(True)
        plt.savefig(out_dir / "histogram.png")
        plt.close()

    if percentiles:
        labels = [f"p{int(p * 100)}" for p in percentiles.keys()]
        values = list(percentiles.values())
        plt.figure()
        plt.plot(labels, values, marker="o")
        plt.title("http_req_duration percentiles")
        plt.xlabel("percentile")
        plt.ylabel("duration (ms)")
        plt.grid(True)
        plt.savefig(out_dir / "percentiles.png")
        plt.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-glob", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--bucket-seconds", type=int, default=1)
    parser.add_argument("--max-points", type=int, default=0)
    args = parser.parse_args()

    input_glob = args.input_glob.strip("\"'")
    globbed = glob.glob(input_glob, recursive=True)
    paths = sorted(Path(p) for p in globbed)
    if not paths:
        print(f"No input files matched: {input_glob}")
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    samples = load_samples(paths)
    normalized = normalize_time(samples)
    values = [item[2] for item in normalized]

    series = aggregate_time_series(samples, bucket_seconds=max(1, args.bucket_seconds))
    if args.max_points and len(series) > args.max_points:
        span = max(1, int(len(series) / args.max_points))
        series = aggregate_time_series(samples, bucket_seconds=span)
    histogram = compute_histogram(values)
    percentiles = compute_percentiles(values, [0.5, 0.75, 0.9, 0.95, 0.99])

    write_csv(out_dir / "time_series.csv", ["second", "avg_ms"], series)
    write_csv(out_dir / "histogram.csv", ["bucket_start_ms", "count"], histogram)
    write_csv(out_dir / "percentiles.csv", ["percentile", "value_ms"], percentiles.items())

    plot_series(out_dir, series, histogram, percentiles)


if __name__ == "__main__":
    main()
