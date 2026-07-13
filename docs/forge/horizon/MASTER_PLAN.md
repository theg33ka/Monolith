# Master Plan: АКС «Горизонт»

Codex ведёт этот файл в одном постоянном чате.

## Definition of done

- ветка `HorizonCrysisSystem` собирается;
- полный цикл работает;
- аварийный и поздний старт работают;
- registry без глобальных сканов;
- bounded scheduler;
- AMS/AMZ используют автопилот;
- economy/orders/spatial работают;
- incidents/relations/consoles работают;
- Wandering AI работает;
- debug/perf работают;
- сеть окончательно уничтожима;
- тесты и performance-checks задокументированы.

## Phase 0 — Repository study

- [x] Git safety and branch.
- [x] Baseline build.
- [x] Forge conventions.
- [x] Drone core/autopilot.
- [x] Grid/map lifecycle.
- [x] Ghost roles/playtime/laws.
- [x] UI/consoles/holopads.
- [x] IFF/damage/admin/test patterns.
- [x] Record findings.

## Phase 1 — Foundation

- [x] Horizon state/system/config.
- [x] Object registry.
- [x] Aggregates.
- [x] Debug status/object commands.
- [x] Tests.
- [x] Commit.

## Phase 2 — RTR

- [x] Dormant RTR.
- [x] Controlled positions.
- [x] Proximity trigger.
- [x] Automatic activation.
- [x] Nearest neighbor.
- [x] One active cluster.
- [x] Announcements.
- [x] Tests.
- [x] Commit.

## Phase 3 — AMS and O-01

- [x] Shared shuttle core.
- [x] Autopilot.
- [x] Deploy order.
- [x] Movement/timeout.
- [x] Deployment timer.
- [x] Grid replacement.
- [x] Two respawns.
- [x] Emergency cluster.
- [x] Late profile.
- [x] Tests/perf.
- [x] Commit.

## Phase 4 — Scheduler/economy

- [x] Coarse scheduler.
- [x] Bounded queue.
- [x] Ledger.
- [x] Income/capacity aggregates.
- [x] Costs.
- [x] Minimal planner.
- [x] Orders.
- [x] Tests.
- [x] Commit.

## Phase 5 — Spatial

- [x] Cluster anchor.
- [x] Protected registry.
- [x] Hard/soft exclusion.
- [x] Candidate generation.
- [x] Bubble/density/branch.
- [x] Minimum station set.
- [x] Tests.
- [x] Commit.

## Phase 6 — Defense

- [x] Incident aggregation.
- [x] Immediate response.
- [x] AMZ.
- [x] Defend orders.
- [x] Chase limits.
- [x] IFF.
- [x] Defense costs/limits.
- [x] Tests.
- [x] Commit.

## Phase 7 — Relations/UI

- [x] Organization key.
- [x] Contribution/damage/access.
- [x] Communication console.
- [x] Resource terminal.
- [x] Diagnostics.
- [x] Tests.
- [x] Commit.

## Phase 8 — Wandering AI

- [x] Role prototype.
- [x] Playtime.
- [x] Directives.
- [x] Goal/context/permissions.
- [x] Ghost offer.
- [x] Handoff.
- [x] Return to AI.
- [x] Abuse safeguards.
- [x] Tests.
- [x] Commit.

## Phase 9 — Integration

- [x] MVP maps/prototypes.
- [x] Temporary assets tracked.
- [x] Announcements.
- [x] Destruction state.
- [x] End-to-end scenario.
- [ ] Commit.

## Phase 10 — Stabilization

- [ ] Full review.
- [ ] Lifecycle review.
- [ ] Performance profiling.
- [ ] Long test.
- [ ] Regression fixes.
- [ ] Docs update.
- [ ] Final commit.

## Repository findings

- 2026-07-13: `main` and `origin/main` both point to `f37d012991`; no fast-forward was required.
- Work continues on `HorizonCrysisSystem`. User-owned `.gitignore` and `RobustToolbox` changes are preserved and excluded from Horizon commits.
- Baseline `dotnet build -c Release --nologo` passed in 179 seconds. Existing `NU1903`, `NU1510` and obsolete API warnings remain.
- Forge extension code belongs under `Content.*\_Forge\Horizon` and `Resources\Prototypes\_Forge\Horizon`; no upstream edit is required for the MVP architecture.
- Shuttle movement reuses `BaseComputerShuttle`, `HTNComponent`, `AutopilotShuttleCompound`, blackboard target keys and `ShipMoveToOperator`; completion is exposed by `SteeringDoneEvent`.
- Runtime grids reuse `MapLoaderSystem.TryLoadGrid`. The active sector map is `GameTicker.DefaultMap`; `PointOfInterestSystem` and `PublicTransitSystem` are the placement/loading references.
- Registry lifecycle uses component startup/shutdown events. Grid/object deletion is therefore event-driven and needs no global grid scan.
- Wandering AI reuses `GhostRole`, `GhostTakeoverAvailable`, playtime `JobRequirement`, Station AI containers and `SiliconLawProvider`.
- Consoles reuse the standard server/shared/client BUI pattern. UI refresh will occur on events/open and a coarse configurable interval only.
- Damage aggregation uses `DamageChangedEvent.DamageDelta` and `Origin`; local NPC faction/IFF support is available through `NpcFactionSystem` and shuttle IFF systems.
- Admin diagnostics use `[AdminCommand]` + `IConsoleCommand`. Pure planning/scheduler tests belong in `Content.Tests`; ECS lifecycle and BUI scenarios belong in `Content.IntegrationTests`.
- The source workbook defines 12 required MVP maps, 53 content prototypes, and recommends 30–50 hours overall plus 5–10 hours silicon/AI playtime for Wandering AI.

## Progress log

- 2026-07-13: Phase 0 complete; baseline release build passed.
- 2026-07-13: Phase 1 foundation complete. `Content.Server` Release build passed; focused `Content.Tests` Horizon run passed 2/2.
- 2026-07-13: Phase 1 committed as `996f2eb779`.
- 2026-07-13: Phase 2 RTR complete. `Content.Server` Release build passed, focused Horizon tests passed 3/3, and YAML linter reported no errors.
- 2026-07-13: Phase 2 committed as `fab804572e`.
- 2026-07-13: Phase 3 AMS/O-01 complete. `Content.Server` Release build passed, focused Horizon tests passed 9/9, and YAML linter reported no errors in 150249 ms.
- 2026-07-13: Phase 3 committed as `70966abefc`.
- 2026-07-13: Phase 4 scheduler/economy complete. `Content.Server` Release build passed and focused Horizon tests passed 13/13.
- 2026-07-13: Phase 4 committed as `dc9c11af41`.
- 2026-07-13: Phase 5 spatial/minimum network complete. `Content.Server` Release build passed, focused Horizon tests passed 15/15, and the rebuilt YAML linter reported no errors in 177789 ms.
- 2026-07-13: Phase 5 committed as `69c91ef19d`.
- 2026-07-13: Phase 6 event-driven incidents/AMZ defense implemented. Validation deferred to the final combined pass at the user's request.
- 2026-07-13: Phase 6 committed as `ab10d1b13e`.
- 2026-07-13: Phase 7 relations and event/coarse-refreshed console UI implemented. Late deployment now creates O-01 immediately. Validation deferred to the final combined pass.
- 2026-07-13: Phase 7 committed as `1b9d02cc05`.
- 2026-07-13: Phase 8 Wandering AI role, 30h/5h requirements, fixed directives, restricted O-01 context and single unarmed AMU-05 carrier handoff/return implemented. Validation deferred to the final combined pass.
- 2026-07-13: Phase 8 committed as `e9ffa689bf`.
- 2026-07-13: Phase 9 integrated all 12 required runtime objects, physical temporary fixtures, industrial/armed/loss announcements, irreversible destruction policy and a full domain scenario. `CONTENT_STATUS.md` records every reused map and the 53-row workbook scope. Validation deferred to the final combined pass.

## Technical decisions

- Keep strategic state in one server-side domain model owned by `HorizonSystem`; entity components hold only local identity/capacity/executor data.
- Use the existing shuttle HTN autopilot for physical movement and bounded strategic orders for intent/timeout tracking.
- Temporary MVP project definitions may reuse existing map files, but all Horizon selection, identity, balance and lifecycle data remains Forge-owned and configurable.

## Current blockers

_None._

## Next exact action

_Commit Phase 9, then perform Phase 10 lifecycle/performance review and the single final combined validation pass._
