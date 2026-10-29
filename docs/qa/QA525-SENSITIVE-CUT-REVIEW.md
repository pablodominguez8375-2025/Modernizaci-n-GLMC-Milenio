# QA525 — informe de riesgo del corte `dev` contra la API histórica

**Propósito:** documentar, sin secretos, las diferencias que impiden una actualización no supervisada del QA sintético de `glmdch`. Es una ayuda para la revisión humana, **no una autorización de despliegue**. El componente `scripts/qa/operators/qa525-sensitive-change-review.py` reutiliza la política fail-closed `qa525-current-change-policy.py` y la release firmada exacta de `dev`, no una clasificación nueva menos estricta.

## Evidencia al abrir este PR (29-10-2026)

Comparación analítica GitHub de `6ff4ae0d092d4214df4ea31af484efbc969fde54` a `d0c9db04c2f1788434b010d767c443331dcba8e2`: **33 commits adelantados, 135 archivos modificados**. Incluye `backend/src/PMGM.Api/Program.cs` y archivos en `backend/src/PMGM.Api/Modules/UserAccounts/`. El resultado es `BLOCKED_SENSITIVE_CHANGE_SUPERVISED_REVIEW_REQUIRED`; las cifras indican un **compare GitHub**, no qué código funciona realmente en QA. La API `6ff4...` es una referencia histórica, no una prueba de la imagen física actual.

La adenda Drive UI v0.183 confirma que faltan comprobaciones físicas de gestión editorial de Biblioteca y de permisos reales de usuario; la adenda v0.178 distingue que los botones de ciclo de vida dependen de banderas/Keycloak y no deben habilitarse sin pruebas reales.

## Ejecución futura de consulta solamente

```bash
python3 scripts/qa/operators/qa525-sensitive-change-review.py --baseline-sha 6ff4ae0d092d4214df4ea31af484efbc969fde54
```

El comando consulta los metadatos públicos de GitHub, requiere las cinco Actions aprobadas y una release exact-SHA única. **Falla en cerrado** si no están completos o si `dev` cambia. A partir de `compare` conservador (sin truncación, ancestro, menos de 250 commits y 300 archivos) produce grupos de riesgo, hasta 25 rutas de muestra por grupo y count total, nunca contenido ni secretos. Todas las salidas contienen `may_apply:false`, `manual_review_completed:false` y `host_baseline_verified:false`.

Para revisar el primer QA con esos cambios hace falta acreditar, fuera de esta herramienta: inspección física read-only de siete contenedores y capas, identidad Master y qa.admin, OIDC/SMTP, programas y flags de migración, backups frescos fuera del host de pmgm y pmgm_keycloak, ensayo de restore en entorno aislado, rollback API+WEB real y UAT autenticada. **No transformar el bloqueo de `Program.cs`/identidad en un bypass genérico ni permitir activación de cuentas no revisada**.

## Gobernanza

PR de documentación/seguridad desde HEAD vigente de `dev`. Los PR #787 a #793 crean protecciones y simulaciones pero **no existe un aplicador QA físico seguro aprobado**. El PR #771 con aplicadores de SHA fijo está obsoleto. Los scripts históricos `scripts/backup-srv01-qa.sh` apuntan a un Compose distinto (`pmgm-srv01`) y no deben ejecutarse sobre `centenario-qa-lab2`. Mantener `main` intacta, sin credenciales en logs. QA físico y primer despliegue se coordinan en el hilo «Montaje QA operativo» solamente tras entregar operador y runbook aptos.
