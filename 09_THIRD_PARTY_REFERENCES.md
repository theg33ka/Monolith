# UI and system references for this pass

## In-repo UI reference: Shuttle Console

Primary visual quality reference:

- `Content.Client/Shuttles/UI/ShuttleConsoleWindow.xaml`
- `Content.Client/Shuttles/UI/ShuttleNavControl.xaml.cs`

Useful ideas:

- nested dark panel frames;
- clear top-level modes;
- constrained status/info panels;
- high information density without raw debug text;
- consistent margins and alignment.

Do not copy a shuttle-specific layout literally. Reuse existing styles/components when suitable.

## In-repo configuration reference: NetworkConfigurator

- `Content.Client/NetworkConfigurator/*`
- `Content.Server/DeviceNetwork/Systems/NetworkConfiguratorSystem.cs`
- `Content.Server/DeviceNetwork/Systems/DeviceListSystem.cs`

This is authoritative for the normal multitool device list/link workflows KIAS must preserve.

## Robust/SS14 UI

Prefer native Robust UI/XAML patterns. Avoid adding a custom rendering framework for ordinary controls. The graph canvas may remain custom drawn where appropriate.

## External Wiremod ideas

Wiremod/Integrated Circuits remains conceptual inspiration for typed ports, graph discoverability and inline values. Do not import unrelated runtime/UI architecture wholesale.
