"""Collect native FullGame ABBA evidence; preflight certification remains separate."""
import argparse
import csv
import json
import math
import html
from collections import defaultdict
from pathlib import Path

from fullgame_scenario import capabilities
from load_report import summarize
from native_proof import lines, read, validate
from network_proof import validate as validate_network


def tick_statistics(path, expected):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == expected, "Missing tick samples"
    assert [int(row["relative_tick"]) for row in rows] == list(range(expected)), "Tick sequence mismatch"
    columns = ("whole_tick_ms", "physics_ms", "driver_ms", "observer_ms", "whole_allocated_bytes",
               "thread_allocated_bytes", "gc_pause_ms")
    for row in rows:
        for name in columns:
            number = float(row[name])
            assert math.isfinite(number) and number >= 0, f"Invalid tick metric {name}"
    longest = current = 0
    for row in rows:
        current = current + 1 if float(row["whole_tick_ms"]) > 1000 / 60 else 0
        longest = max(longest, current)
    return dict(wholeTick=summarize([float(row["whole_tick_ms"]) for row in rows]),
                physics=summarize([float(row["physics_ms"]) for row in rows]),
                longestConsecutiveOverBudgetTicks=longest,
                allocationsBytes=sum(int(row["whole_allocated_bytes"]) for row in rows),
                threadAllocationsBytes=sum(int(row["thread_allocated_bytes"]) for row in rows),
                gcPauseMs=sum(float(row["gc_pause_ms"]) for row in rows),
                top100WorstTicks=sorted(rows, key=lambda row: float(row["whole_tick_ms"]), reverse=True)[:100])


def wave_statistics(inputs, events, outcomes, ships):
    delivered = {event["id"]: event for event in events}
    completed = {outcome["id"]: outcome for outcome in outcomes}
    groups = defaultdict(list)
    for item in inputs:
        groups[(item["wave"], item["tick"], item["type"])].append(item)
    result = []
    for (wave, tick, kind), items in groups.items():
        synchronous = all(item.get("synchronous", True) for item in items)
        expected = ships if synchronous else 1
        assert len(items) == expected and len({item["ship"] for item in items}) == expected, "Incomplete fleet wave"
        starts = [delivered[item["id"]] for item in items]
        assert {event["actualTick"] for event in starts} == {tick}, "Wave dispatch is not synchronous"
        assert len({event["engineTick"] for event in starts}) == 1, "Wave starts span engine ticks"
        native = [completed[item["id"]] for item in items if item["id"] in completed]
        result.append(dict(wave=wave, tick=tick, type=kind, synchronous=synchronous, planned=len(items), delivered=len(starts),
                           asynchronousCompleted=len(native),
                           latencyTicks=[outcome["latencyTicks"] for outcome in native],
                           immediateEffects=[event["nativeEffect"] for event in starts
                                             if not event["nativeEffect"].get("asynchronous")]))
    return result


def report(directory):
    state = read(directory / "status.json")
    assert state.get("fullGame") is True, "Not a FullGame series"
    assert [run["variant"] for run in state["completedRuns"]] == ["A", "B", "B", "A"], "Incomplete ABBA order"
    scenario = directory / "scenario.jsonl"
    inputs = lines(scenario)
    pair = read(directory / "baseline-manifest.json")
    required = set(capabilities()["required"])
    assert required <= {item["type"] for item in inputs}, "Missing active event categories"
    runs = []
    for run in state["completedRuns"]:
        path = Path(run["path"])
        fingerprints = read(path / "run-manifest.json")
        build = next(item for item in pair["builds"] if item["label"] == run["variant"])
        for name in ("assemblySha256", "nativeLabSha256", "nativeGameDriversSha256"):
            assert fingerprints[name] == build[name], f"Changed build or harness: {name}"
        proof = validate(path, scenario)
        start = read(path / "started.json")
        assert start["ships"] == state["ships"], "Unexpected ship count"
        assert start["measuredTicks"] == state["measuredTicksPerRun"], "Unexpected duration"
        assert start["warmupTicks"] == state["warmupTicks"], "Unexpected warmup"
        assert start["scenarioSha256"] == state["scenarioSha256"], "Series scenario changed"
        if run["variant"] == "B":
            final = read(path / "final-telemetry.json")
            assert final["activeGrids"] == state["ships"] and final["runningCards"] == state["ships"] * 26
            assert final["faultedCards"] == final["expectedOfflineGrids"] == 0
            assert all(value == 0 for value in final["queues"].values()), "Queues did not drain"
        runs.append(dict(variant=run["variant"], path=str(path), nativeProof=proof,
                         fingerprints=fingerprints, network=validate_network(path),
                         statistics=tick_statistics(path / "ticks.csv", state["measuredTicksPerRun"]),
                         waves=wave_statistics(inputs, lines(path / "events.jsonl"),
                                               lines(path / "native-outcomes.jsonl"), state["ships"])))
    for name in ("nativeLabSha256", "nativeGameDriversSha256"):
        assert len({run["fingerprints"][name] for run in runs}) == 1, "Asymmetric native instrumentation"
    result = dict(status="NATIVE_FULLGAME_ABBA_COMPLETE", fullGameplayCoverage=False,
                  scope="Native schedule and completion evidence; network, scaling, visuals and physics acceptance require separate preflight proofs",
                  deferredChecks=capabilities()["deferredChecks"], scenarioSha256=state["scenarioSha256"], runs=runs,
                  performanceEligible=all(run["fingerprints"]["performanceEligible"] for run in runs),
                  attributionLimits="Tick/physics/driver/observer timings overlap; they must not be summed as independent costs")
    (directory / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    rows = []
    wave_rows = []
    for index, run in enumerate(runs, 1):
        ticks = run["statistics"]["wholeTick"]
        rows.append(f'<tr><td>{index} {run["variant"]}</td><td>{ticks["medianMs"]:.3f}</td>'
                    f'<td>{ticks["p99Ms"]:.3f}</td><td>{ticks["p999Ms"]:.3f}</td><td>{ticks["maxMs"]:.3f}</td>'
                    f'<td>{ticks["over60TpsBudget"]}</td></tr>')
        for wave in run["waves"]:
            latency = wave["latencyTicks"]
            wave_rows.append(f'<tr><td>{index} {run["variant"]}</td><td>{wave["tick"]}</td>'
                             f'<td>{html.escape(wave["type"])}</td><td>{wave["delivered"]}/{wave["planned"]}</td>'
                             f'<td>{max(latency) if latency else "immediate"}</td></tr>')
    deferred = ', '.join(html.escape(item["id"]) for item in result["deferredChecks"])
    page = ('<!doctype html><meta charset="utf-8"><title>KIAS Native FullGame ABBA</title>'
            '<style>body{font:16px system-ui;max-width:1100px;margin:32px auto;background:#17212b;color:#eee}'
            'td,th{padding:8px;border:1px solid #64748b}table{border-collapse:collapse}a{color:#7dd3fc}</style>'
            '<h1>Native FullGame ABBA</h1><p>Игровые результаты проверены. Итоговая готовность определяется отдельным строгим preflight.</p>'
            f'<p>Отложены, но необходимы: {deferred}.</p><p><a href="results.json">Полные доказательства, метрики и 100 самых тяжёлых тиков каждого прогона</a></p>'
            '<table><tr><th>Прогон</th><th>p50, мс</th><th>p99</th><th>p99.9</th><th>max</th><th>&gt;16.67 мс</th></tr>'
            + ''.join(rows) + '</table><h2>Волны</h2><table><tr><th>Прогон</th><th>Тик старта</th><th>Воздействие</th><th>Доставлено</th><th>Максимальная задержка, тики</th></tr>'
            + ''.join(wave_rows) + '</table>')
    (directory / "report.html").write_text(page, encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    result = report(parser.parse_args().directory)
    print(json.dumps({key: result[key] for key in ("status", "performanceEligible", "scope")}))
