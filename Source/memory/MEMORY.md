# Project Memory Index (актуализировано 29.08.2026)

## Стартовая точка
- [FDS5_FASTCOARSE_HANDOFF_3.md](FDS5_FASTCOARSE_HANDOFF_3.md) — ЕДИНСТВЕННЫЙ входной документ новой сессии: статус, билды, доказательная база, правила, открытые задачи, команды

## Двухрежимное ускорение (текущий трек)
- [FDS5_DUAL_MODE_DESIGN.md](FDS5_DUAL_MODE_DESIGN.md) — видение режимов A (готов) и B (не начат)
- [FDS5_FLAME_SHEET_DESIGN.md](FDS5_FLAME_SHEET_DESIGN.md) — дизайн режима A; §4 актуализирован под конус R26d

## Technical Reference (активный контент-трек)
- [FDS5_TR_PLAN.md](FDS5_TR_PLAN.md) — план 14 глав (v1.1), регламент: научная терминология, БЕЗ внутренних ревизий/кодовых имён прогонов, сквозное сравнение с FDS6 (исходник `E:\FIREGOAWAY\GitHub\FDS_source\fds_full_bakup_new\Source`), прогресс-трекер, глоссарий соответствий «внутренний термин → научный»
- [FDS5_TECHNICAL_REFERENCE_RU.md](FDS5_TECHNICAL_REFERENCE_RU.md) — русский справочник (v0.3: гл. 1 заполнена; гл. 4, 5, 7 частично; научный стиль)
- [FDS5_TECHNICAL_REFERENCE_EN.md](FDS5_TECHNICAL_REFERENCE_EN.md) — английское зеркало (структурно идентично)
- [FDS5_TR_NEXT_SESSION_PROMPT.md](FDS5_TR_NEXT_SESSION_PROMPT.md) — промпт сессии наполнения TR (актуализирован 29.08 вечер: глава 2 следующая, правила терминологии/стиля/сравнения с FDS6)

## Механизмы форка (технические справочники)
- [FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md](FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md) — MLR/CCC: fixed + procedural
- [MLR_BASED_HRR_GUIDE.md](MLR_BASED_HRR_GUIDE.md) — пользовательское руководство MLR-based HRR
- [SOAP_IMPLEMENTATION.md](SOAP_IMPLEMENTATION.md), [FDS5_SOAP_AND_PRESSURE_BLENDING.md](FDS5_SOAP_AND_PRESSURE_BLENDING.md) — решатели давления
- [LAZY_SOAP_MANUAL.md](LAZY_SOAP_MANUAL.md) — LAZY-SOAP + FIXED_DT_OVERRIDE
- [CUTFACE_IMPLEMENTATION.md](CUTFACE_IMPLEMENTATION.md) — cut-face, ALPHA_CF_W
- [Cl-HCL_integration_in_FDS_Chemistry.md](Cl-HCL_integration_in_FDS_Chemistry.md) — химия Cl/HCl
- [COMPATIBILITY_REPORT.md](COMPATIBILITY_REPORT.md) — 24-пунктный чек-лист совместимости (реализовано)

## Исходниковедение (первоисточники для TR)
- [How_HRR_is_calculated_originaly.md](How_HRR_is_calculated_originaly.md), [How_MLR_is_calculated_originaly.md](How_MLR_is_calculated_originaly.md), [How_CCC_is_calculated_originaly.md](How_CCC_is_calculated_originaly.md) — сток-FDS5 бэкграунд
- [MESH_ARCHITECTURE_ANALYSIS.md](MESH_ARCHITECTURE_ANALYSIS.md) — архитектура мешей
- [INTEGRATED_INTENSITY_to_INCIDENT_HEAT_FLUX_Proof.md](INTEGRATED_INTENSITY_to_INCIDENT_HEAT_FLUX_Proof.md) — вывод для датчиков
- [FDS5_DERIVED_SPECIES_SLCF.md](FDS5_DERIVED_SPECIES_SLCF.md), [FDS5_HRR_CSV_COMPATIBILITY.md](FDS5_HRR_CSV_COMPATIBILITY.md) — форматы выходов

## Контекст проекта
- [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) — общий бэкграунд проекта (цели, архитектура)
- [FDS5_FDS6_COMPATIBILITY_PLAN.md](FDS5_FDS6_COMPATIBILITY_PLAN.md) — дорожная карта совместимости

## Архив
`archive\` — 34 исторических документа (хэндоффы-1/2, фазовые планы, bbfrac/DT_ADAPT-эксперименты, старые промпты). Не читать для работы; только археология по требованию владельца.
