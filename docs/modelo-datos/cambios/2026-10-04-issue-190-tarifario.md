# Registro de cambio de datos — Issue 190 — Tarifario v2

PO Pablo, instrucciones v2 del 04-10. ChatGPT, rama feature/tariff-decree-v2-20261004-gpt, PR #318, base c81f68dabf52f03875f947a23964c91d30994d56. Corte anterior [DB-006](../PMGM-DB-006-acceso-efectivo-tesoreria.md). Estado: implementación de rama, sin instalación.

Diccionario, relaciones verificables, anterior/nuevo, clasificación, API, migración/recuperación y pruebas en [DB-007](../PMGM-DB-007-tarifario-decretos.md). Alcance preciso: nuevas versiones de tarifario, geografía Ficha y campos de trazabilidad de planes/cargos/derechos. No pretende documentar íntegramente las tablas financieras preexistentes. Se preservan sus PK/FK, importes, fechas, monedas, recibos, asignaciones y cierres; no retira columnas históricas. TreasuryTerritory cambia a derivado compatible y su escritura independiente se rechaza. Modelo implementado no acredita schema instalado.

Metadatos financieros institucionales, sin datos reales ni secretos. Autorización de administración Gran Tesorería, lectura local limitada a Taller y permiso dinámico view; auditoría append-only. Snapshot versión referenciado nullable para legacy, Restrict sin borrado en cascada. Sin cambios de retención, conversión de monedas, Hospitalaria o firmas.

Validación: paridad del seed, vigencia y faltantes, preservación de histórico por snapshots, registro/auditoría/ExpectedVersion PostgreSQL, migración y demo. Resultados CI/Showcase/QA por SHA exacto se completan en recibo de PR y [handoff](../../handoffs/2026-10-04-chatgpt-tariff-decree-v2.md). SHA final y SOURCE_SHA/checksum se registran sólo después de verificar gates y lectura pública. Main congelado y srv01/QA física/UAT pausados.
