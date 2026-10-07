# Target architecture — KIAS programmable controllers

## 1. Existing KIAS remains the physical/control plane

The controller subsystem is not another ship network. Existing KIAS owns:

- active grid/core lifecycle;
- DATA topology and Online device discovery;
- power/physical prerequisites;
- sensor algorithms;
- actuator subsystem APIs;
- access/security policy;
- device diagnostics.

Controllers add a **portable programmable policy/dataflow plane** on top.

## 2. Physical objects

### KIAS programmable controller

Portable item containing only persistent program data. It executes only inside an Online powered controller rack.

### KIAS controller programming console

One controller slot + full node editor. It edits a server-side draft and writes atomically to the card.

### KIAS controller rack

Powered KIAS device with exactly 8 real controller slots. It owns active runtimes and dynamic power load.

## 3. Program/runtime split

Persistent card data:

- program version/name;
- nodes;
- wires;
- node configs;
- positions;
- SPECIFIC bindings;
- ANY/ALL profile IDs.

Runtime only:

- compiled adjacency;
- queues;
- scheduled timers;
- volatile state;
- current matched selector devices;
- subscriptions;
- faults.

## 4. Typed graph

Port types:

- Signal
- Bool
- Number
- String
- Entity
- Enum/ContactDisposition

No implicit unsafe type conversion.

Node classes:

- lifecycle;
- constants;
- logic;
- control;
- timing;
- state;
- comparison;
- external device selectors/adapters.

## 5. External device addressing

### SPECIFIC

Binds to one physical device EntityUid/profile. Portable item keeps the reference/schema, but on another ship it becomes unavailable rather than rebinding.

### ANY(profile)

Dynamic set of all Online matching devices on the **current rack grid**.

- output events from any matching device merge into the node;
- command input is sent to one deterministic available matching device;
- `FirstAvailable` is the required baseline;
- portable across ships.

### ALL(profile)

Same dynamic match set.

- output events from any matching device merge into the node;
- command input/value is broadcast to all matching Online devices exactly once;
- portable across ships.

Selectors are valid even with zero matches.

## 6. Device profiles

Use a stable profile abstraction rather than raw prototype identity. One profile owns one port schema. Device variants can implement the same profile.

Suggested descriptor:

```text
ProfileId
DisplayName
PortDescriptors[]
Optional tags/capabilities
```

Suggested port descriptor:

```text
PortId
Direction
Type
Name/Description
Event-vs-state semantics
```

A central registry/resolver maps Online KIAS entities to profiles.

## 7. Selector indexes

Maintain indexes similar to:

```text
(grid, profileId) -> Online matching device set
(grid, deviceUid, portId) -> SPECIFIC subscribers
(grid, profileId, portId) -> ANY/ALL subscribers
```

Topology/device revisions update sets. Do not scan all devices for every event.

## 8. Event-driven runtime

```text
KIAS/DeviceLink event
 -> indexed graph endpoint(s)
 -> bounded controller queue
 -> node evaluation
 -> output propagation
 -> actuator adapter(s)
```

No whole-graph per-frame evaluation. Timers/clocks use a central scheduler/buckets/heap.

## 9. Compile/validation

Compile validates:

- node IDs/kinds;
- ports/directions/types;
- limits;
- specific binding metadata;
- selector profile IDs;
- configs;
- pure combinational cycles.

Feedback is allowed only through state/time-breaking nodes.

Runtime has an evaluation budget and faults locally instead of crashing the server.

## 10. Device I/O boundary

A `KiasControllerIoSystem` (or equivalent) is the only normal bridge between graph runtime and gameplay devices.

It should:

- receive typed emissions from KIAS sensors;
- expose DeviceLink source events where appropriate;
- validate actuator commands;
- invoke existing subsystem APIs;
- enforce same-grid/Online/security rules.

## 11. Protocol architecture after migration

Legacy `KiasProtocolSystem` policy execution is retired/deprecated after migration.

Keep:

- event production;
- PDC threat tracking/interception;
- power/network calculations;
- sensor detection;
- safe actuator APIs.

Replace:

- `trigger -> conditions -> actions` policy engine;
- protocol-specific hidden/default actions;
- a second separate protocol editor.

With:

- normal graph programs;
- prototype-backed presets;
- physical cards in racks.

## 12. Default portable patterns

Default protocol/preset graphs should favor type selectors:

```text
ANY WeaponFlashDetector
 -> hostile compare
 -> IF
 -> ALL Speaker
 -> ALL/filtered EmergencyLightController
 -> ALL DefenceController.Automatic
```

This is the key portability property: changing shuttle/device instances must not require reprogramming generic automation.

## 13. Access/security

Programming/rebinding/WRITE uses the centralized KIAS access model from the previous patch. Runtime resolution always stays on the rack's current grid.

Cut access wire remains the normal physical bypass where applicable. The graph system must not introduce a new ownership backdoor.

## 14. Power/lifecycle

Rack load is dynamic: base + per inserted controller.

A controller runtime exists only while:

- card is in rack;
- rack is powered;
- rack is KIAS Online;
- grid/core is active.

On stop: clear queue, cancel timers, unregister subscriptions, clear volatile state. On resume: cold boot + ON START.
