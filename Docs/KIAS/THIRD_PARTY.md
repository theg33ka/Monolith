# Источники контроллеров KIAS

Контроллеры реализуются нативно на C#/Robust UI. DM/React и Factory runtime не переносятся. Таблица различает изученные идеи и фактическое использование локальных API.

| Проект | Версия | Путь | Лицензия / основание | Использование | Атрибуция |
|---|---|---|---|---|---|
| tgstation/tgstation | `e5b5039dfefbfed07c4f5960060e83e3935889ff` | `code/modules/wiremod/core/{integrated_circuit,component,port,duplicator}.dm`; `tgui/packages/tgui/interfaces/IntegratedCircuit/{index,ComponentMenu,Port}.jsx` | AGPL v3, корневой `LICENSE` этой версии | Conceptual: типизированные порты, fan-out, позиции, pan/zoom, сериализация. Код не копируется. | tgstation contributors; [исходники](https://github.com/tgstation/tgstation/tree/e5b5039dfefbfed07c4f5960060e83e3935889ff/code/modules/wiremod) |
| Monolith / WizDen DeviceLinking | локальная база `a024edc7a3` | `Content.Shared/DeviceLinking/*`, `Content.Server/DeviceLinking/Systems/*`, `Content.Client/NetworkConfigurator/*` | История смешанная; применяются правила локального проекта. Upstream MIT не объявляется лицензией всех изменённых файлов. | Reused: существующие API, защита перегрузки, source/sink. Идеи UI и логических вентилей. | Существующая атрибуция сохраняется. |
| Локальный Goob Factory | последний коммит файла `a75a27b456a5e83fbbd1a8df43d5c13bd9abb7e4` | `Content.Shared/_Goobstation/Factory/Filters/{CombinedFilterComponent,AutomationFilterSystem}.cs` | Файлы без собственного SPDX-заголовка; правила смешанной истории проекта | Conceptual: составные предикаты. Factory не импортируется, код не копируется. | Goob contributors; локальные файлы не изменяются. |
| Hurtsay для Forge | предоставлены пользователем в пакете контроллеров | `Resources/Textures/_Forge/KIAS/KiasControllerRack.rsi`, `KiasProgrammableController.rsi` | CC-BY-SA-3.0; пользователь указал автора и разрешил выбрать лицензию | Supplied artwork; PNG без изменения пикселей | `Hurtsay for Forge` в RSI metadata |

Ссылки закреплены на изученной версии, а не на меняющемся `master`. Новые файлы графа являются самостоятельной реализацией, а не буквальным портом внешнего кода.
