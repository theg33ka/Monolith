# Horizon MVP content status

This manifest separates working program logic from final art and mapping. Reused maps and sprites are intentionally marked temporary; they are not presented as finished Horizon assets.

## Required map/object coverage

| MVP object | Runtime prototype | Current map/content | Status |
|---|---|---|---|
| RTR | `HorizonRTRGrid` / `HorizonRTR` | `/Maps/_Forge/Horizon/rtr.yml` | Dedicated map; grids are loaded through a bounded deployment queue |
| AMS-01 | `HorizonAMS01` | `/Maps/_Forge/Horizon/Shuttles/ams01_kamenshchik.yml` | Dedicated Kamenshchik map; autopilot console has a runtime fallback |
| O-01 | `HorizonO01` | `/Maps/_Forge/Horizon/Stations/o01.yml` | Dedicated command map with the Horizon Wandering AI core |
| E-01 | `HorizonE01` | `/Maps/Salvage/small-template.yml` | Functional aggregate energy node, temporary map |
| S-01 | `HorizonS01` | `/Maps/Salvage/small-template.yml` | Functional relay node, temporary map |
| D-04 Uglich | `HorizonD04` | `/Maps/_Forge/Horizon/Stations/d04_uglich.yml` | Dedicated Uglich mining map |
| P-11 Tula | `HorizonP11` | `/Maps/Salvage/small-template.yml` | Functional aggregate production node, temporary map |
| Z-01 | `HorizonZ01` | `/Maps/Salvage/small-template.yml` | Functional protected defense node, temporary map |
| AMZ-01 | `HorizonAMZ01` | `/Maps/_Forge/Shuttles/Drones/Paralysis.yml` | Functional response/autopilot/IFF, temporary map |
| AMZ-04 | `HorizonAMZ04` | `/Maps/_Forge/Shuttles/Drones/Paralysis.yml` | Functional second response asset, temporary map |
| T-01 | `HorizonT01` | `/Maps/Salvage/small-template.yml` | Functional technical node, temporary map |
| AMU-05 | `HorizonAMU05` | `/Maps/Salvage/small-ai-survey-drone.yml` | Functional Wandering AI carrier, temporary map |

All runtime choices, IDs, costs, capacities, placement ranges and temporary flags live in Forge-owned prototypes. The remaining unique maps and sprites stay an art/mapping replacement task and do not require a systems rewrite.

## Mapper placement contract

The runtime still creates a missing Horizon fixture at grid origin as a functional fallback. Final map revisions should place these prototypes explicitly so their physical positions are intentional:

| Grid | Forge-owned prototypes to place | Existing prototype required |
|---|---|---|
| RTR | `HorizonRTR` | None |
| AMS-01 Kamenshchik | `HorizonNavigationLight`, `HorizonNameplate` | `ComputerShuttle` for the physical autopilot console |
| O-01 | `HorizonStationCore`, `HorizonCommunicationConsole`, `HorizonResourceTerminal`, `PlayerStationAiHorizon`, `HorizonNavigationLight`, `HorizonNameplate` | Optional `ComputerIFF` UI only |
| D-04 Uglich | `HorizonStationCore`, `HorizonMiningModule`, `HorizonNavigationLight`, `HorizonNameplate` | Optional `ComputerIFF` UI only |

Horizon IFF is a grid component configured and locked by `HorizonSystem`; there is no separate Forge-owned IFF entity prototype. `ComputerIFF` only provides the standard physical UI.

## Source workbook prototype coverage

The workbook contains 53 rows, including explicit post-MVP work.

- Directly implemented for the program MVP: PRT-001, 003-006, 009, 013-015, 017, 023, 032-036, 038-044, 046 and 049-053.
- Implemented with aggregate or temporary reusable content: PRT-011, 016, 018-022, 024-025 and 030-031. Defense is executed by bounded AMZ response rather than independent strategic turret logic; unique items are represented in the aggregate economy and are not loot rewards.
- Partial MVP carrier support: PRT-045. AMU-05 and the `SalvageObject` order contract exist, but physical towing/dismantling is intentionally outside the required program loop.
- Explicitly post-MVP in the workbook and not implemented here: PRT-002, 007-008, 010, 012, 026-029, 037 and 047-048.

## Temporary asset rules

- The four dedicated maps above have `temporaryContent: false`; nodes still using borrowed maps remain temporary.
- Runtime spawns are limited to one project grid per strategic cycle.
- Placeholder machines use Forge-owned prototypes and existing valid sprite states; no new unverified raster assets are introduced.
- Nameplates and navigation lights are physical per-grid entities. Strategic state is still held only by the lifecycle registry.
