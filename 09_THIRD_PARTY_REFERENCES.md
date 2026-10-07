# Third-party references and attribution checklist

This handoff uses external projects as design/code references. The coding agent must verify the exact file headers/history at the commit actually used before copying/adapting code.

| Project | Expected license context | Intended use |
|---|---|---|
| `/tg/station` Wiremod / Integrated Circuits | AGPL v3 | Primary UX/dataflow/serialization design reference; prefer native C# implementation |
| WizDen `space-station-14` DeviceLinking | MIT upstream | Reuse existing local Monolith DeviceLink APIs; adapt small APIs only if required |
| Goob Station nested factory filters | AGPL-3.0-or-later / REUSE | Conceptual nested-filter reference; no dependency unless needed |
| Forge/Monolith | Project REUSE/mixed-history policy | Target codebase; follow per-file rules |

For any substantive copied/adapted code, record:

- source project;
- exact commit;
- exact source path;
- license;
- original authors/copyright from headers/history;
- whether copied, adapted or conceptual reference.

The two sprites in `assets/` are user-supplied project assets. Add repository-required metadata, but do not invent ownership/license terms not provided by the project owner.
