#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path

from tensorboard.backend.event_processing.event_accumulator import EventAccumulator


def sanitize_filename(name: str) -> str:
    sanitized = re.sub(r"[^\w.-]+", "_", name.strip())
    sanitized = sanitized.strip("._")
    return sanitized or "metric"


def collect_event_files(run_dir: Path) -> list[Path]:
    return sorted(run_dir.rglob("events.out.tfevents*"))


def export_event_file(event_file: Path, output_dir: Path, selected_tags: set[str] | None) -> list[dict]:
    accumulator = EventAccumulator(str(event_file))
    accumulator.Reload()

    scalar_tags = accumulator.Tags().get("scalars", [])
    export_rows: list[dict] = []

    for tag in scalar_tags:
        if selected_tags is not None and tag not in selected_tags:
            continue

        scalar_events = accumulator.Scalars(tag)
        if not scalar_events:
            continue

        relative_parent = event_file.parent.name
        filename = f"{relative_parent}__{sanitize_filename(tag)}.csv"
        csv_path = output_dir / filename

        with csv_path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.writer(handle)
            writer.writerow(["wall_time", "step", "value"])
            for scalar in scalar_events:
                writer.writerow([scalar.wall_time, scalar.step, scalar.value])

        export_rows.append(
            {
                "tag": tag,
                "source_event_file": str(event_file),
                "csv_file": str(csv_path),
                "points": len(scalar_events),
            }
        )

    return export_rows


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Export TensorBoard scalar tags from an ML-Agents run into CSV files."
    )
    parser.add_argument(
        "run",
        help="Run directory name under results/, or an explicit path to a run directory.",
    )
    parser.add_argument(
        "--results-root",
        default="results",
        help="Root directory containing ML-Agents runs. Default: results",
    )
    parser.add_argument(
        "--output-dir",
        default=None,
        help="Directory to write CSV files into. Default: <run>/csv_export",
    )
    parser.add_argument(
        "--tag",
        action="append",
        default=None,
        help="Only export this scalar tag. Repeat for multiple tags.",
    )
    parser.add_argument(
        "--list-tags",
        action="store_true",
        help="List scalar tags found in the run and exit without exporting.",
    )
    args = parser.parse_args()

    run_arg = Path(args.run)
    if run_arg.exists():
        run_dir = run_arg
    else:
        run_dir = Path(args.results_root) / args.run

    if not run_dir.exists():
        raise SystemExit(f"Run directory not found: {run_dir}")

    event_files = collect_event_files(run_dir)
    if not event_files:
        raise SystemExit(f"No TensorBoard event files found in: {run_dir}")

    selected_tags = set(args.tag) if args.tag else None

    if args.list_tags:
        discovered_tags: set[str] = set()
        for event_file in event_files:
            accumulator = EventAccumulator(str(event_file))
            accumulator.Reload()
            discovered_tags.update(accumulator.Tags().get("scalars", []))

        for tag in sorted(discovered_tags):
            print(tag)
        return 0

    output_dir = Path(args.output_dir) if args.output_dir else run_dir / "csv_export"
    output_dir.mkdir(parents=True, exist_ok=True)

    manifest: list[dict] = []
    for event_file in event_files:
        manifest.extend(export_event_file(event_file, output_dir, selected_tags))

    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"Exported {len(manifest)} CSV files to {output_dir}")
    print(f"Manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
