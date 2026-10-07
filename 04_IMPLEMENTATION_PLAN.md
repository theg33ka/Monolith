# Ordered implementation plan — programmable controller integration

## Phase 0 — post-patch recon and baseline

- verify current `KIAS` HEAD and working tree;
- inspect actual first-patch changes;
- run current build/KIAS integration tests;
- inventory current protocol records/defaults/actions;
- inventory KIAS devices and current DeviceLink source/sink ports;
- produce/update a small parity table: device -> controller profile -> native ports -> DeviceLink fallback;
- confirm current access resolver API and power `SetLoad` API.

## Phase 1 — graph schema and compiler

- persistent program/node/wire records;
- schema version;
- stable node IDs;
- port type system;
- node definitions;
- validation limits;
- SCC/combinational cycle detection;
- compile into adjacency/index-friendly runtime form;
- unit tests first.

## Phase 2 — device profile registry

- stable `ProfileId` abstraction;
- port descriptor query/registry;
- map current KIAS devices to profiles;
- generic DeviceLink fallback = Signal;
- no giant prototype switch.

## Phase 3 — physical controller/programmer/rack

- controller item + supplied sprite;
- programming console with 1 slot;
- rack + supplied sprite + exactly 8 slots;
- dynamic power load;
- rack state/examine UI;
- lifecycle activation/deactivation.

## Phase 4 — server runtime

- event queue;
- constants/state;
- centralized timer/clock scheduler;
- SPECIFIC device subscriptions;
- evaluation budget/faults;
- clean boot/stop behavior.

## Phase 5 — ANY/ALL selectors

- `(grid, profile) -> devices` index;
- `ANY` and `ALL` nodes;
- merged output-event semantics;
- `ANY` deterministic first-available command routing;
- `ALL` broadcast routing;
- metadata outputs (`MatchedCount`, `HasAny`, `Source`) where clean;
- dynamic match updates from topology/device revisions;
- cross-ship portability tests.

## Phase 6 — DeviceLink bridge

- observe source-port invocation safely;
- direct sink invoke safely;
- preserve invoke/overload/loop protection;
- generic Signal ports;
- tests for loops and stale entities.

## Phase 7 — graph UI

- Robust UI canvas;
- pan/zoom/grid;
- nodes/ports/wires;
- palette/search/categories;
- SPECIFIC devices list;
- ANY profile picker;
- ALL profile picker;
- live matched count;
- node config;
- program name;
- validation panel;
- draft/WRITE/discard/eject safety.

## Phase 8 — expose KIAS devices

Implement profiles/ports for all currently accepted KIAS sensors/actuators, prioritizing those used by current default protocols.

Do not remove physical prerequisites. Use existing subsystem methods for actions.

## Phase 9 — migrate old protocols to graph presets

- inventory every active/default `KiasProtocolRecord` behavior;
- build graph preset equivalents;
- preset loader in programmer;
- route default/new gameplay automation through controller runtimes;
- retire/deprecate old policy executor;
- keep raw event producers/subsystem algorithms;
- add deterministic legacy import/migration or explicitly supported compatibility path;
- remove duplicate protocol editor workflow from normal gameplay.

## Phase 10 — persistence/security

- map save/load card program;
- specific binding remap on same saved map;
- portable selectors re-resolve on current rack grid;
- all BUI messages server-validated;
- use centralized KIAS access resolver/wire bypass;
- malicious cross-grid/profile/port IDs rejected.

## Phase 11 — perf/stress

- 8 controllers/rack;
- 100 nodes/controller;
- 200 wires/controller;
- selector-heavy graphs;
- 50/200 grids;
- burst events;
- topology churn;
- hard-off near-zero cost;
- inspect max/near-max tick, not only total elapsed.

## Phase 12 — docs/assets/build validation

- integrate supplied sprites into repo conventions;
- update KIAS architecture/player/validation/parity docs;
- add controller/third-party docs;
- compile, tests, YAML/localization/REUSE validation;
- final gameplay smoke scenarios.
