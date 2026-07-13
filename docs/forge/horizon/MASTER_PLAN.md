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
- [ ] Commit.

## Phase 4 — Scheduler/economy

- [ ] Coarse scheduler.
- [ ] Bounded queue.
- [ ] Ledger.
- [ ] Income/capacity aggregates.
- [ ] Costs.
- [ ] Minimal planner.
- [ ] Orders.
- [ ] Tests.
- [ ] Commit.

## Phase 5 — Spatial

- [ ] Cluster anchor.
- [ ] Protected registry.
- [ ] Hard/soft exclusion.
- [ ] Candidate generation.
- [ ] Bubble/density/branch.
- [ ] Minimum station set.
- [ ] Tests.
- [ ] Commit.

## Phase 6 — Defense

- [ ] Incident aggregation.
- [ ] Immediate response.
- [ ] AMZ.
- [ ] Defend orders.
- [ ] Chase limits.
- [ ] IFF.
- [ ] Defense costs/limits.
- [ ] Tests.
- [ ] Commit.

## Phase 7 — Relations/UI

- [ ] Organization key.
- [ ] Contribution/damage/access.
- [ ] Communication console.
- [ ] Resource terminal.
- [ ] Diagnostics.
- [ ] Tests.
- [ ] Commit.

## Phase 8 — Wandering AI

- [ ] Role prototype.
- [ ] Playtime.
- [ ] Directives.
- [ ] Goal/context/permissions.
- [ ] Ghost offer.
- [ ] Handoff.
- [ ] Return to AI.
- [ ] Abuse safeguards.
- [ ] Tests.
- [ ] Commit.

## Phase 9 — Integration

- [ ] MVP maps/prototypes.
- [ ] Temporary assets tracked.
- [ ] Announcements.
- [ ] Destruction state.
- [ ] End-to-end scenario.
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

## Technical decisions

- Keep strategic state in one server-side domain model owned by `HorizonSystem`; entity components hold only local identity/capacity/executor data.
- Use the existing shuttle HTN autopilot for physical movement and bounded strategic orders for intent/timeout tracking.
- Temporary MVP project definitions may reuse existing map files, but all Horizon selection, identity, balance and lifecycle data remains Forge-owned and configurable.

## Current blockers

_None._

## Next exact action

_Commit Phase 3, then implement Phase 4 coarse scheduler, economy, bounded planner and general orders._
