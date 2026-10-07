# Source map for the correction pass

## Graph UI

- `Content.Client/_Forge/KIAS/Controllers/KiasControllerWindow.cs`
  - toolbar, palette, inspector; current all-fields-for-all-nodes behavior.
- `Content.Client/_Forge/KIAS/Controllers/KiasGraphCanvas.cs`
  - node drawing, values, ports, wires, pan/zoom.
- `Content.Client/_Forge/KIAS/Controllers/KiasControllerLabels.cs`
  - localized profile/port/error labels.
- `Content.Client/_Forge/KIAS/KiasLocalWindow.cs`
  - generic local/service/device configuration UI.
- `Content.Client/_Forge/KIAS/KiasWindow.xaml(.cs)`
  - management UI.

## Graph schema/runtime

- `Content.Shared/_Forge/KIAS/Controllers/KiasControllerProgram.cs`
- `Content.Shared/_Forge/KIAS/Controllers/KiasGraphCatalog.cs`
- `Content.Shared/_Forge/KIAS/Controllers/KiasGraphCompiler.cs`
- `Content.Shared/_Forge/KIAS/Controllers/KiasGraphMachine.cs`
- `Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs`
- `Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem*.cs`
- `Content.Server/_Forge/KIAS/Controllers/KiasControllerUiSystem.cs`

## Speaker/audio

- `Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.Commands.cs`
- `Content.Server/_Forge/KIAS/KiasSafetySystem.cs`
- `Content.Server/_Forge/KIAS/KiasDisplaySystem.cs`
- `Content.Server/_Forge/KIAS/KiasDisplaySystem.Audio.cs`
- `Content.Shared/_Forge/KIAS/KiasAudio.cs`
- `Content.Shared/_Forge/KIAS/KiasDisplay.cs`

## Lighting/integration/power

- `Content.Server/_Forge/KIAS/KiasActuatorSystem.cs`
- `Content.Server/_Forge/KIAS/KiasIntegrationSystem.cs`
- `Content.Server/_Forge/KIAS/KiasSystem.cs`
- `Content.Shared/_Forge/KIAS/KiasActuatorComponents.cs`
- `Resources/Prototypes/_Forge/KIAS/controller_profiles.yml`
- `Resources/Prototypes/_Forge/KIAS/kias.yml`
- `Resources/Prototypes/_Forge/KIAS/devices.yml`

## NetworkConfigurator / DeviceList / atmosphere

- `Content.Server/DeviceNetwork/Systems/NetworkConfiguratorSystem.cs`
- `Content.Server/DeviceNetwork/Systems/DeviceListSystem.cs`
- `Content.Shared/DeviceNetwork/Components/DeviceListComponent.cs`
- `Content.Server/DeviceLinking/Systems/DeviceLinkSystem.cs`
- `Content.Server/Atmos/Monitor/Systems/AirAlarmSystem.cs`
- `Content.Server/Atmos/Monitor/Systems/AtmosAlarmableSystem.cs`
- `Resources/Prototypes/Entities/Structures/Wallmounts/air_alarm.yml`

## Presets/localization

- `Resources/Prototypes/_Forge/KIAS/controller_presets.yml`
- `Resources/Locale/ru-RU/_Forge/kias.ftl`
- `Resources/Locale/en-US/_Forge/kias.ftl`
- `Resources/Prototypes/_Forge/KIAS/controllers.yml`
- `Resources/Prototypes/_Forge/KIAS/crafting.yml`

## UI quality references in the same repo

- `Content.Client/Shuttles/UI/ShuttleConsoleWindow.xaml(.cs)`
- `Content.Client/Shuttles/UI/ShuttleNavControl.xaml.cs`
- `Content.Client/NetworkConfigurator/*`

## Existing tests to extend

- `Content.IntegrationTests/Tests/_Forge/KIAS/KiasControllerLayoutTests.cs`
- `KiasControllerRuntimeTests.cs`
- `KiasControllerEditorTests.cs`
- `KiasSpeakerTests.cs`
- `KiasDeviceTests.cs`
- `KiasParityTests.cs`
- `KiasVentilationTests.cs`
- `KiasPersistenceTests.cs`
- `KiasFleetTests.cs`

Prefer extending the closest existing suite instead of creating dozens of tiny test fixtures without reason.
