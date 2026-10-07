# KIAS — programmable controller / graph integration pack

This is the **next implementation handoff** after the original KIAS master implementation and the first corrective patch/pass were reported as successfully applied.

The target is a physical PLC/Wiremod-style automation layer integrated into the existing KIAS:

- removable programmable controller cards;
- a separate programming console;
- a powered 8-slot controller rack;
- native Robust UI node editor;
- strict typed ports;
- SPECIFIC device bindings;
- portable `ANY <device type>` and `ALL <device type>` selector nodes;
- event-driven server runtime;
- migration of current hardcoded KIAS protocols into ordinary editable graph presets.

## Critical new portability rule

A controller may address a **device profile**, not only a concrete entity.

`ANY WeaponFlashDetector` merges trigger events from any detector on the current KIAS grid and sends commands to one deterministic available detector/device.

`ALL Speaker` merges events from all matching devices and broadcasts incoming commands to every matching Online speaker.

Because ANY/ALL persist only a stable profile ID, a generic card can be moved to another shuttle and continue to work without rebinding. SPECIFIC nodes deliberately do not auto-rebind.

## Files

- `00_AGENT_MASTER_PROMPT.md` — **give this to the coding agent for the new pass**.
- `01_REPO_FINDINGS.md` — updated repo/reuse findings and superseded assumptions.
- `02_TARGET_ARCHITECTURE.md` — controller architecture.
- `03_DEVICE_PROTOCOL_SPEC.md` — device profiles/ports and graph preset behavior.
- `04_IMPLEMENTATION_PLAN.md` — ordered implementation phases.
- `05_PERFORMANCE_AND_TESTS.md` — selector/runtime performance rules and acceptance.
- `06_REPO_SOURCE_MAP.md` — quick path/reference map.
- `07_CONTROLLER_GRAPH_SPEC.md` — precise graph + ANY/ALL semantics.
- `08_PROTOCOL_MIGRATION.md` — how to remove hardcoded protocol policy and replace it with graph presets.
- `09_THIRD_PARTY_REFERENCES.md` — license/attribution checklist.
- `assets/` — the two supplied game sprites, raw and convenience RSI wrappers.
- `history/` — the already-applied initial master prompt and first patch prompt for context only.

## Non-negotiables

- Do not rebuild/replace existing KIAS topology, access, sensors, PDC or physical prerequisite systems when they can be reused.
- Preserve the corrected KIAS access model and standard wire hacking from the first patch.
- Controller execution is server-authoritative and event-driven.
- No per-node ECS entities or per-node `Update()` loops.
- Rack has exactly 8 slots and real dynamic power use.
- ANY/ALL matching never crosses the rack's current grid.
- Portable default automations use ANY/ALL instead of concrete UIDs wherever possible.
- Existing KIAS protocols are migrated to graphs; do not leave a second hidden rule engine active in parallel.
- Heavy subsystem algorithms (PDC interception, sensor physics, etc.) remain in optimized systems and are orchestrated by graphs.
- Target scaling remains roughly 200 KIAS-equipped grids.
