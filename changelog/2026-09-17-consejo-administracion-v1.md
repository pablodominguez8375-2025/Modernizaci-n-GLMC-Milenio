# 2026-09-17 — Consejo de Administración v1

## Implementado en rama

- entidad de sesión del Consejo por Taller;
- asistencia histórica de integrantes e invitados;
- reconocimiento de los ocho cargos reglamentarios;
- validación server-side del cargo contra `OfficeAssignment` del mismo Taller y vigente en la fecha de la sesión;
- compatibilidad con códigos institucionales y variantes históricas normalizadas de nombres de cargo, sin ampliar los ocho cargos reglamentarios;
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

## Pendiente para cierre del PR

- CI + Showcase Demo verdes sobre el HEAD exacto final;
- integración del PR #91 a `dev` sin regresiones;
- actualización de la Línea Base de Drive con el SHA de merge y resultado final de los gates.

## Estado operacional

Hasta que el PR sea integrado y el paquete correspondiente sea desplegado/probado en QA, este incremento debe describirse como **implementado en rama/PR**, no como operacional en `srv01`.
