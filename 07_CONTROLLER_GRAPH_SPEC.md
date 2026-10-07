# Controller graph UX and semantics specification

## Visual grammar

Port colors may remain type-based, but color is never the only explanation. Tooltip and inspector show type text.

Recommended player-facing type labels:

- gold: Импульс;
- green: Да/Нет;
- cyan: Число;
- pink: Строка;
- orange: Объект;
- purple: Перечисление/domain.

## Node layout

Header: localized node/profile name.

Secondary line:

- internal node: configured value/state when meaningful;
- selector: matched count and compact filter summary;
- SPECIFIC: device name + online state.

Then ports.

Examples:

```text
Строка
"Внимание: разгерметизация"
                 Значение ●
```

```text
ALL Освещение
12 найдено
● Установить      Есть устройства ●
● Включить             В сети ●
● Выключить          Источник ●
```

Do not literally hardcode ASCII; this is information hierarchy only.

## Inspector sections

Use visually distinct sections, for example:

- `Узел` — title/status;
- `Выборка` — room/group only when selector supports them;
- `Параметры` — node-specific config;
- `Входы и выходы` — help cards;
- `Действия` — remove.

No unlabeled `OptionButton`. No raw direction/type strings.

## Value editors

- bool = checkbox/toggle with label;
- enum = dropdown;
- seconds = numeric line edit/spin-like field with unit in label;
- string = line edit; safe max-length indication if useful;
- number = validated numeric field;
- compare = labeled dropdown.

Save only changed/valid relevant config. Hidden stale config must not accidentally affect unrelated node semantics.

## Selector filters

`Room`/`Group` are filters. Labels must be `Фильтр помещения` / `Фильтр группы`.

Show only if the selected profile actually supports the concept. Empty = no filter.

## Wiring feedback

When starting a wire:

- incompatible ports visibly dim as now;
- on release over incompatible port, show short tooltip/status reason;
- same direction -> «Нельзя соединить два входа/два выхода»;
- type mismatch -> show both friendly types;
- Signal/Bool mismatch -> suggest Edge or Latch/Toggle.

## Port help

Hover tooltip:

`Объявить — Импульс`<br>
`Произносит текст, поданный на вход «Сообщение».`

Inspector should list help without requiring hover for discoverability.

## Search/palette

Search should match:

- profile display name;
- node name;
- device name;
- localized port names where practical.

Avoid several visually identical `ALL Порты устройства` entries. Generic DeviceLink fallback title should include meaningful source/sink port names or device family context.

## Window behavior

- no horizontal inspector scrollbar;
- palette has controlled width and ellipsis/tooltips;
- inspector wider than current 205–240 px baseline where space allows;
- canvas gets remaining width;
- at min width side panels may become narrower, but text remains intentional and usable;
- do not let long localized text expand the entire window off-screen.

## Save state

Dirty/saved status must be obvious but compact. `WRITE` is primary action, `Discard` secondary/destructive-ish, `Eject` disabled while dirty as today unless product behavior intentionally changes.
