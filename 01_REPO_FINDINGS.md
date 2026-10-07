# KIAS repo findings — correction pass

Baseline: `3e067d069ebb45b192db06db8a38354f9f053cca`.

## Подтверждённые находки в текущем коде

### Controller card metadata

`KiasControllerUiSystem.Edit(... Write ...)` копирует draft в `controller.Program`, обновляет revision/enabled, но не меняет entity metadata физической карточки. Поэтому rack видит program name, а предмет остаётся прототипным `KIAS programmable controller`.

### Inspector показывает всё всем

`KiasControllerWindow.SelectNode()` сейчас безусловно создаёт поля room/group/text/number/seconds/enum/bool/comparison. Большинство полей для большинства node kinds ничего не делает. Это источник основной путаницы на скриншоте.

### Значения constants скрыты в inspector

`KiasGraphCanvas.Draw()` рисует title/detail/ports, но не рисует `node.Config` value. Из-за этого String/Number/Bool/Timer невозможно читать по схеме без выбора узла.

### Port descriptions инфраструктурно есть, UX нет

`KiasGraphPort.Description` существует, но native controller profiles почти везде используют общий `kias-controller-port-description`. Inspector печатает только raw `Direction Id: Type`; описание не используется.

### Signal и Bool намеренно разные

`KiasGraphMachine.Output()` всегда распространяет `Signal`, а data outputs подавляют одинаковое повторное значение. Compiler требует точное совпадение типов. Это правильная строгая модель, но UI не объясняет её и молча отвергает несовместимые wires.

### Speaker message path

`KiasControllerIoSystem.Command()` для Speaker читает `read("Message")`, а не `node.Config.Text`. Поэтому generic inspector field «Текст» на Speaker ложный.

### ALL speaker throttling risk

Speaker command формирует emission key без target. При ALL broadcast несколько адресных speaker commands могут попасть в общий emission gate под одним ключом. Требуется regression test и target-aware semantics.

### Lighting profile mismatch

`LightController` profile привязан к `KiasLightController`. Поэтому `ALL Освещение` в UI означает controllers, а не fixtures. Integrated powered light получает `KiasIntegrated`/`KiasDevice` и generic On/Off DeviceLink, но не LightController profile.

### Integrated OFF -> cannot ON

`KiasSystem.Rebuild()` ставит `NoPower` раньше проверки integrated control path, а `KiasSystem.IsOnline()` дополнительно требует `_power.IsPowered(device)`. `KiasIntegrationSystem.OnSignal()` выключает target через `SetPowerDisabled`. В итоге control endpoint сам исключается из KIAS после OFF.

### Group mode service tool

`KiasServiceSystem` в режиме Group берёт group name из `KiasServiceToolComponent.Message`. `KiasLocalWindow` для service state показывает generic message field. Backend формально может назначать group, но UI semantics непрозрачны.

### Air alarm использует DeviceList, не только DeviceLink

`AirAlarm` prototype содержит одновременно `DeviceList`, `DeviceLinkSource`, `DeviceNetwork`, `AtmosAlarmable`. Штатное управление sensors/vents/scrubbers опирается на DeviceList + DeviceNetwork. Ручные source/sink links не заменяют этот список.

`NetworkConfiguratorSystem.DetermineMode()` особенно чувствителен к entities, у которых одновременно есть DeviceList и DeviceLink ports. Нужно воспроизвести пользовательский regression тестом, прежде чем менять generic logic.

### Depressurization preset существует

`controller_presets.yml` содержит preset `atmosphere`, локализованный как «Разгерметизация». Значит текущий симптом — не «preset отсутствует», а end-to-end trigger path не происходит в реальном сетапе.

### UI reference

`ShuttleConsoleWindow.xaml` использует вложенные PanelContainer, визуальные рамки, отдельную mode bar и явную иерархию. KIAS окна в основном собираются программно в плоские BoxContainer + LineEdit, что объясняет debug-like вид.

## Рискованные области

- глобальный `DeviceLinkSystem` уже изменён KIAS и должен сохранять vanilla behavior;
- `NetworkConfiguratorSystem` — общий gameplay code; менять только по воспроизводимому тесту;
- power semantics нельзя ослаблять для всех KIAS devices ради integrated endpoints;
- не превращать Lighting profile resolver в prototype-specific switch;
- enum domain требует schema extension/migration без поломки сохранённых controller cards/presets.
