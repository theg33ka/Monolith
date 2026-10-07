# Controller device/profile semantics — corrected

## Profile: Lighting

Meaning: every directly KIAS-controllable powered light fixture on the current grid.

Inputs:

- `Set : Bool` — `Да` turns the fixture on, `Нет` turns it off.
- `On : Signal` — turn on.
- `Off : Signal` — turn off.

This profile must include integrated compatible light fixtures. It must not mean KiasLightController.

## Profile: LightGroupController

Meaning: physical KIAS light group controller.

Inputs:

- `Set : Bool` — set entire configured group state;
- `On : Signal` — turn group on;
- `Off : Signal` — turn group off.

Selector group filter, if exposed, filters controllers by configured group. The controller's own group setting is configured in its device UI/service workflow, not by silently reusing selector config.

## Profile: Speaker

Data inputs:

- `Message : String` — text used by next announce/alarm action;
- `Channel : AudioChannel enum` — localized dropdown;
- `Key : String` — event identity / deduplication key; normally propagated automatically, optional for simple scripts.

Action inputs:

- `Announce : Signal` — speak as normal notification;
- `Alarm : Signal` — speak with alarm semantics.

Help text must explain `Key` as event identity, not security key.

## Profile: Recorder

- `Message : String` — text;
- `Key : String` — event/dedup key;
- `Record : Signal` — append record.

## Automation/Core event outputs

`EventKey` should be labeled `ID события` / `Ключ события` and described as stable identity of the source/event used to separate independent repeats. It is usually wired into Cooldown/Speaker/Recorder and normally does not need manual editing.

`Message`, `Value`, `Disposition` are contextual data for the current event and need per-port descriptions.

## Service metadata outputs

- `$MatchedCount` — number of devices matching selector;
- `$OnlineCount` — online/control-plane available matches;
- `$HasAny` — true if at least one match;
- `$Source` — actual device that emitted the most recent external event.

Use localized display names; technical IDs stay internal.

## Generic DeviceLink bridge

Generic existing source/sink ports remain `Signal` unless a richer native KIAS profile exists.

The bridge must not replace `DeviceListComponent`. A device may participate in both DeviceList/DeviceNetwork and DeviceLink simultaneously.

## Port description rule

Every exposed port must answer in one or two lines:

1. What does this port mean?
2. What data/action does it expect/produce?

Examples:

- `Установить · Да/Нет` — «Да включает выбранный свет, Нет выключает.»
- `Объявить · Импульс` — «Произносит строку из входа “Сообщение”.»
- `Сработал · Импульс` — «Одно событие при обнаружении вспышки оружия.»
- `Источник · Объект` — «Устройство или сущность, вызвавшая текущее событие.»
