# KIAS — correctness, programmer UX and regression patch (2026-10-08)

**Source:** [`theg33ka/Monolith`, branch `KIAS`](https://github.com/theg33ka/Monolith/tree/KIAS). Inspected HEAD `e32aecc197fed90b5947354bbad7b2c24add7911`; re-check current HEAD before implementation. This archive contains **specifications, not an implemented game patch**.

## Installation / replacement

1. Back up the root-level `00_AGENT_MASTER_PROMPT.md` through `09_THIRD_PARTY_REFERENCES.md` and `README.md` if you want to retain their former text. The originals document a previous correction pass; they are **not** present to be executed again.
2. Unpack the `.md` files **into the repository root**, preserving names; allow replacement of same-named files. New numbered files `10_...`, `11_...`, `12_...` augment the former set. Optional supporting screenshots live in `references/`.
3. Open `00_AGENT_MASTER_PROMPT.md` with the coding agent, or paste `COPYPASTE_PROMPT.md` as the opening agent message. Both are self-contained; the short paste prompt points to the comprehensive master and checklists.
4. Agent must inspect **live code** and repository contribution rules, not trust this snapshot over current HEAD.

## Documents

| File | Purpose |
| --- | --- |
| `00_AGENT_MASTER_PROMPT.md` | Master instructions for ~55–75 min of focused development and verification |
| `01_REPO_FINDINGS.md` | Grounded code observations, hypotheses vs established facts |
| `02_TARGET_ARCHITECTURE.md` | Scope and required invariants |
| `03_DEVICE_PROTOCOL_SPEC.md` | Precise expected behavior per affected device |
| `04_IMPLEMENTATION_PLAN.md` | Time-boxed ordering and stopping rules |
| `05_PERFORMANCE_AND_TESTS.md` | Automated tests, performance limits, UI checks |
| `06_REPO_SOURCE_MAP.md` | Real source files and where to start |
| `07_CONTROLLER_GRAPH_SPEC.md` | Programmer hierarchy and readable labels |
| `08_PROTOCOL_MIGRATION.md` | Legacy passes as REGRESSIONS, no double-implementation |
| `09_THIRD_PARTY_REFERENCES.md` | Contribution, asset, and clean-code rules |
| `10_RISK_REGISTER.md` | Risk analysis and proactive failure scenarios |
| `11_LIVE_SMOKE_CHECKLIST.md` | Manual in-game acceptance, including negative cases |
| `12_DELIVERY_REPORT_TEMPLATE.md` | Evidence report that cannot be satisfied by claims alone |
| `COPYPASTE_PROMPT.md` | Short self-contained kickoff text, also provided in ChatGPT reply |

## Prior-pass status

**Already claimed implemented** by previous agent (preserve and re-test): 12-character persistent device identifiers; room grouping; diagnostics and output monitoring; rotary switch position settings; past sprite shrink/rotation/data-cable changes; fauna detection; Russian speaker text; 32×64 core sprite; corrected ALL Lighting vs group controllers; speaker graph behavior; DeviceList/AirAlarm; atmos preset; visual graph inspector; responsive UI; rack UI stability. These are **not a request to rewrite everything**.

**Current TODO (10):** compact UI IDs; clearer room/device hierarchy; readable programmatic palette and widths; “Шаблон:” label; integration-kit sprite another 20% smaller; core screenshot occlusion/placement; relay popups and Shift+click/examine current channel; scanner zone detach/rebind; button-to-speaker misleading default link; real suppression actuation.

**Non-goals:** giant rearchitecture, global systems refactor, new framework, fabricated screenshots/test results, spending tokens to fill a quota. Coding time is a target, not a guarantee.
