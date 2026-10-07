# KIAS controller graph specification

## Design goal

The graph system is a small deterministic PLC/dataflow runtime, not a general scripting language. It must be understandable through a node editor, safe for untrusted player-authored graphs, portable between ships when desired, and cheap when idle.

## Node identity

Every node has a stable integer/Guid-like program-local ID. Wire endpoints address node ID + stable port ID, never list index.

## Port model

Direction: Input/Output.

Types: Signal, Bool, Number, String, Entity, Enum.

Signal carries a pulse and optional runtime metadata. Data values are strongly typed. A node may keep last runtime value, but persistent card data never stores arbitrary live entity state.

## Universal runtime event metadata

Where useful, internal dispatch may carry:

- source device UID;
- source profile ID;
- source port ID;
- timestamp/tick;
- typed payload.

This metadata allows ANY/ALL nodes to merge sensor streams without losing the identity of the physical source. Only expose selected metadata to graph ports; do not create a dynamic scripting object.

## Device selector nodes

### SPECIFIC(profile, binding)

One physical device. Port schema snapshot remains visible if unavailable.

### ANY(profile)

Match all Online devices on current rack grid implementing profile.

Output side:

- every matching device emission for a profile output port is forwarded through that ANY node output;
- `Source` is updated/emitted with the physical source;
- persistent data values represent the latest emission seen at that selector, not an aggregate over all devices unless the port/node explicitly defines an aggregation.

Input side:

- one matching Online device receives the command;
- baseline picker is deterministic `FirstAvailable`;
- stable ordering can be based on a reproducible per-grid/device key available in the engine; do not rely on dictionary iteration order;
- if chosen device becomes invalid before execution, retry selection once against current cached set or no-op safely.

### ALL(profile)

Output side is the same merged event stream principle: a signal from any matching physical device can enter the graph.

Input side broadcasts to every matching Online device exactly once for that graph emission.

For values that do not make semantic sense as a simultaneous aggregate, do not invent hidden sum/AND/OR behavior. The output is an event/value stream from individual sources. Separate aggregator nodes may be added later.

## Why ANY and ALL both merge sensor events

The user-facing distinction is targeting/fan-out, not suppressing sensors. A graph `ALL WeaponFlashDetector.Triggered` still needs to react when any one detector fires. `ALL` becomes materially different when the graph writes/commands a port: it fans out to every matching detector/actuator.

## Selector match lifecycle

Membership changes on:

- KIAS topology revision;
- device startup/shutdown/delete;
- grid/parent move;
- power status/Online status change;
- rack grid change.

Membership does not persist on the card.

## Optional selector filters

Architecture should allow a future selector key:

```text
ProfileId + optional Room + optional Group + optional Tag
```

Default is all devices of profile. If current KIAS already has stable room/group metadata and filter support is cheap, include it in v1 because it makes presets such as “all emergency lights” practical.

## Internal nodes

### Combinational

- AND/OR/XOR/NOT/NAND/NOR/XNOR
- comparisons

### Control

IF/ELSE consumes Trigger + Condition and emits True/False Signal.

### Time/state breaking

- Timer/Delay
- Clock
- Latch/Memory
- Toggle
- Counter
- Edge Detector

These break combinational cycles and hold volatile runtime state only.

## Compilation

Compiler resolves node definitions and ports into compact arrays/dictionaries. It creates outgoing-edge tables so runtime propagation is O(edges actually traversed), not O(total graph).

It also precomputes:

- specific external endpoints;
- selector endpoint keys;
- timer/state node tables;
- source/sink type checks.

## Validation errors vs warnings

Blocking:

- unknown node/profile/port;
- duplicate node ID;
- bad wire endpoint;
- output->output/input->input;
- type mismatch;
- forbidden multiple data sources;
- pure combinational cycle;
- limits exceeded;
- invalid config values.

Warnings:

- specific target currently unavailable;
- ANY/ALL currently matches zero devices;
- optional subsystem/profile not currently present.

A portable program should be writeable while some matching devices are absent.

## Execution budget

Every external emission starts/joins a bounded work queue. A controller should have a configurable maximum evaluations per logical event/batch. Exceeding the budget marks the card runtime FAULT and records a useful diagnostic; it must not cascade to other cards/racks.

## Determinism

For the same event ordering and device set:

- internal node evaluation order should be deterministic;
- ANY FirstAvailable selection should be deterministic;
- output fan-out ordering should not change observable behavior where possible.

Random routing is optional future functionality, not baseline semantics.

## Debugging

Editor/rack diagnostics should be able to show:

- compiled/invalid;
- active/offline/fault;
- matched count for selectors;
- unavailable specific targets;
- last fault reason;
- optionally low-rate last port values while editor diagnostics mode is enabled.

Do not stream every port value permanently.
