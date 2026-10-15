# MASTER PROMPT — implement KIAS in Monolith

You are implementing the complete KIAS ship automation system in a local clone of `Forge-Station/Monolith` on a dedicated feature branch.

Repository research was performed against `main` commit `b069ad386d7599ac6b51d9dfcdfba9b6a311f676`. The local branch may be newer. Before editing, verify local HEAD and re-open every source path listed in `01_REPO_FINDINGS.md`. Follow current equivalents if code moved.

## Goal
Build a modular ship automation layer:
- one KIAS core per grid;
- optional specialized servers: defence, atmosphere/safety, power, crew/security, navigation/comms;
- dedicated underfloor KIAS data cable with 2-tile device service radius;
- modular room scanners;
- dedicated external sensors where placement matters;
- typed KIAS events/protocols;
- speakers, displays, relays, fire suppression, logs and hard master shutdown;
- direct integration with existing air alarms, anomaly events, FTL/autopilot, IFF/faction, ship ownership, fire control and projectiles.

## Working rules
1. First inspect current repository code and map the paths in this pack to current equivalents.
2. Reuse existing systems; do not fork/reimplement shuttle control, atmosphere, FireControl, IFF or power distribution.
3. Event-driven first. Polling only where no event exists, at low frequency, over pre-indexed small sets.
4. Never add KIAS loops equivalent to `every frame × every ship × every scanner × nearby entities`.
5. Never add `every radar × all projectiles` full scans.
6. No per-device `Update()` loops unless unavoidable; central systems own scheduling.
7. Compile after each coherent subsystem and fix failures immediately.
8. Add integration tests and at least one stress/performance harness.
9. Reuse XAML/BUI patterns already in Monolith.
10. Do not ask the user before ordinary implementation choices. Escalate only true blockers or gameplay-changing conflicts.

## Mandatory gameplay details
- Console/display shows `Entities: N // Crew: M`.
- `Entities` includes intended player/sapient entities: living/dead players, borgs, chimeras, sentient artifacts; ordinary NPC mobs must not inflate it.
- `Crew` is detected registered KIAS crew transponders.
- Crew transponder is a separate 1x1 item.
- Register by left-clicking the transponder on the Crew KIAS server while registration is unlocked.
- Crew server registration starts locked; only ship owner can lock/unlock.
- Spectral module announces ship-wide when an anomaly crosses into growth state, with location.
- Hull system includes grid-vs-grid collision events and announcements.
- Horizon bluespace interferometer reacts to nearby FTL completion/arrival (~2 km) and drives CONTACT.
- Power relay selected with screwdriver/RMB-style verb among HV / MV / LV / DATA.
- Simple scanner→speaker link configuration supports direct custom message text.
- TEST action has 10 second cooldown and restores prior state after test.
- Master key OFF: announce shutdown first, then entire KIAS becomes inactive as if dead. ON: rebuild/re-register and announce online.

## Required implementation order
`core/grid registry -> data cable/topology -> servers -> speaker/display/tool -> scanner/modules/transponders -> atmosphere/anomaly -> FTL/Horizon/autopilot -> hull/collision -> power relay -> defence/PDC -> protocols/recorder -> perf pass`

Do not start with PDC.

## Definition of done
- topology survives cable cuts/reconnect and grid lifecycle;
- transponders and counts work;
- hard-off removes the grid from active KIAS processing;
- atmosphere/anomaly/FTL/autopilot integrations are event-driven;
- simple links work with custom speaker text;
- PDC uses existing FireControl/Gun code;
- no obvious O(ships×all-entities/projectiles) per-frame behavior;
- tests, YAML and localization pass.
