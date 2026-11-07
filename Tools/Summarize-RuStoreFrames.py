"""Summarize game-layer SurfaceFlinger --latency samples without screenshots."""
import argparse
import json
import statistics
from pathlib import Path


def summarize(path):
    # Columns: desired_present_time, actual_present_time, frame_ready_time (ns).
    # Successive dumps overlap; zero and INT64_MAX mark unavailable samples.
    present = set()
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        fields = line.split()
        if len(fields) == 3 and all(field.isdigit() for field in fields):
            actual = int(fields[1])
            if 0 < actual < 2**63 - 1:
                present.add(actual)
    times = sorted(present)
    if len(times) < 3:
        raise ValueError(f"Not enough valid game frames in {path.name}")
    intervals = sorted((b - a) / 1e6 for a, b in zip(times, times[1:]))
    duration = (times[-1] - times[0]) / 1e9
    return {
        "source": path.name,
        "uniqueFrames": len(times),
        "durationSeconds": round(duration, 4),
        "averageFps": round((len(times) - 1) / duration, 2),
        "medianFrameMs": round(statistics.median(intervals), 3),
        "p95FrameMs": round(intervals[int((len(intervals) - 1) * .95)], 3),
        "maxFrameMs": round(max(intervals), 3),
        "gapsOverOneSecond": sum(value > 1000 for value in intervals),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("samples", nargs="+", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    report = {"method": "FPS = (unique frame count - 1) / elapsed actual-present seconds",
              "scope": "One observed game scenario; not a battle or sustained thermal benchmark.",
              "runs": [summarize(path) for path in args.samples]}
    payload = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.out:
        args.out.write_text(payload, encoding="utf-8")
    print(payload, end="")
