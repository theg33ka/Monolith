import csv
import tempfile
import unittest
from pathlib import Path

from fullgame_report import tick_statistics, wave_statistics
from fullgame_scenario import capabilities, generate
from world_smoke_scenario import DURATIONS, GROUPS


class FullGameEvidenceTests(unittest.TestCase):
    def test_background_is_seeded_and_does_not_overlap_composite_restores(self):
        first = generate(40, 45000, 7)
        self.assertEqual(first, generate(40, 45000, 7))
        self.assertNotEqual(first, generate(40, 45000, 8))
        self.assertEqual(len(first), len({item['id'] for item in first}))
        self.assertTrue(all(0 <= item['tick'] < 45000 for item in first))
        background = [item for item in first if not item['synchronous']]
        self.assertEqual({item['ship'] for item in background}, {f'ship-{i:04d}' for i in range(1, 41)})
        self.assertTrue(all(1800 <= item['tick'] - int(item['wave'].split('-')[1]) < 3120 for item in background))

    def test_owner_deferrals_preserve_collision_and_geometry(self):
        inputs = generate(2, 45000)
        types = {item["type"] for item in inputs}
        deferred = {kind for item in capabilities()["deferredChecks"] for kind in item["events"]}
        self.assertFalse(types & deferred)
        self.assertTrue({"collision.damage_threshold", "geometry.breach", "pdc.empty", "crew.death"} <= types)

    def test_combined_systems_retains_every_event(self):
        expected = sum(len(GROUPS[name]) for name in ("fanout", "crew_power", "gun"))
        self.assertEqual(len(GROUPS["systems"]), expected)
        self.assertLess(max(tick for tick, _ in GROUPS["systems"]), DURATIONS["systems"])

    def test_wave_rejects_split_engine_ticks(self):
        inputs = [dict(id=str(i), ship=str(i), tick=120, type="crew.death", wave="death") for i in range(2)]
        events = [dict(id=str(i), actualTick=120, engineTick=500+i, nativeEffect={"asynchronous": True}) for i in range(2)]
        with self.assertRaisesRegex(AssertionError, "span engine ticks"):
            wave_statistics(inputs, events, [], 2)

    def test_wave_rejects_missing_ship(self):
        with self.assertRaisesRegex(AssertionError, "Incomplete fleet wave"):
            wave_statistics([dict(id="x", ship="1", tick=120, type="gun.fire", wave="gun")], [], [], 2)

    def test_tick_evidence_rejects_nan_and_missing_samples(self):
        columns = ("relative_tick", "whole_tick_ms", "physics_ms", "driver_ms", "observer_ms",
                   "whole_allocated_bytes", "thread_allocated_bytes", "gc_pause_ms")
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "ticks.csv"
            with path.open("w", newline="") as stream:
                writer = csv.DictWriter(stream, columns)
                writer.writeheader()
                writer.writerow({name: "nan" if name == "whole_tick_ms" else 0 for name in columns})
            with self.assertRaisesRegex(AssertionError, "Invalid tick metric"):
                tick_statistics(path, 1)
            with self.assertRaisesRegex(AssertionError, "Missing tick samples"):
                tick_statistics(path, 2)


if __name__ == "__main__":
    unittest.main()
