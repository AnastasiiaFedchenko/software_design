import csv
import json
import sys
from pathlib import Path

PERCENTILES = ["p(50)", "p(75)", "p(90)", "p(95)", "p(99)"]


def load_summary(path: Path) -> dict:
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def extract_metrics(summary: dict) -> dict:
    metrics = summary.get("metrics", {})
    duration = metrics.get("http_req_duration", {}).get("values", {})
    failed = metrics.get("http_req_failed", {}).get("values", {})
    rps = metrics.get("http_reqs", {}).get("values", {})
    result = {"http_req_failed_rate": failed.get("rate", 0.0), "http_reqs_rate": rps.get("rate", 0.0)}
    for p in PERCENTILES:
        result[p] = duration.get(p, 0.0)
    result["avg"] = duration.get("avg", 0.0)
    result["min"] = duration.get("min", 0.0)
    result["max"] = duration.get("max", 0.0)
    return result


def aggregate(rows):
    if not rows:
        return {}
    agg = {}
    for key in rows[0].keys():
        values = [row[key] for row in rows]
        agg[key] = {
            "min": min(values),
            "max": max(values),
            "avg": sum(values) / len(values),
        }
    return agg


def main():
    if len(sys.argv) < 3:
        print("Usage: aggregate_k6_summaries.py <input-glob> <output-prefix>")
        sys.exit(1)

    input_glob = sys.argv[1]
    output_prefix = Path(sys.argv[2])

    rows = []
    for summary_file in sorted(Path().glob(input_glob)):
        rows.append(extract_metrics(load_summary(summary_file)))

    if not rows:
        print("No summary.json files found.")
        sys.exit(1)

    aggregated = aggregate(rows)
    output_prefix.with_suffix(".json").write_text(
        json.dumps(aggregated, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    with output_prefix.with_suffix(".csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["metric", "min", "max", "avg"])
        for metric, stats in aggregated.items():
            writer.writerow([metric, stats["min"], stats["max"], stats["avg"]])


if __name__ == "__main__":
    main()
