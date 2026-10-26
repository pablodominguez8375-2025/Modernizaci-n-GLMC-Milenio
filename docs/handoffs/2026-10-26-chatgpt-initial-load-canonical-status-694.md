# PMGM #694 — Carga inicial: estados institucionales canónicos en validación previa

Corte de origen: `dev@07864aab4be1df58dc9298044b0fa7003d5d3fc5`, exclusivo backend ChatGPT. Coordinación: Claude UI #533 en repositorio; PR #741 seguridad Master y #737 operador físico QA tienen ramas independientes. No se edita ninguno de sus archivos.

## Defecto y alcance
`InitialLoadPreflightPolicy.Inspect` verificaba solo que `institutionalStatus` fuera no vacío en `Hermanos.csv`. Esto permitía estados inventados y futuras inconsistencias en carga inicial. El módulo institucional ya publica el catálogo oficial en `MembershipCodes.InstitutionalStatus`.

Se actualizó **solo la política offline** para comparar valores exactos y sensibles a mayúsculas contra esos ocho códigos: `active`, `inactive`, `past_active`, `voluntary_withdrawal`, `forced_withdrawal`, `reinstated`, `deceased`, `workshop_transfer`. Cuando el valor falta, informa exclusivamente `REQUIRED`; ante estado no reconocido, `INVALID_STATUS` sin eco de datos personales.

Regresión xUnit: pasa los ocho valores canónicos con `CanApply=false` y cero escrituras, rechaza tres alias inválidos/incorrectos, rechaza vacío sin error duplicado, comprueba la salida redactada. **No modifica API, BD, migraciones, rutas, Keycloak, usuarios, no activa preview HTTP ni importa datos reales**. No habilita carga real sin aprobación jurídico/seguridad del proceso #694 y pruebas QA. El catálogo se reutiliza desde MembershipCodes, sin valores inventados.

## Próximos pasos
- 3/3 CI/Showcase/QA instalable exactos para merge solo a dev.
- Mantener la implementación transaccional, idempotencia, autorización Máster y cierre irreversible de ventana #694 en fases posteriores, siempre con evidencias confiables y datos sintéticos hasta visto bueno jurídico.
- Continuidad espejo issue #694 + Estado Maestro Drive.
