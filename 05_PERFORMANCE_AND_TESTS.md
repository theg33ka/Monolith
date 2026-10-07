# Performance, risks and acceptance — controller graphs

## Scaling target

Assume ~200 KIAS-equipped shuttle grids. Most are idle. Graph support must preserve the original rule: idle/hard-off KIAS is cheap.

## Forbidden complexity

- frame × controller × node;
- frame × selector × every device;
- event × full graph scan;
- event × full grid device scan;
- per-node ECS entity;
- one independent `Update()` per Timer/Clock node;
- full BUI graph state spam every tick.

## Required indexes

Prefer explicit registries:

```text
active racks
active controllers
(grid, profileId) -> Online devices
(grid, deviceUid, portId) -> SPECIFIC subscribers
(grid, profileId, portId) -> ANY/ALL subscribers
scheduled timers/clocks -> centralized scheduler
```

Hard KIAS OFF or rack offline removes its controllers from active registries/subscriptions.

## ANY/ALL hot-path risk

The selector feature must not mean “on every detector event, scan every device and ask its type”. Resolve membership on topology/device/power changes and keep cached sets.

On output event from a device:

1. identify its current profile(s) cheaply;
2. dispatch to profile-port subscribers;
3. include source UID in runtime event metadata;
4. no allocation-heavy global enumeration.

On ALL actuator command, iterate only the cached matching Online set for that profile/filter.

On ANY command, pick from the cached set in deterministic stable order. If first disappears, next valid device becomes selection without graph recompilation.

## Timer scheduling

Use central scheduler/buckets/min-heap. Stagger clocks/timers naturally. Never synchronize hundreds of clocks simply because all controllers booted in the same tick if jitter/bucketing can avoid a spike without changing semantics materially.

Exact one-shot timers should preserve requested duration within normal simulation tolerance.

## UI

The editor may be large but only exists for the active user/session. Avoid sending every runtime value every tick.

Push:

- structural draft changes;
- validation results;
- device/profile match count changes;
- optional low-rate debug values only while diagnostics are enabled.

Rack UI stays tiny/bounded.

## Protocol migration risk

The biggest correctness risk is accidentally leaving both engines active:

- old `KiasProtocolSystem` performs action;
- graph preset performs same action;
- player gets duplicate sirens/PDC toggles/messages.

Acceptance requires proving that normal default automation has one active policy path.

Raw event sources may continue to be shared.

## Persistence risk

Specific entity bindings must save/load safely on the same map but never become cross-grid wildcard matches after moving a physical card.

ANY/ALL persist only stable profile/filter config, never matched runtime UIDs.

## Core acceptance checklist

### Physical

- controller item uses provided sprite;
- rack uses provided sprite;
- programmer separate from rack;
- rack = 8 slots exactly;
- 9th rejected;
- dynamic real power load;
- no execution outside active rack.

### Runtime

- power/DATA/KIAS OFF stop immediately;
- timers/queues cleared;
- cold boot on resume;
- two controllers never share volatile state;
- 8 cards run independently;
- evaluation budget faults only the offending controller.

### SPECIFIC

- exact device works;
- stale/deleted target safe;
- transfer to another ship -> unavailable;
- no name/prototype auto-rebind.

### ANY

- zero matches = valid no-op;
- event from each of multiple matching sensors reaches graph;
- command reaches one matching Online device;
- deterministic selection;
- failover after target offline/removal;
- transferred card automatically works with same profile on new ship.

### ALL

- zero matches = valid no-op;
- event from each matching sensor reaches graph;
- command reaches all Online matches once;
- offline excluded;
- topology update changes set without recompile;
- transferred card automatically works with new ship matches.

### Graph correctness

- logic truth tables;
- IF/ELSE;
- timer/clock;
- state nodes;
- comparisons;
- type mismatch rejected;
- cycle detection;
- stateful feedback allowed;
- malformed IDs/config rejected.

### DeviceLink

- source -> graph;
- graph -> sink;
- existing overload/loop protection intact.

### Protocol migration

- every current default/accepted protocol mapped to graph preset;
- presets visible/editable in node editor;
- default portable graphs use ANY/ALL where possible;
- no duplicate old+new actions;
- legacy data has tested migration/import path;
- disabling/removing the controller stops that automation: there is no hidden hardcoded duplicate.

## Stress scenarios

Record total elapsed + allocations + worst/near-worst update duration where harness permits:

- one rack, 8 cards, 800 total nodes;
- 50 ships with selector-heavy basic warnings;
- 200 idle-active ships;
- 200 hard-off ships;
- event burst from weapon flashes/hull impacts;
- topology/power flapping;
- mass rack boot;
- 0 matching devices vs many matching devices;
- migrated default Battle Alert across multiple ships.
