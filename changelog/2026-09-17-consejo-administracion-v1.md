# 2026-09-17 — Consejo de Administración v1

## Estado final del incremento

- PR funcional: #91.
- Rama funcional: `feature/consejo-administracion-v1`.
- HEAD exacto validado antes del merge: `666d622cd2fad6dd51fb0974c5c329ecba4e10f8`.
- PMGM CI: **success** sobre el HEAD exacto.
- PMGM Showcase Demo: **success** sobre el HEAD exacto.
- Merge a `dev`: **completado**.
- Merge SHA: `913c89e7f5e4a5a4c64fd164013d111d2265e41e`.

## Implementado e integrado en dev

- entidad de sesión del Consejo por Taller;
- asistencia histórica de integrantes e invitados;
- reconocimiento de los ocho cargos reglamentarios;
- validación server-side del cargo contra `OfficeAssignment` del mismo Taller y vigente en la fecha de la sesión;
- compatibilidad con códigos institucionales y variantes históricas normalizadas de nombres de cargo, incluyendo `Tesorería`, sin ampliar los ocho cargos reglamentarios;
- confirmación auditable de quórum calificado sin umbral numérico inventado;
- acuerdos categorizados por competencia normativa;
- remisión obligatoria a Cámara del Medio para presupuesto, programa anual, cambio de oficiales electivos y propuesta de CRF;
- revisiones de Tesorería, Hospitalaria y Columnas;
- migración PostgreSQL;
- API autenticada y segregada por Taller;
- panel React integrado en Gestión Logial;
- datos ficticios equivalentes para GitHub Pages/showcase;
- pruebas unitarias de roles, correspondencia de cargos, voto, derivación a Cámara y regla de quórum;
- documentación arquitectónica `PMGM-ARCH-008`.

## Corrección de cierre de CI

El único fallo final previo al merge correspondía a la compatibilidad de la etiqueta `Tesorería` con el rol institucional `lodge_treasury`. La política `LodgeCouncilPolicy` fue corregida manteniendo la prueba explícita de esa variante. Con esa corrección, las 247 pruebas backend y el resto de los gates del workflow quedaron verdes.

## Estado Demo GitHub Pages

El código del showcase y su build fueron validados por **PMGM Showcase Demo = success** en el HEAD exacto previo al merge. Esta evidencia confirma integridad del artefacto demostrativo versionado; no sustituye la comprobación independiente de una publicación live de GitHub Pages posterior al merge.

## Estado QA

El incremento está integrado en `dev` y es reproducible desde la línea técnica vigente. **No se declara desplegado ni probado operacionalmente en `srv01` en este cierre**, porque no se ejecutó una intervención directa sobre el servidor QA desde esta sesión.

## Siguiente hito

- sincronizar la Línea Base Maestra de Google Drive con el merge SHA y los resultados de CI/Showcase;
- generar/usar el artefacto instalable correspondiente al corte vigente;
- desplegar en QA `srv01` cuando exista acceso operativo al servidor;
- ejecutar prueba QA/UAT del Consejo de Administración antes de cualquier promoción estable a `main`.
