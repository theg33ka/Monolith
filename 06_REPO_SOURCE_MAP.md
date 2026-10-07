# Repo source map — graph integration quick reference

| Area | Existing / reference paths |
|---|---|
| KIAS core/topology | `Content.Shared/_Forge/KIAS/KiasComponents.cs`, `KiasTopology.cs`, `Content.Server/_Forge/KIAS/KiasSystem.cs`, `KiasGridComponent.cs` |
| Current legacy protocols | `Content.Shared/_Forge/KIAS/KiasProtocols.cs`, `Content.Server/_Forge/KIAS/KiasProtocolSystem.cs` |
| KIAS hull sensors | `Content.Shared/_Forge/KIAS/KiasHullComponents.cs`, `Content.Server/_Forge/KIAS/KiasHullSystem.cs` |
| KIAS external sensors | `Content.Shared/_Forge/KIAS/KiasExternalSensors.cs`, `Content.Server/_Forge/KIAS/KiasExternalSensorSystem.cs` |
| KIAS navigation events | `Content.Shared/_Forge/KIAS/KiasNavigationEvents.cs` and current navigation system |
| KIAS power | `Content.Server/_Forge/KIAS/KiasPowerSystem.cs` |
| Current KIAS UI | `Content.Client/_Forge/KIAS/*` — re-open current post-patch split UI paths |
| Central KIAS access | current first-patch `KiasAccessSystem`/resolver or equivalent — locate before implementing programmer/rack |
| DeviceLink source/sink | `Content.Shared/DeviceLinking/*` |
| DeviceLink runtime | `Content.Server/DeviceLinking/Systems/DeviceLinkSystem.cs` |
| DeviceLink UI reference | `Content.Client/NetworkConfigurator/NetworkConfiguratorLinkMenu.xaml(.cs)` |
| Logic gates reference | `Content.Server/DeviceLinking/Components/LogicGateComponent.cs`, `Systems/LogicGateSystem.cs` |
| Timer reference | `Content.Server/DeviceLinking/Components/SignalTimerComponent.cs`, `Systems/SignalTimerSystem.cs` |
| Dynamic APC load | `Content.Shared/Power/EntitySystems/SharedPowerReceiverSystem.cs`, server `PowerReceiverSystem` |
| Fire control/PDC | `Content.Server/_Mono/FireControl/FireControlSystem*.cs`, existing KIAS defence system |
| Projectile lifecycle | `Content.Server/Projectiles/ProjectileSystem.cs`, `_Mono/SpaceArtillery/ShipWeaponProjectileComponent.cs` |
| Atmos | `Content.Server/Atmos/Monitor/Systems/AirAlarmSystem.cs` |
| Anomaly | `Content.Shared/Anomaly/*`, `Content.Server/Anomaly/AnomalySystem.cs` |
| FTL/arrival | `Content.Server/Shuttles/Events/FTLCompletedEvent.cs`, shuttle/autopilot systems |
| IFF/faction/company | current shuttle IFF + Forge company/faction components; preserve physical receiver prerequisite from first patch |
| /tg Wiremod | `code/modules/wiremod/core/*`, `tgui/packages/tgui/interfaces/IntegratedCircuit/*` |
| Goob nested filters | `Content.Goobstation.Shared/Factory/Filters/*` |
| New controller files | Prefer `Content.Shared/_Forge/KIAS/Controllers/*`, `Content.Server/_Forge/KIAS/Controllers/*`, `Content.Client/_Forge/KIAS/Controllers/*` unless current repo conventions dictate otherwise |
| New prototypes | Prefer `Resources/Prototypes/_Forge/KIAS/controllers.yml` or current KIAS prototype file |
| Controller tests | `Content.IntegrationTests/Tests/_Forge/KIAS/*` |
| Docs | `Docs/KIAS/ARCHITECTURE.md`, `PLAYER_GUIDE.md`, `VALIDATION.md`, `PARITY_AUDIT.md`, new `CONTROLLERS.md`, `THIRD_PARTY.md` |

## Supplied asset paths in this handoff

- `assets/raw/kias_controller_rack.png`
- `assets/raw/kias_programmable_controller.png`
- `assets/rsi_ready/KiasControllerRack.rsi/`
- `assets/rsi_ready/KiasProgrammableController.rsi/`

The coding agent must choose final repository texture paths and add any required REUSE/license metadata according to the actual Forge conventions.
