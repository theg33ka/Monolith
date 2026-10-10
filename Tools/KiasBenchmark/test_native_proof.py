import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from native_proof import validate


class NativeProofTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.scenario = self.root / "scenario.jsonl"
        self.write("scenario.jsonl", dict(id="one", ship="ship-0001", type="ftl.visitor_in", tick=120))
        self.write("started.json", dict(scenarioSha256=hashlib.sha256(self.scenario.read_bytes()).hexdigest(),
                                       plannedEvents=1, ships=1, measuredTicks=1000))
        self.write("completed.json", dict(status="PARTIAL_NATIVE_DRY_RUN_COMPLETE", deliveredEvents=1,
                                         ships=1, measuredTicks=1000, nativeCompleted=1))
        self.event = dict(id="one", ship="ship-0001", type="ftl.visitor_in", scheduledTick=120,
                          actualTick=120, engineTick=1922, nativeEffect=dict(asynchronous=True, deadlineTicks=300))
        self.outcome = dict(id="one", ship="ship-0001", type="ftl.visitor_in", scheduledTick=120,
                            nativeStartedTick=1922, nativeCompletedTick=2000, latencyTicks=78,
                            postconditionTrue=True, proof=dict(actualConsoleFtlCompleted=True))
        self.write("events.jsonl", self.event)
        self.write("native-outcomes.jsonl", self.outcome)
        (self.root / "server.log").write_text("[INFO] native complete\n")

    def write(self, name, value):
        (self.root / name).write_text(json.dumps(value) + "\n", encoding="utf-8")

    def test_valid_native_completion(self):
        self.assertEqual(validate(self.root, self.scenario)["nativeCompletions"], 1)

    def test_missing_completion_rejected(self):
        (self.root / "native-outcomes.jsonl").write_text("")
        with self.assertRaisesRegex(AssertionError, "Missing or unexpected"):
            validate(self.root, self.scenario)

    def test_late_dispatch_rejected(self):
        self.event["actualTick"] += 1
        self.write("events.jsonl", self.event)
        with self.assertRaisesRegex(AssertionError, "Late synchronous"):
            validate(self.root, self.scenario)

    def test_null_proof_rejected(self):
        self.outcome["proof"] = None
        self.write("native-outcomes.jsonl", self.outcome)
        with self.assertRaisesRegex(AssertionError, "Missing native proof"):
            validate(self.root, self.scenario)

    def test_completion_after_deadline_rejected(self):
        self.outcome.update(nativeCompletedTick=2300, latencyTicks=378)
        self.write("native-outcomes.jsonl", self.outcome)
        with self.assertRaisesRegex(AssertionError, "exceeded deadline"):
            validate(self.root, self.scenario)

    def test_server_error_rejected(self):
        (self.root / "server.log").write_text("[ERRO] runtime failure\n")
        with self.assertRaisesRegex(AssertionError, "Server errors"):
            validate(self.root, self.scenario)


if __name__ == "__main__":
    unittest.main()
