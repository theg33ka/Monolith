# KIAS: validation, 2026-10-08

Tested branch: `KIAS`. Base HEAD: `3e067d069ebb45b192db06db8a38354f9f053cca`; evidence refers to the correction patch working tree, including removal of temporary smoke commands. The final commit is recorded in Git history.

## Automated checks

```powershell
dotnet restore Content.IntegrationTests/Content.IntegrationTests.csproj -m:2 -nr:false
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:2 -nr:false --filter 'FullyQualifiedName~Tests._Forge.KIAS' --logger 'console;verbosity=normal'
git diff --check
```

- Restore succeeded. Original baseline: 46/46.
- Original regressions reproduced before their fixes: WRITE metadata, ALL Speaker and integrated atmosphere DeviceList.
- Final production build and full KIAS suite: **58 passed, 0 failed, 0 skipped**. This includes controller compiler/runtime/editor/persistence/layout, device integration, native atmosphere pipeline, audio/local speech, fleet and stress tests.
- Build has zero errors; repository/analyzer warnings remain and were not treated as clean-warning validation.
- After removing temporary commands, the ordinary graphical client also passed assembly sandbox verification and reached MainScreen with the sandbox enabled: `.kias/clean-client.log`; stderr was empty.
- `git diff --check`: passed after preserving Markdown hard breaks as `<br>`.

Local evidence: `.kias/correction-restore.log`, `.kias/correction-baseline.log`, `.kias/correction-reproductions.log`, `.kias/correction-final-tests.log`.

The new regression suite covers both ALL Lighting OFF→ON with native APC/light state and ALL LightController OFF→ON through the physical group controller; missing DATA and native KIAS power; SPECIFIC and ALL local speaker messages with selected/excluded senders and cooldown; native/integrated List mode; ordinary non-KIAS Link; Group UI refresh and selector cache; native AirAlarm device registration/gas/Danger → real atmosphere preset → AtmosClear. Persistence covers names and old/new service-tool groups.

Layout checks use **850×500, 1200×720, 1600×900**, empty/populated graphs and long labels, every node kind, inspector capabilities/width, localized help, summaries and mismatch feedback. Native RU localization validates all spawnable KIAS entities and graph ports; static RU/EN audit checks duplicate/missing locale keys and all 91 prototype names/descriptions.

## Actual graphical client/server smoke

Ran `bin/Content.Server/Content.Server.exe` on loopback port 1213 and `bin/Content.Client/Content.Client.exe`, user `KiasLocalSmoke`, RU locale, **1600×900, UI scale 1**. A temporary debug scene spawned physical KIAS machines, DATA/core, two speakers and actual cards. All five windows opened through server `TryOpenUi` with native BUI states; the client rendered screenshots through Clyde.

| Window | Standard | Minimum | Local screenshots |
|---|---|---|---|
| Programmer | 1200×720 | 850×500 | `.kias/programmer-standard.png`, `.kias/programmer-minimum.png` |
| Rack | 650×420 | 430×320 | `.kias/rack-standard.png`, `.kias/rack-minimum.png` |
| Service tool, Group | 430×380 | 360×300 | `.kias/service-standard.png`, `.kias/service-minimum.png` |
| Light group controller | 430×380 | 360×300 | `.kias/lighting-standard.png`, `.kias/lighting-minimum.png` |
| Management | 900×650 | 650×450 | `.kias/management-standard.png`, `.kias/management-minimum.png` |

Inspected these ten frames for readable palette, bounded node summaries, usable toolbar/dropdowns, separate inspector, wrapped local details, grouped sections, minimum-size vertical scrolling and pinned local actions. Narrow windows intentionally shorten long captions with full tooltips; scrolling exposes the remaining fields/content.

Native client UI messages additionally exercised Rename → WRITE → Eject. The replicated physical card name matched the submitted name: `.kias/native-write-result.txt` reports PASS. The rack's running program produced local speech from both speakers in the graphical client. `.kias/native-bui-complete.txt` confirms ten frames; logs are `.kias/smoke-client.log` and `.kias/smoke-server.log`.

## Exact limits

- Live interaction was driven through temporary native game commands/UI messages. It was not a manual mouse/keyboard session; the available desktop automation runtime failed to initialize. Dragging, wheel navigation and hover timing were not exercised as user gestures. Their event handlers were reviewed; automated tests cover layout, help strings and connection compatibility.
- The debug scene bypassed machine power and granted local debug access to isolate BUI rendering; real APC/control-plane behavior is covered by integration regressions.
- Screenshot saving used `ROBUST_DISABLE_SANDBOX=1` only for the temporary local client process. Both temporary C# commands were removed before final production build/tests; no global setting or shipped debug command was added.
- All ten images are local ignored artifacts, not media attached to a published PR. The PR checklist remains explicit about this.
- Live screenshots cover RU at UI scale 1. EN descriptions/keys and other sizes have automated/static coverage; other display scales and a public multiplayer server were not tested.
- The default server map logged an unrelated pre-existing `invalid FTL state Available` warning. No KIAS assertion or native UI crash occurred during the completed capture.

Compatibility: version 1 and existing profile IDs remain; a legacy enum constant spanning different known domains now needs separate constants. See [CORRECTION_REPORT.md](CORRECTION_REPORT.md).
