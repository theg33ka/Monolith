# Изменения v2 относительно KIAS_Optimization_FullGame_Lab_20261010.zip

1. `03_ROOM_TOPOLOGY.md` **полностью переписан**: архитектурный контракт, wall-mounted seed, static doors даже open, shared doorway, диагональные стены, runtime caches, dirty queues и safe unknown statuses, code-level псевдокод.
2. Найденный новый критический зависимый путь: `KiasIntegrationSystem.Reconcile/CanControl` также использует radius! Требование перевода на room index добавлено в `00`, `01`, `02`, `03`, `07`, `09`.
3. Уточнены `KiasDisplaySystem.LocalUi`, `KiasCoverageShape`, KiasScannerState/SetSensorRange; старые Range UI/тесты должны быть мигрированы без удаления старого DataField.
4. Добавлены `08_SCANNER_ACCEPTANCE_MATRIX.md` (G01–G15, I01–I09, S01–S08), `09_SCANNER_MIGRATION_AND_UI.md`, `10_SCANNER_PERF_AND_REBUILD.md`.
5. В `04/05/06` добавлены реальные 40-grid room waves, отдельные метрики rebuild/latency и `RoomGate` перед новым A/B.
6. `07` дополнен обязательной проверкой перепривязки дверей/вентиляции, всех потребителей coverage, room-map для Briar и чисел до/после.
7. Мастер-промпт теперь обязательно требует пройти **все** новые файлы и приложить доказательства G/I/S.

Рабочий репозиторий на GitHub **не изменялся**; это пакет заданий для локального агента. Source audit: `KIAS@97178c6cfed4b6eda7855759da0f796b54c55ca0`.
