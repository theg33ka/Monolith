# KIAS Benchmark Lab — пакет заданий и критериев

**Назначение:** перед A/B-перфомансом провести end-to-end проверку физического `briarKIAS.yml`, затем создать полностью воспроизводимый 20-минутный тест нагрузочного профиля 30–40 гридов с Release-сборками Vanilla/KIAS, синхронизацией событий по серверным тикам, анализом лагспайков и сравнительным HTML-отчётом.

**Рабочий репозиторий:** https://github.com/theg33ka/Monolith , ветка `KIAS`.
**База движка:** `RobustToolbox` как git submodule; не заменять upstream RobustToolbox.
**Корабль:** `Resources/Maps/_Forge/Shuttles/Archive/Mercenary/briarKIAS.yml`.
**Reference:** https://github.com/Forge-Station/Monolith и https://github.com/Forge-Station/RobustToolbox .

## Очерёдность документов

1. `00_MASTER_PROMPT.md` — вставить кодинг-агенту первым целиком.
2. `01_BASELINE_AND_REPO_MAP.md` — фиксация исходников, диагностика штатных средств.
3. `02_BRIAR_AUDIT_AND_SCANNER_PREP.md` — настоящее состояние Briar, разрешённые правки.
4. `03_FUNCTIONAL_GATE.md` — обязательный реальный тест перед нагрузкой.
5. `04_PRESET_ACCEPTANCE_MATRIX.md` — индивидуальные проверки всех графов.
6. `05_REPLAY_ENGINE.md` — seed, журнал, исполнение и контроль отклонений.
7. `06_RELEASE_AB_AND_PARITY.md` — честная пара сборок и равные исходные условия.
8. `07_METRICS_PROFILING.md` — CSV, Prometheus, Tracy, backlog и аллокации.
9. `08_LOAD_SCENARIO.md` — 20 минут, 40 гридов, классы событий и распределение.
10. `09_REPORT_TRIAGE_AND_CRITERIA.md` — отчёт, баги, критерии готовности.
11. `10_RUNBOOK.md` — команды запуска и порядок операций для Windows.

## Критический порядок

`SOURCE AUDIT → MAP/SCANNER AUDIT → FUNCTIONAL GATE → разрешённые исправления → RECHECK → сборка сопоставимых A/B → dry-run 60 c → full A/B → анализ.`

**Запрещено:** делать заключение о производительности по недоработанной функциональности; считать тестом вручную вызванный KIAS trigger; подавлять исключения; самовольно менять другие устройства или архитектуру корабля; выдавать планы и моки за проведённые измерения.

## Зафиксированные наблюдения (исходники доступны на GitHub на 2026-10-09)

- Корабль `entityCount: 2391`: 13 `KiasRoomScanner`, 8 `KiasAdvancedRoomScanner`, 13 `KiasSuppression`, 10 `KiasSpeaker`, 6 `KiasWeaponFlashDetector`, 4 `KiasControllerRack`, 26 `KiasProgrammableController`, 200 `KiasDataCable`.
- Файловый каталог `controller_presets.yml` содержит **27** программ. Среди 26 физических карточек в сохранённом гриде отсутствует `fire-clear`; `quiet` присутствует, но пресет в каталоге отключён по умолчанию. Не считать это автоматически дефектом: подтвердить назначение и runtime-результат.
- На гриде не обнаружено `KiasLightController`; боевые пресеты `battle-impact`, `battle-flash`, `battle-manual` содержат профиль `LightController`. Нужна реальная проверка выходов.
- `KiasControllerProgrammer` не находится среди proto-групп грида; тест нового патча программатора провести на отдельном тестовом стенде, без перепланировки Briar.
- В YAML восемь `AirAlarm` без сериализованного `KiasIntegrated`; `FireAlarm` как proto-группа не обнаружен. Код `KiasSafetySystem` / `KiasProtocolSystem` фильтрует соответствующие тревоги по компонентам и интеграции. Проверять после загрузки; не утверждать заранее, что всё не работает.
- Слоты сканеров в сохранении отображаются как `ent: null`, но прототипы имеют `startingItem`; только live-состояние после MapInit определяет реальные установленные модули.
- У обычного сканера шесть слотов, четыре стартовых (Motion, Identity, Optical, Connector). У advanced шесть слотов и шесть стартовых (Motion, Identity, Threat, Spectral, Biometric, Radiation). Advanced не может вместить одновременно вообще все существующие модули без изменения вместимости.

Это **находки по тексту**, не результаты выполнения сервера. При изменении KIAS пересчитывать inventory динамически.

## Итоговые артефакты кодинг-агента

Исходники реализованного лабораторного инструмента; набор smoke/integration тестов; проверенные подготовленные YAML с diff; воспроизводимый сценарий `scenario.jsonl` + `manifest.json`; команды/GUI для запуска; заполненные протоколы реальных тестов; сырые логи; A/B HTML-отчёт с графиками и ссылками на конкретные тики. Все результаты в ignored `.kias-benchmark/` (или аналогичной безопасной папке).
