# Proyecto Centenario #694 — fase G, lectura segura de estado de carga inicial

**Fecha:** 24-10-2026 · **agente:** ChatGPT · **base:** PR #705 head `e8404b7f46db55370a1365065a5db890f38e4292` (aún no fusionado a dev). **PR stacked**: nunca fusionar #706 antes de que #705 esté correctamente integrado y actualizado a HEAD vivo de dev.

## Propósito
Preparar una consulta segura y **solo interna** del estado singleton `core.initial_load_windows` sin ningún permiso de modificación. La consulta retorna estado `not_opened`, `open`, `closed` o `unavailable`, versión y valores fijos de seguridad: `CanApply=false`, `WritesPerformed=0`. No devuelve ids de auditoría, digest de autorización jurídica, identidad del Máster, RUT ni valores importados.

## Implementación acotada
- `InitialLoadWindowStatusReader`: `AsNoTracking` en `BootstrapDbContext` al `Id=1`; si falta, está corrupto, DB no responde o falla la consulta, retornar únicamente `unavailable`, no inventar `open`. Si hay cancelación, propagarla.
- Pruebas sintéticas de estado y de mapeo de proyecciones con PostgreSQL efímero en la batería de integración existente. No escribir filas ni ejecutar migración desde este servicio; la migración es exclusivamente de #705.
- **No hay endpoint HTTP ni servicio de transiciones**, no se activa en la aplicación, no hay ingestión de CSV o datos reales. La futura pantalla de Claude necesita un endpoint propio con autorización al Máster `admin` verificada server-side y auditable, en otra etapa.
- Archivos propios: nuevo `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadWindowStatusReader.cs`, nuevo `backend/tests/PMGM.Api.Tests/InitialLoadWindowStatusReaderTests.cs` y este handoff. No editar código de Claude, migraciones, `Program.cs` ni `main`.

## Dependencias y controles
PR #705 debe pasar PMGM CI + Showcase + QA Installable exact HEAD, incluido reto de IAM laboratorio fallido por `mcli alias set`; no saltarse infra. Luego aplicar diff propio fase G sobre dev, reiniciar y validar 3/3 gates exact-head, merge solo a dev. Jurídico Ley 21.719 aún no ha autorizado nómina real; QA físico sigue sintético. No tocar main. Documentar GitHub y Drive.
