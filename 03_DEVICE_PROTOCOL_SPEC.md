# Devices, controller profiles and automation presets

## 1. Controller profile philosophy

Every graph-visible KIAS device family gets a stable controller profile. Variants of the same gameplay device should share a profile when their automation semantics are the same.

A device can expose both native KIAS typed ports and generic DeviceLink ports. Native typed ports take precedence for rich data.

## 2. Source profiles — minimum expected ports

### RoomScanner

Possible outputs depending on installed modules:

- `Motion : Signal`
- `Occupied : Bool`
- `Entities : Number`
- `Crew : Number`
- `PersonCritical : Signal`
- `PersonDead : Signal`
- `Person : Entity`
- `AnomalyGrowth : Signal`

Ports unavailable without the physical module must not magically appear as functional data sources. UI may show disabled/unsupported ports or profile variants; choose a clean implementation.

### HullSensor

- `Impact : Signal`
- `Structure : Entity` where available.

### IntegrityMonitor

- `Damage : Signal`
- `Amount : Number`
- `Structure : Entity`

### CollisionMonitor

- `Collision : Signal`
- `RelativeSpeed : Number`
- `OtherGrid : Entity`

### AtmosSafety

- `Danger : Signal`
- `Fire : Signal`
- `Source : Entity`

### PowerMonitor

- `Deficit : Signal`
- `Supply : Number`
- `Consumption : Number`
- `Difference : Number`
- `Channel : Enum`

### Horizon

- `Contact : Signal`
- `ContactEntity : Entity`
- `Distance : Number`
- `Bearing : Number`
- `Disposition : ContactDisposition`

Rich IFF/disposition remains dependent on the accepted physical receiver prerequisite.

### WeaponFlashDetector

- `Triggered : Signal`
- `Source : Entity`
- `Disposition : ContactDisposition`

### ProximitySensor

- `Contact : Signal`
- `ContactEntity : Entity`
- `Distance : Number`
- `Disposition : ContactDisposition`

### CrewMonitor / CrewServer

- `CrewCritical : Signal`
- `CrewDead : Signal`
- `Person : Entity`
- presence/count change outputs where useful.

### Navigation

- `Arrival : Signal`
- other existing navigation/contact events that have real physical backing.

## 3. Actuator profiles — minimum expected ports

### Speaker

Inputs:

- `Message : String`
- `Announce : Signal`
- optional allowed alarm/tone selection through safe prototype-backed config, not arbitrary resource paths.

### Recorder

- `Message : String`
- `Record : Signal`

### LightController

- `Set : Bool`
- `On : Signal`
- `Off : Signal`

### Suppression

- `Trigger : Signal`

### Relay

- `Closed : Bool`
- `Open : Signal`
- `Close : Signal`

### DefenceController / PDC

- `Automatic : Bool`
- `Enable : Signal`
- `Disable : Signal`

The realtime interception algorithm remains inside the existing defence/PDC subsystem.

### NavigationComms

- `MaydayMessage : String`
- `Mayday : Signal`

Use existing rate limits/safety logic.

### Generic DeviceLink device

Expose generic source/sink ports as `Signal` unless a KIAS adapter declares a richer type.

## 4. SPECIFIC / ANY / ALL user semantics

### Specific

`SPECIFIC Speaker #123` means exactly that device. Moving the card to another ship does not silently choose another speaker.

### ANY

`ANY WeaponFlashDetector` means “listen to every matching detector, but when commanding this device type choose one available instance.”

Sensor example:

```text
ANY WeaponFlashDetector.Triggered
```

fires if **any** matching detector fires.

Actuator example:

```text
Signal -> ANY Speaker.Announce
```

chooses one deterministic Online speaker.

### ALL

`ALL Speaker` listens to events from all speakers if relevant and broadcasts incoming commands to all matching speakers.

Example:

```text
ANY WeaponFlashDetector.Triggered
 -> ALL Speaker.Announce
```

This is a portable ship-wide warning card.

## 5. Internal logic node set

- ON START
- Bool/Number/String constants
- AND / OR / XOR / NOT / NAND / NOR / XNOR
- IF/ELSE
- Timer/Delay
- Clock
- Latch/Memory Bool
- Toggle
- Counter
- Edge Detector
- Number compare
- Bool equality
- String equality
- Enum/Disposition equality

## 6. Preset library replacing current protocols

All current accepted default protocols must be represented as editable graph presets. The exact list is discovered from the current branch/parity audit; expected baseline includes:

### Battle Alert

Typical portable graph:

```text
ANY HullSensor.Impact ---------\
ANY WeaponFlashDetector.Triggered -> OR/conditions -> battle branch
Manual input ------------------/

battle branch -> ALL Speaker
              -> ALL/filtered LightController
              -> ALL DefenceController.Automatic=true
              -> ALL Recorder
```

Hostile disposition comparison is used where the source provides disposition.

### Point Defence

Graph controls PDC automatic state / engagement enable. Existing PDC system still performs projectile indexing, trajectory checks, FireControl reservation and firing.

### Contact

`ANY Horizon.Contact -> disposition conditions -> ALL Speaker + ALL Recorder`.

### Decompression

`ANY AtmosSafety.Danger -> speaker + configured DeviceLink door/vent adapters + recorder`.

### Fire

`ANY AtmosSafety.Fire -> speaker + ALL/filtered Suppression + configured doors + recorder`.

### Emergency Power

`ANY PowerMonitor.Deficit -> threshold/timer -> warning + selected relay/group actions`.

Do not blindly open/close every relay; use selector filters/group metadata or explicit devices as the preset/ship design requires.

### Anomaly Growth

`ANY RoomScanner.AnomalyGrowth -> ALL Speaker + Recorder`.

### Autopilot Arrival

`ANY Navigation.Arrival -> ALL/filtered Speaker`.

### Critical Vessel

Compose existing damage/atmos/power/crew conditions with Timer/Latch logic, then emergency outputs.

### Medical Assistance

Preserve the accepted semantics: never trigger only because a parked ship is empty; require actual distress conditions.

### MAYDAY

Graph invokes Navigation/Comms mayday endpoint; existing rate limiting and IFF/radio implementation remains subsystem code.

## 7. Preset portability rule

A default preset should contain no specific EntityUid unless the protocol inherently targets a particular configured physical device.

Prefer:

- ANY sensor profiles;
- ALL actuator profiles;
- optional logical room/group filters.

This makes the same controller card reusable on different shuttles.
