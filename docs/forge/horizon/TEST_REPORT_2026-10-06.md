# Horizon archive integration and stabilization — 2026-10-06

## Repository

- Branch: `HorizonCrysisSystem`.
- Merged `upstream/main` (`b069ad386d`) in merge commit `d3e7cad24d`.
- Kept both `.gitignore` rule groups; no history rewrite or push.
- RobustToolbox and recursive submodules match incoming main; no local engine source changes.

## Changes

- Imported 16 ZIP entries, 15 distinct grids. Both T-09 variants retained; identical AMT-06 duplicate retained without a duplicate project.
- Added 13 opt-in archive projects; AMZ-01 and T-01 use dedicated maps.
- Neutralized Viar-17 hostile AI cores, cleared AMT-01's invalid network configurator reference, and replaced removed `ForgeLaserDrill` with `StationLaserDrill` in D-04.
- Fixed engine discovery of Horizon CVars, project-specific counts and pending reservations, manual build limits, queued order deadlines, partial-load cleanup, bounded grid spawning and queued late deployment.
- Added per-grid bounded damage relays and reused Monolith ship targeting for AMZ response. Cross-map/friendly-grid dispatch is rejected; targeting and autopilot stop when the network is destroyed.
- Wandering AI retains ownership of its core with mind visitation; return uses unvisit, including the carrier's normal return action.
- Nine `horizon_debug` actions cover catalog, resources, builds, pause/resume, single steps, cancellation, simulated AMS failure and wake delay skipping.

## Final validation

- Release solution build: passed, zero errors. Existing repository dependency/obsolete/analyzer warnings remain.
- Focused Horizon domain/configuration tests: 37 passed, zero failed.
- Server/client prototype validation: no errors in 243409 ms.
- Map integration test: all 22 distinct project map paths loaded; invalid project rejection, queued build limit and cancellation/requeue also passed. Final server-init rerun after return-action subscription passed in 33 seconds.
- Git whitespace checks and RobustToolbox local-change check: passed.

Earlier failed validation passes exposed and were used to fix missing CVar registration, the removed D-04 drill prototype and AMT-01's invalid network reference. They are not counted as successful checks.

## Acceptance boundary

This prepares the code and content for a supervised test game, not an already-accepted multiplayer release. A live full round, physical combat/ammunition, carrier ownership/return/destruction and latency under players still need the `PLAYTEST.md` acceptance checklist. Archive transport classifications need mapping/balance confirmation. Six borrowed base-project maps remain temporary; physical salvage/towing remains outside the aggregate MVP.
