# Repository findings — controller/graph integration pass

## Status of this document

The original repo research pack and the first corrective KIAS patch have already been used. This revision is the handoff for the **programmable controller / graph** pass. The coding agent must re-open the current branch before assuming any path is unchanged.

Historical base research snapshot: `Forge-Station/Monolith main b069ad386d7599ac6b51d9dfcdfba9b6a311f676` (2026-10-06). A later KIAS snapshot inspected during graph planning already had `KiasSystem`, `KiasGridComponent`, `KiasProtocolSystem`, device roles, KIAS events, external sensors and DeviceLink integration. Current local HEAD may be newer.

## KIAS topology/runtime — reuse, do not replace

Known relevant paths from the KIAS branch:

- `Content.Shared/_Forge/KIAS/KiasComponents.cs`
- `Content.Shared/_Forge/KIAS/KiasTopology.cs`
- `Content.Shared/_Forge/KIAS/KiasProtocols.cs`
- `Content.Server/_Forge/KIAS/KiasGridComponent.cs`
- `Content.Server/_Forge/KIAS/KiasSystem.cs`
- `Content.Server/_Forge/KIAS/KiasProtocolSystem.cs`
- `Content.Server/_Forge/KIAS/KiasHullSystem.cs`
- `Content.Server/_Forge/KIAS/KiasExternalSensorSystem.cs`
- `Content.Server/_Forge/KIAS/KiasPowerSystem.cs`

Findings from that snapshot:

- `KiasGridComponent` already caches `Devices`, `Cables`, `Online`, roles and a topology object;
- topology is rebuilt when dirty rather than packet-simulated along every cable;
- `KiasSystem.IsOnline(device)` is the correct starting primitive for controller device discovery;
- sensor systems already emit concrete KIAS events;
- current protocol system is an existing `trigger -> conditions -> actions` rule engine and therefore becomes the main migration target of this pass.

The graph subsystem should sit **on top of** the existing KIAS physical/data topology, not build another network.

## Access model — original finding is superseded

The first research pack said `ShipOwnershipComponent.OwnerUserId` was the owner authority. The first corrective patch explicitly replaced that simplistic model. For this pass:

- use the **current centralized KIAS access resolver** implemented in the branch;
- preserve deed/ID/company/POI/open-claim precedence and standard wire bypass from the previous patch;
- programmer/rack WRITE/rebind/security operations must not reintroduce direct `OwnerUserId` checks.

If the current branch does not contain the expected resolver, stop and reconcile with the applied first-patch context in `history/01_FIRST_PATCH_PROMPT_APPLIED.md` before coding controllers.

## DeviceLinking — reuse as external machine bridge

Paths:

- `Content.Shared/DeviceLinking/DeviceLinkSourceComponent.cs`
- `Content.Shared/DeviceLinking/DeviceLinkSinkComponent.cs`
- `Content.Shared/DeviceLinking/SharedDeviceLinkSystem.cs`
- `Content.Server/DeviceLinking/Systems/DeviceLinkSystem.cs`
- `Content.Shared/DeviceLinking/DevicePortPrototype.cs`
- `Content.Client/NetworkConfigurator/NetworkConfiguratorLinkMenu.xaml.cs`

Important behavior:

- source/sink ports are prototype-defined;
- `InvokePort()` supports `NetworkPayload`;
- boolean logic state is carried in payload for logic-capable devices;
- local non-device-network links raise `SignalReceivedEvent`;
- DeviceLink already has invoke/overload protection and link bookkeeping;
- the existing NetworkConfigurator UI is useful as a visual reference for port linking, but its Bezier drawing implementation should not be copied naively.

Recommendation: DeviceLink is the **boundary adapter**, not the graph's internal wire representation.

## Existing SS14 logic/timer semantics

Known local paths:

- `Content.Server/DeviceLinking/Components/LogicGateComponent.cs`
- `Content.Server/DeviceLinking/Systems/LogicGateSystem.cs`
- `Content.Server/DeviceLinking/Components/SignalTimerComponent.cs`
- `Content.Server/DeviceLinking/Systems/SignalTimerSystem.cs`

Existing `LogicGateSystem` already implements OR/AND/XOR/NOR/NAND/XNOR semantics. `SignalTimerSystem` provides established timer behavior. These are good behavior references, but controller logic nodes should be lightweight runtime objects/records, not hidden world entities.

## Dynamic power load support already exists

Known local path:

- `Content.Shared/Power/EntitySystems/SharedPowerReceiverSystem.cs`
- server `PowerReceiverSystem` override.

`SharedPowerReceiverSystem.SetLoad(...)` exists in this codebase. Use it to make the rack load depend on inserted controller count. Do not create a fake watt counter detached from the power net.

## /tg/station Wiremod — best UX/dataflow reference

Reference areas:

- `code/modules/wiremod/core/integrated_circuit.dm`
- `code/modules/wiremod/core/component.dm`
- `code/modules/wiremod/core/port.dm`
- `code/modules/wiremod/core/duplicator.dm`
- `tgui/packages/tgui/interfaces/IntegratedCircuit/*`

Useful design patterns:

- typed input/output ports;
- component-relative X/Y;
- persistent connections;
- output fan-out;
- pan/zoom editor;
- drag-and-drop palette;
- save/load graph data;
- explicit component UI data.

Do not port DM/React runtime literally.

## Goob nested filters — reference, not dependency by default

Relevant paths from Goob:

- `Content.Goobstation.Shared/Factory/Filters/CombinedFilterComponent.cs`
- `Content.Goobstation.Shared/Factory/Filters/AutomationFilterSystem.cs`

Combined filters are useful conceptual reference for recursive AND/OR/XOR/NAND/NOR/XNOR predicates. They are not required for the base controller runtime. Only reuse minimal code if an explicit entity-filter node becomes necessary.

## New core requirement — portable type selectors

The major addition to the previous plan is `ANY` / `ALL` device-type selectors.

A controller device must have a stable **controller profile** independent of a specific EntityUid. Examples:

- `WeaponFlashDetector`
- `Speaker`
- `PdcRadar`
- `DefenceController`
- `Relay`
- `RoomScanner`

A profile exposes one stable port schema. Variants/prototypes of the same gameplay device should map to the same profile where practical.

This allows a graph to contain:

```text
ANY WeaponFlashDetector -> ... -> ALL Speaker
```

with no specific device UIDs. That card must remain functional after moving to another KIAS ship.

## ALL/ANY exact distinction

**Both** selectors receive/merge output events from every matching Online device, because a sensor event from any physical unit needs to be able to enter the graph.

Their difference is primarily command fan-out:

- `ANY` input/command -> one deterministic matching Online device (`FirstAvailable` baseline);
- `ALL` input/command -> every matching Online device exactly once.

Selectors should expose metadata such as matched count and source entity where useful.

## Existing protocols become graph presets

The current KIAS protocol engine must not remain a second permanent automation language.

Keep raw event producers and low-level subsystem algorithms. Replace policy/action rules with normal controller graphs and a data/prototype preset library. Default automation should use ANY/ALL selectors so it is portable.

Heavy algorithms such as PDC interception remain optimized subsystem code; the graph controls/enables them rather than reproducing projectile physics node-by-node.

## Licenses / attribution

Known reference license picture from research:

- `/tg/station` code: AGPL v3;
- Goob code: AGPL-3.0-or-later with repo REUSE metadata;
- WizDen SS14 code: MIT upstream;
- Forge/Monolith: project REUSE/mixed-history policy; follow per-file metadata.

Before literal adaptation, inspect the exact source file header/history. Prefer native implementation from concepts. Update `Docs/KIAS/THIRD_PARTY.md` for any substantive external code use.
