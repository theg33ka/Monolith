# Validation, performance and UI acceptance

## Rule: correctness first, no fake green status

This document defines tests that must pass. Do not pre-fill pass counts before the implementation run.

## Automated correctness matrix

### Controller card

- draft rename does not rename item;
- WRITE renames item;
- description correct;
- eject/reinsert retains identity;
- map save/load retains name/program consistency.

### Speaker

- StringConstant -> Message + OnStart -> Announce produces actual local speech;
- Alarm path works;
- ALL with 2+ speakers reaches every target exactly once;
- cooldown still suppresses true repeats appropriately;
- unrelated speaker groups are not accidentally addressed.

### Lighting

- profile discovery distinguishes direct fixtures and group controllers;
- ALL Lighting controls two or more integrated lights;
- ANY Lighting controls one deterministic fixture;
- group controller affects only lights in its group;
- integrated fixture OFF remains graph-addressable and can be ON again;
- physically disconnected DATA path still makes endpoint unavailable.

### NetworkConfigurator / AirAlarm

- list mode can save AirSensor + GasVent + GasScrubber;
- applying list updates AirAlarm DeviceList;
- AirAlarm actually receives/syncs devices through DeviceNetwork;
- link mode still links source/sink ports;
- KIAS generic DeviceLink bridge still works;
- non-KIAS list configuration regression test included.

### Atmosphere preset

Use the real chain as far as practical:

- configured AirAlarm knows sensor;
- sensor enters danger threshold / emits normal atmos network alert;
- AirAlarm becomes Danger and raises normal event;
- KIAS receives AtmosDanger;
- installed `atmosphere` preset executes;
- clear event occurs on recovery.

A test that calls `KiasProtocolSystem.Trigger(AtmosDanger)` directly is not sufficient for this regression.

## Graph editor automated UI tests

Extend existing `KiasControllerLayoutTests` or split focused tests.

Run at least:

- 850x500;
- 1200x720;
- 1600x900.

Cases:

- empty graph;
- graph with constants, logic, ANY/ALL/SPECIFIC, long names;
- selected node of every internal configurable category;
- selected external Lighting/Speaker profile;
- very long Russian strings.

Assertions:

- finite/positive DesiredSize;
- canvas remains usable;
- palette and inspector stay within their viewport;
- inspector horizontal scrolling disabled;
- no generic fields visible for unrelated node kinds;
- node summary text fits/truncates intentionally;
- every visible port has non-empty localized name and meaningful description;
- no raw `Input Set: Bool` style output;
- long names ellipsize/wrap and provide tooltip;
- `Lighting` and `LightGroupController` are distinct palette items;
- wire mismatch produces UI feedback instead of silent no-op.

## Localization audit test

Add a data-driven validation over graph profiles/prototypes:

- every player-facing KIAS entity has ru-RU name and description;
- `KIAS` is Latin in Russian strings;
- graph node/port/profile labels resolve;
- each native controller port description is not the generic placeholder;
- typed enum values resolve to localized labels.

## Live UI smoke — required

Automated `Measure/Arrange` is necessary but not enough.

Launch a local game using the repo's normal workflow and inspect:

1. Programmer with 8–12 nodes and wires.
2. Programmer at minimum practical window size.
3. Right inspector for String, Timer, ALL Lighting, Speaker.
4. Rack with empty/mixed/running/fault slots.
5. Service multitool in Group mode.
6. Light group controller local UI.
7. Management console.

Capture screenshots locally into `.kias/ui-smoke/` or another ignored folder and note resolution/UI scale. Compare readability to the shuttle console reference: section hierarchy, spacing, labels, button grouping, contrast.

Fail the task if:

- fields overlap;
- horizontal inspector scrollbar appears;
- important text is clipped with no tooltip;
- raw technical identifiers dominate normal player UI;
- controls are present but their purpose is not understandable without source code.

## Performance regression

Do not turn UI/correctness pass into per-frame scans.

Re-run existing KIAS fleet/stress tests after selector/profile changes. Direct-light discovery must remain indexed/cached on topology changes, not scan all lights per graph event.
