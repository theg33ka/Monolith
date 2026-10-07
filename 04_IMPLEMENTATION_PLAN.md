# Ordered implementation plan — correctness/UI pass

## Phase 0 — reproduce first

Create minimal failing tests/smokes for:

- card name after WRITE;
- controller -> Speaker;
- ALL Speaker broadcast;
- integrated light OFF -> ON;
- ALL Lighting vs ALL LightGroupController distinction;
- AirAlarm DeviceList multitool workflow;
- real atmos danger -> KIAS preset path.

Do not start with cosmetic refactor before correctness failures are captured.

## Phase 1 — card metadata + speaker correctness

Small isolated fixes first. Add regression tests.

## Phase 2 — integrated control-plane power semantics

Introduce the minimal distinction needed for KiasIntegrated endpoints. Re-run native KIAS power/off tests to prove normal devices still go offline when expected.

## Phase 3 — lighting profile split

Add direct Lighting capability and rename existing LightController profile for user-facing semantics. Update presets only where they intentionally target group controllers. Verify old saved cards/presets migrate or keep stable IDs cleanly.

## Phase 4 — restore DeviceList workflow

Reproduce why AirAlarm cannot be configured through normal list workflow. Fix the smallest relevant layer. Preserve port-link mode and generic DeviceLink behavior.

## Phase 5 — graph schema/help

Add port descriptions/domains and any versioned metadata needed. Avoid rewriting runtime value representation unless necessary.

## Phase 6 — inspector + canvas readability

Capability-driven inspector, inline configured values, tooltips, wire incompatibility feedback, selector filter labels.

## Phase 7 — KIAS visual design pass

Create reusable section/status/field UI helpers/styles. Apply to programmer, rack, management/local/service windows. Use ShuttleConsole visual hierarchy as an in-repo quality reference, not a literal skin copy.

## Phase 8 — localization

Audit all KIAS player-facing prototypes and graph strings. `KIAS` remains Latin. Add concise `.desc`.

## Phase 9 — end-to-end validation

Automated suites + live client smoke + screenshots. Only after this update `Docs/KIAS/VALIDATION.md` with measured actual results.
