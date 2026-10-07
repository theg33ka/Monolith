# Presets, legacy configuration and regressions

## Existing presets remain graph programs

Do not reintroduce hardcoded protocol actions to fix regressions. Current controller presets remain the automation policy layer.

`atmosphere` is the graph preset localized as «Разгерметизация».

## Depressurization acceptance path

Expected upstream chain:

1. Air sensor is part of the AirAlarm device list / atmos network.
2. Sensor crosses threshold through normal atmos logic.
3. AirAlarm/AtmosAlarmable enters Danger and performs normal alarm behavior.
4. KIAS observes the normal event and emits `Automation.AtmosDanger` plus context.
5. `atmosphere` controller graph receives it.
6. Graph actions execute.

The preset may be corrected if its graph is wrong, but tests must not bypass steps 1–4.

## DeviceList is not legacy garbage

`DeviceListComponent` is a current native SS14 mechanism used by AirAlarm and other systems. Preserve it.

The programmable graph's generic DeviceLink ports are an additional automation interface. They do not mean players should manually wire every vent/scrubber/sensor port to replace an AirAlarm device list.

## NetworkConfigurator UX

A multitool/configurator has list and link modes. For entities that have both DeviceList and DeviceLink capabilities, both workflows must remain reachable and predictable.

Regression target:

- player stores devices in configurator;
- interacts with AirAlarm in list mode;
- gets list configuration UI/actions;
- Set/Add writes the device list;
- interacting in link mode still exposes port linking.

If current mode auto-selection makes the list effectively inaccessible, fix it without breaking explicit link mode.

## Preset compatibility after profile rename

If user-facing `LightController` is split into `Lighting` and `LightGroupController`, inspect every preset:

- presets that intend group-wide configured lighting should use group controllers;
- presets that intend every directly controllable lamp may use direct Lighting;
- keep stable serialized IDs/migration where possible; do not silently reinterpret old saved cards.
