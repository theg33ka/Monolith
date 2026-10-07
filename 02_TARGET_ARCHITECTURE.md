# Target architecture — post-implementation correction

## 1. Three independent concepts: KIAS network, target power, automation graph

KIAS DATA connectivity answers: **can KIAS talk to/control this endpoint?**

Device functional power answers: **is the target itself currently powered/working?**

Graph runtime answers: **what logic should execute when events/data arrive?**

Do not conflate these.

For a normal native KIAS server/device, losing its required power makes it offline. For a device controlled by an integration endpoint, KIAS may intentionally disable the target's functional power while the integration endpoint remains reachable over KIAS DATA. This distinction is required for OFF -> ON recovery.

## 2. Controller profiles describe capabilities, not misleading object names

Profiles should map gameplay semantics:

- `Lighting` = directly controllable light fixtures;
- `LightGroupController` = KiasLightController devices;
- `Speaker`, `Recorder`, sensors, relays, etc. = existing capability families;
- generic DeviceLink profile remains fallback for legacy ports.

A profile resolver may use explicit marker/capability components when a single `component:` prototype field is insufficient.

## 3. Addressing

`SPECIFIC`: exactly one device.

`ANY`: one deterministic available match for commands; events merge from any match.

`ALL`: command broadcast to every current match exactly once; events merge from any match.

Filters are selector metadata, not device configuration. Room/group fields must be named and shown as filters, not «set room/group» actions.

## 4. Event vs state

`Signal` is an impulse/event. It has no persistent true/false value.

`Bool` is a persistent logical state.

The graph keeps strict typing. Conversions are explicit nodes:

- Bool -> Edge -> Signal;
- Signal -> Toggle/Latch -> Bool.

The UI must teach this model at the point of interaction.

## 5. Enum domains

Do not treat every user-facing enum as a naked integer.

Port/schema metadata should know its semantic domain where relevant:

- audio channel;
- contact disposition;
- KIAS alert;
- power channel;
- other actual enums.

The editor renders localized dropdown values and rejects incompatible enum domains if a safe implementation is practical. Persistence must remain versionable.

## 6. Native gameplay systems remain authoritative

KIAS graph does not reimplement:

- AirAlarm device network/list management;
- light bulb/power systems;
- DeviceLink storage/loop protection;
- speech/chat routing;
- PDC/fire control;
- atmos alarm calculations.

KIAS should call/bridge existing systems and preserve their normal manual configuration path.

## 7. UI architecture

Separate presentation layers:

- graph canvas: dense visual logic;
- palette: discovery/search;
- inspector: only selected-node relevant configuration/help;
- status/validation: errors and current card/network state;
- device local UIs: focused device configuration;
- rack/management: operational status.

Use common reusable KIAS UI helpers/styles for section panels, status badges, field rows and help text instead of recreating ad-hoc controls in each window.
