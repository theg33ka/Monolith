# Horizon MVP test report — 2026-07-13

## Build

- Branch: `HorizonCrysisSystem`
- Configuration: Release
- Command: `dotnet build -c Release --no-restore --nologo /p:WarningsAsErrors= -v:minimal`
- Result: passed after correcting Horizon compile errors found by the first full solution pass.
- Existing repository NU/obsolete/analyzer warnings remain; no Horizon build error remains.

## Automated tests

- Command: `dotnet test Content.Tests/Content.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Content.Tests.Server._Forge.Horizon"`
- Result: 33 passed, 0 failed.
- NUnit execution time: 5.5125 seconds.
- Covered policies: registry aggregates, nearest RTR, bounded AMS recovery, queue behavior, economy, planning, spatial placement, defense/IFF, relations, Wandering AI safeguards, irreversible destruction and the combined MVP lifecycle scenario.

## Performance

- Synthetic typical-cluster soak: 500 cycles in 41.71 ms.
- Work queue hard cap: 256 even with an unsafe runtime CVar.
- Work drained per tick: hard cap 32; requeued work waits for the next drain.
- Spatial candidates: hard cap 64; object/protected-zone inputs hard cap 128.
- Orders: hard cap 128; incidents: 128; relations: 256; defense units: 8.
- Production defaults are lower: queue 64, batch 4, candidates 16, objects 64, orders 24, incidents 32, relations 64 and defense units 2.

## Prototype validation

- Command: `bin/Content.YAMLLinter/Content.YAMLLinter.exe`
- Result: `No errors found in 179699 ms.`
- Validated both server and client prototype graphs, including Horizon projects, consoles, temporary fixtures, ghost role, playtime requirements and lawset.

## Scenario status

| Scenario | Result | Evidence |
|---|---|---|
| RTR activation / nearest neighbor | Pass | deployment policy tests and compiled system path |
| Automatic activation | Pass (code path) | 5400-second CVar and coarse deployment update |
| AMS movement / O-01 deploy | Pass (integration build) | standard HTN autopilot path, bounded timeout and recovery tests |
| Two respawns / emergency cluster | Pass | complete recovery policy matrix |
| Late start | Pass (code path) | immediate protected O-01 path compiled and prototype-validated |
| Registry / scheduler / economy | Pass | lifecycle aggregate and bounded queue tests |
| Spatial bubble | Pass | hard-zone and branch-limit tests |
| Incident / AMZ / IFF | Pass | aggregation, chase-limit and escalation tests |
| Relations / resource terminal | Pass | threshold tests plus server-validated physical stack path |
| Wandering AI | Pass | role/law prototypes plus handoff safeguard tests |
| Mature destruction | Pass | combined lifecycle scenario and idempotent destruction assertion |

## Remaining non-program blockers

- The 12 Horizon maps and unique art are temporary reused assets; see `CONTENT_STATUS.md`.
- A 4–6 hour live multiplayer playtest was not run in this local automated pass.
- AMU-05 provides the working Wandering AI carrier; physical salvage/towing execution remains outside the required program MVP loop.

