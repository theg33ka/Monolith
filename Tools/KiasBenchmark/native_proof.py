"""Validate dispatch and native completion evidence without certifying full coverage."""
import argparse
import hashlib
import json
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def lines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]


def validate(directory, scenario):
    start, end = read(directory / "started.json"), read(directory / "completed.json")
    inputs, events = lines(scenario), lines(directory / "events.jsonl")
    outcomes = lines(directory / "native-outcomes.jsonl")
    assert start["scenarioSha256"] == hashlib.sha256(scenario.read_bytes()).hexdigest(), "Scenario hash mismatch"
    assert end["status"] == "PARTIAL_NATIVE_DRY_RUN_COMPLETE", "Missing native completion"
    assert end["deliveredEvents"] == start["plannedEvents"] == len(inputs) == len(events), "Missing dispatch"
    assert end["ships"] == start["ships"] > 0, "Fleet mismatch"
    assert end["measuredTicks"] == start["measuredTicks"], "Duration mismatch"
    assert len({event["id"] for event in events}) == len(events), "Duplicate dispatch"
    assert len({outcome["id"] for outcome in outcomes}) == len(outcomes), "Duplicate native completion"
    pending = {}
    engine_ticks = {}
    for expected, actual in zip(inputs, events):
        assert (expected["id"], expected["ship"], expected["type"], expected["tick"]) == (
            actual["id"], actual["ship"], actual["type"], actual["scheduledTick"]), "Replay binding mismatch"
        assert actual["actualTick"] == expected["tick"], "Late synchronous dispatch"
        assert engine_ticks.setdefault(expected["tick"], actual["engineTick"]) == actual["engineTick"], "Same scheduled tick spans engine ticks"
        assert actual["nativeEffect"], "Empty native effect"
        if actual["nativeEffect"].get("asynchronous"):
            pending[actual["id"]] = actual
    assert set(pending) == {outcome["id"] for outcome in outcomes}, "Missing or unexpected native completion"
    assert end["nativeCompleted"] == len(outcomes), "Native completion counter mismatch"
    for outcome in outcomes:
        event = pending[outcome["id"]]
        assert outcome["ship"] == event["ship"] and outcome["type"] == event["type"], "Native binding mismatch"
        assert outcome["postconditionTrue"] is True and outcome["proof"], "Missing native proof"
        assert outcome["scheduledTick"] == event["scheduledTick"], "Changed native schedule"
        assert outcome["nativeStartedTick"] == event["engineTick"], "Changed native start"
        latency = outcome["nativeCompletedTick"] - outcome["nativeStartedTick"]
        assert latency == outcome["latencyTicks"] >= 0, "Invalid native latency"
        assert latency <= event["nativeEffect"]["deadlineTicks"], "Native completion exceeded deadline"
    log = (directory / "server.log").read_text(encoding="utf-8-sig", errors="replace")
    assert "[ERRO]" not in log and "[FATL]" not in log, "Server errors invalidate native proof"
    assert not (directory / "failed.json").exists() and not (directory / "run-failed.json").exists(), "Run marked failed"
    return dict(status="PASS", scope="Dispatch and completion integrity only; category coverage is separate",
                inputs=len(inputs), nativeCompletions=len(outcomes), types=sorted({event["type"] for event in events}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("scenario", type=Path)
    args = parser.parse_args()
    print(json.dumps(validate(args.directory, args.scenario), indent=2))
