# Horizon: test game setup

Work branch: `HorizonCrysisSystem`. Commands below require Debug administrator permission.

## Quick start

1. Start a normal game round. Horizon is enabled by `forge.horizon.enabled` by default.
2. Run `horizon_force_event late`. If RTR maps are still loading, late deployment is queued automatically.
3. Use `horizon_status` until the phase is `Operational`.
4. Run `horizon_debug pause` to stop automatic strategic scheduling.
5. Run `horizon_debug projects` to list maps, project IDs, prices and limits.
6. Run `horizon_debug resources 500 500 300`, then `horizon_debug build HorizonAMZ09` and `horizon_debug step`.
7. Inspect `horizon_objects`, `horizon_orders`, `horizon_incidents`, `horizon_relations` and `horizon_perf`.
8. Run `horizon_debug resume` for normal economy and expansion.

Energy is clamped to actual network energy capacity. Builds keep normal resource, placement, project and object limits; `step` is queued, not an immediate mass spawn. Pausing strategy does not freeze defense, movement, recovery or world time.

## Deployment/recovery test

In a fresh round use `horizon_force_event activate`, then `horizon_debug wake` to skip only the wake delay. The AMS still travels with the normal autopilot. Use `horizon_debug fail_ams` during deployment to exercise two retries and the single emergency cluster. Use `horizon_force_event destroy` to check irreversible network loss. Restart the round between destructive scenarios.

`horizon_debug cancel ORDER_GUID` cancels queued construction only. Active movement cannot be cancelled with this command.

## Imported archive

All 16 source entries are in `Resources/Maps/_Forge/Horizon/Archive`. `amt-06_duplicate.yml` is byte-identical to `amt-06.yml`; one project uses the canonical map. Both T-09 revisions are separate projects.

`HorizonAMZ01` and `HorizonT01` now use their dedicated archive maps. Other archive craft are opt-in projects with `desiredCount: 0`: `HorizonAMT01`, `HorizonAMT02`, `HorizonAMT06`, `HorizonAMT11`, `HorizonAMD01`, `HorizonAMH01`, `HorizonAMY01`, `HorizonAMZ09`, `HorizonC01`, `HorizonT05`, `HorizonT09`, `HorizonT09V2`, `HorizonViar17`.

Archive names do not establish final gameplay roles by themselves: catalog kinds are conservative test classifications and must be confirmed during mapping/balance review. Viar-17's two hostile `NpcStationAiAttacker` cores were replaced with `PlayerStationAiEmpty`; it must not contain enemy AI when deployed as a network asset. AMY-01 keeps its empty station AI core; it is not the designated Wandering AI carrier. AMU-05 remains that carrier.

## Mapper contract

IFF is configured and locked on the grid by the Horizon system. There is no new Horizon IFF entity; `ComputerIFF` is optional UI.

| Asset | Required Horizon prototypes | Other requirements |
|---|---|---|
| RTR | `HorizonRTR` | Keep the core physically reachable/destructible |
| AMS-01 | `HorizonNavigationLight`, `HorizonNameplate` | `ComputerShuttle`, thrusters, gyroscope |
| O-01 | `HorizonStationCore`, `PlayerStationAiHorizon`, `HorizonCommunicationConsole`, `HorizonResourceTerminal`, navigation light/nameplate | Only O-01 hosts the Wandering AI role |
| Energy/relay/mining/production/defense/technical stations | `HorizonStationCore`, corresponding `HorizonEnergyModule` / `HorizonRelayModule` / `HorizonMiningModule` / `HorizonProductionModule` / `HorizonDefenseModule` / `HorizonTechnicalModule`, navigation light/nameplate | Economy is aggregate; not a physical production chain |
| AMZ craft | Navigation light/nameplate | `ComputerShuttle`, thrusters, gyroscope, weapons/ammunition; defense executor is attached at runtime |
| AMU-05 | `HorizonWanderingCarrier`, navigation light/nameplate | Unarmed remote borg; no second Wandering AI core |
| Optional archive transports | `HorizonStationCore`, navigation light/nameplate | Their test catalog role determines the fixture added at runtime |

Missing fixtures are added near grid origin as a fallback. Put them on proper floor tiles in final maps to avoid overlapping machines. `StationAiBrainHorizon` is filled into `PlayerStationAiHorizon` automatically and must not be mapped separately.

## Live acceptance checklist

- RTR activation, AMS travel, O-01 deployment and all retry paths.
- Contributions, UI refresh, economy, project counts and queue deadlines.
- Damage incidents and AMZ response/return on the same map; turret operation and ammunition under live combat.
- Wandering AI takeover, laws, carrier handoff, ownership retained in the core, return and carrier destruction.
- O-01 destruction ends the mature network permanently.
- Observe spawn/strategy latency and queues with several players for a full test round.

Automated map/policy checks are not a substitute for this multiplayer acceptance run. Remaining borrowed E-01/S-01/P-11/Z-01/AMZ-04/AMU-05 maps and physical salvage/towing are not represented as finished custom content.
