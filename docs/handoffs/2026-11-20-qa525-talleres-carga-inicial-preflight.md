# QA525 — Preflight único: Talleres + carga inicial (#694)

Fecha: 2026-11-20. Estado: **PREPARACIÓN / NO_GO_DESPLIEGUE**. No constituye autorización de ejecución en `glmdch`.

## Fuente y alcance
- `dev` post-merge PR #1065: `eb1c4cf51176a70a71ec2174f976225232573b68`.
- Fuente funcional validada antes del merge: `230a7400ae68d2f98402aef31211074f8430be8f`; CI PMGM 44802843671, instalable 44802844206 y showcase 44802843789 = success.
- Artefacto QA pre-merge GitHub Actions ID `14661328776` (no expirado al revisar). **No se ha comprobado aún hash, manifiesto ni firma del ZIP.** No asumir equivalencia de procedencia con SHA post-merge.
- Solo servidor QA `glmdch`; `main` y producción fuera de alcance. No datos personales reales en pruebas.

## Gates antes de solicitar GO de despliegue
1. Congelar SHA de release y verificar `dev` vivo; comprobar CI de **push post-merge** por Actions, no solo API de ejecuciones de PR (esta última devolvió [] para SHA merge).
2. Obtener artefacto instalable del SHA elegido; verificar SHA256, MANIFEST, BUILD-INFO, attestations/Sigstore y workflow identity; rechazar diferencias y registrar huellas en Drive/GitHub. **No reutilizar el instalable pre-merge como si estuviera firmado para el merge.**
3. Verificar en modo lectura inventario de `glmdch`: host, servicios, SHA actual API/WEB, Compose, identidad Keycloak, migraciones y estado `pmgm-qa-update --status`. No cambiar nada durante el preflight.
4. Validar G6 actual: backup verificable, restore aislado, revisión de evidencia y plan de rollback dual con lock; **STOP** ante discrepancias. Nunca sustituir evidencia actual por un PASS histórico.
5. Preparar configuración protegida en servidor: `InitialLoad:ApplyEnabled=false` inicialmente; `InitialLoad:ReceiptSecret` Base64 de al menos 32 bytes; `InitialLoad:MasterSubject`, `InitialLoad:MasterIssuer`, `Authentication:Authority` coherentes; `InitialLoad:ExternalApprovalDigest` referencia externa de 64 hex minúsculas. No publicar valores secretos ni tokens en logs, PR o Drive. La autorización institucional es externa al software; no agregar gate jurídico por lote.
6. Revisar que modificación de Talleres existentes esté implementada, autorizada por perfil y cubierta por pruebas de edición, persistencia y auditoría; **la carga inicial no prueba por sí sola la edición posterior de Talleres**. Si falta evidencia, marcar bloqueo funcional y corregir antes de afirmar QA operativo.
7. Solicitar al Sponsor **GO explícito por SHA y alcance** antes de ejecutar el actualizador en `glmdch`. No tocar `main`.

## Validación sintética tras GO (operador, Termius/SSH glmdch)
- Confirmar health API/WEB, login JWT Máster y denegación de otros perfiles.
- Comprobar módulo de Talleres: abrir ficha, modificar campo autorizado, guardar, recargar y comprobar persistencia; probar permiso denegado y auditoría; no tocar Taller 00 de soporte.
- Carga inicial: con flag off, endpoints apply/open/result devuelven 404; habilitar únicamente bajo procedimiento aprobado en QA, con ventana inicialmente cerrada.
- Preparar 3 CSV **ficticios**: `Talleres.csv`, `Hermanos.csv`, `CargosVigentes.csv`; un Taller por lote, máximo 30 Hermanos por lote; lotes sucesivos permitidos.
- Máster: descargar plantillas, previsualizar, validar recibo y caducidad, abrir ventana con motivo de auditoría, aplicar, verificar conteos de Taller/Hermanos/cargos, repetir mismo lote y confirmar idempotencia, cerrar y verificar rechazo posterior.
- Negativos: no Máster, lote cruzado, recibo vencido/manipulado, ventana cerrada, 31 Hermanos, conflicto de identidad; no duplicados ni escrituras parciales.
- Verificar UI → API → DB → relectura, y que fallos de red/503 no afirmen falsamente que no se guardó nada.
- Ejecutar rollback controlado solo si corresponde al plan aprobado; comprobar restauración de baseline y salud. Conservar evidencia sin PII.

## Criterio GO operativo
**GO** únicamente si: artefacto firmado y verificado, G6/rollback vigentes, migraciones y servicios sanos, edición de Talleres probada, carga inicial E2E sintética e idempotencia probadas, permisos/auditoría correctos, evidencia en GitHub + Drive y aceptación del Sponsor. En caso contrario: **NO_GO**, conservar baseline.
