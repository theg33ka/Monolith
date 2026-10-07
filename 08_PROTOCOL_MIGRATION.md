# Migration: KIAS protocols -> controller graphs

## Goal

The controller graph becomes the **only normal player automation policy layer**. Existing KIAS protocol records are a legacy representation to migrate, not a second permanent engine.

## Preserve low-level systems

Do not confuse “remove hardcoded protocols” with “delete subsystem logic”. Keep:

- sensor detection;
- KIAS raw events;
- topology;
- power stats;
- crew tracking;
- PDC projectile index/trajectory/interception;
- FireControl integration;
- IFF classification with physical prerequisites;
- safe speaker/relay/light/suppression/navigation actuator APIs;
- anti-spam/coalescing where it belongs to the output subsystem itself.

These are capabilities, not policies.

## Remove/deprecate policy execution

After migration, no normal path should independently iterate legacy protocol records and execute actions that a graph also executes.

Avoid this failure:

```text
WeaponFlash event
 -> old KiasProtocolSystem announces
 -> graph preset announces
 => duplicate speech/siren/PDC action
```

## Preset representation

Use a data/prototype-backed graph template format, e.g. `KiasControllerProgramPrototype`.

A preset is ordinary graph data:

- nodes;
- positions;
- wires;
- defaults/config;
- no hidden C# action list.

Programming console `NEW FROM PRESET` copies it into card draft. After that it is just a normal editable program.

## Portability rule

Default presets should use `ANY` sensor selectors and `ALL` actuator selectors wherever sensible.

They may use selector filters (group/room/tag) when needed. Only use SPECIFIC bindings when the behavior truly depends on a particular physical machine chosen by the shipbuilder/player.

## Expected presets

The coding agent must enumerate the current branch's actual defaults. Expected baseline from the project spec:

- Battle Alert
- Point Defence orchestration
- Contact
- Decompression
- Fire
- Emergency Power
- Anomaly Growth
- Autopilot Arrival
- Critical Vessel
- Medical Assistance
- MAYDAY
- any other accepted/default KIAS automations found in current code/docs.

## Example migration mapping

Legacy:

```text
Trigger: WeaponFlash
Disposition: Hostile
Actions:
  Announce(message)
  Lights(group=EMERGENCY, true)
  Pdc(true)
  Record(message)
```

Graph:

```text
ANY WeaponFlashDetector.Triggered
ANY WeaponFlashDetector.Disposition -> [== Hostile] -> IF.Condition
Triggered -------------------------------> IF.Trigger
IF.True -> ALL Speaker.Announce
         -> ALL/filtered LightController.On
         -> ALL DefenceController.Enable
         -> ALL Recorder.Record
String Constant(message) -> Speaker.Message / Recorder.Message
```

The exact graph may use cleaner shared signal wiring or helper nodes, but every action must be visible/editable.

## Stateful policies

Legacy cooldown/debounce/threshold semantics become explicit nodes/config:

- cooldown -> Timer/Latch/Edge pattern or dedicated Cooldown node if a small reusable node is justified;
- threshold -> compare node;
- sustained condition -> Timer + cancel/reset;
- escalation -> state/latch nodes.

If a dedicated `Cooldown`/`Debounce` node greatly simplifies faithful migration, it may be added to the internal node catalog as a generic reusable node. Do not add protocol-specific nodes like `BattleAlertNode`.

## Default availability

Do not execute presets magically just because KIAS core exists. The physical-controller model should remain real.

Choose a coherent integration for standard KIAS builds:

- ship/core prototype may spawn/provide preprogrammed controller cards and a rack;
- construction/lathe may provide blank cards and preset workflow;
- existing KIAS maps may be updated to include a rack/cards where default automation is expected.

The important rule: active automation is represented by actual graph runtimes/cards, not hidden core policy.

## Legacy save/map compatibility

Before deleting old runtime:

1. identify whether existing maps/saves contain `KiasProtocolComponent.Protocols`;
2. create deterministic translator from each representable legacy record/action to graph nodes/wires, or provide a one-version explicit importer;
3. preserve enabled state and all visible actions;
4. report unsupported legacy edge cases rather than silently dropping them;
5. test migration;
6. remove or disable old execution after successful migration.

Do not keep both engines indefinitely “for compatibility”.

## Management console after migration

The management console can show:

- rack/controller status;
- active program names;
- faults;
- high-level automation summary;
- shortcut/instruction to open programmer.

It should no longer be the main rule editor if that would recreate a second automation UI.

## Definition of migrated

A protocol is migrated only when:

- its trigger data exists as graph input ports;
- its conditions can be expressed by generic nodes;
- its actions exist as device/input endpoints;
- its default graph preset reproduces behavior;
- tests prove the old hardcoded action path is not also running.
