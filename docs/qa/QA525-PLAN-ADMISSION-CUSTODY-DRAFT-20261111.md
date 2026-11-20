# QA525 — Hoja de custodia PLAN / ADMISSION para LAUNCHER_ONLY
**Estado: BORRADOR NO FIRMADO / NO_GO.** 11-11-2026.
Referencias: #525, PR #1000, `dev@5e09d7b5718309e31f639a1eb45890546e7b29cf`.
No ejecutar el contenido como script. No publicar claves, PIN, tokens o passphrases.

## Contrato real comprobado
En la cadena QA525 launcher-only, `plan_sha256` y `admission_sha256` son **pines de integridad opacos**. El launcher coteja esos valores entre la configuración anclada y el GO; el flujo NO valida semánticamente los documentos subyacentes. Un SHA coherente NO demuestra autorización ni respaldo válido. Revisión independiente y custodia deben ocurrir antes de fijar esos SHA.

## PLAN — datos exactos para revisión humana
| Campo | Valor propuesto / evidencia |
| --- | --- |
| schema interno del documento | `qa525-plan-launcher-only-v1` (convención documental, no esquema del verificador) |
| estado | `PENDING_REVIEW_NO_GO` |
| host / entorno firmado | `glmdch` / `QA_GLMDCH_ONLY` |
| operador | `soporte@glmdch` |
| source | `5e09d7b5718309e31f639a1eb45890546e7b29cf` |
| scope | `QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION` |
| app archive digest | `sha256:fed841dfcce436cdfed0a21e8e7af8de220974b7296f906c90107dbedeacac5c` |
| launcher instalado observado | `61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3` |
| launcher candidato observado | `69fddd49bde49d0335cbfb22dfbfa4e729328c018bfdbb75c2fa709df38ce99d` |
| OPS recibido | `sha256:37a22f19ee2a56ad778ba609adb0059b728c0e0615d42d837d9ef356a63df4ed` |
| release asset digest | `sha256:2e66bfdbb59b1512247c02ac13ab1443e804e067cf44a2b98bb0b7c5c3472c6d` |
| política exacta SHA256 | **PENDIENTE relectura del documento fuente del mismo corte, no inferir del prefijo** |
| rutas físicas e inventario fresco | **PENDIENTE comprobar en glmdch** |
| reversibilidad, guarda de nonce, recibos de postestado | Exigir STOP/recovery explícito |
| despliegue API/WEB/BD/identidad/main/producción | **PROHIBIDO** |

## ADMISSION — condiciones que impiden aprobar
Estado inicial `PENDING_EVIDENCE_NO_GO`. Cada fila requiere nombre de artefacto, SHA256 íntegro, origen, fecha UTC, verificador y resultado. Nada de un simple «PASS» sin evidencias.

| Puerta | Prueba física requerida | Estado |
| --- | --- | --- |
| Release & OPS autenticadas | recibos, clave independiente, comprobación APP y OPS | Evidencia histórica PASS; **revalidación antes del GO** |
| PRE-root v3 | config privada, manifiesto de bytes exactos, pin Sponsor **externo**, permisos, symlink/TOCTOU, Python/gh/ssh-keygen hashes | **PENDIENTE** |
| G6 dos bases + globals | backup Restic cifrado actual con identificador | **PENDIENTE** |
| Silo off-host | copia fuera del host, hash leído en destino | **PENDIENTE** |
| Restore aislado | reporte reproducible que pruebe restauración | **PENDIENTE** |
| MinIO | estado del watchdog y política de interrupción | **PENDIENTE** |
| Python 3.14 glmdch | prueba rootless con módulo exacto y SHA de script verificado | **PENDIENTE** |
| GO Sponsor | acta de alcance launcher-only, nonce único y TTL <=900 s | **NO EMITIR AÚN** |

## Procedimiento para pasar de borrador a bytes fijados
1. Dos revisores independientes contrastan PLAN y ADMISSION contra los verificadores reales y evidencias host, sin confidenciales. Rechazar discrepancias. El documento que firma el sponsor NO amplía el scope.
2. Cuando **todas** las condiciones estén respaldadas, generar los dos JSON finales con formato declarado y orden determinista, sin secretos; calcular sus SHA256 **sobre bytes finales realmente conservados**, no sobre tablas, texto de GitHub ni borradores. Si el contenido cambia, los SHA cambian y se invalida la admisión.
3. Custodiar copias idénticas de los bytes finales; comprobar SHA y permisos al ingresar a glmdch, y pinarlos en config privada PRE-root y GO. El software actual coteja los digests pero no abre ni juzga los JSON; la revisión humana sigue siendo indispensable.
4. Validar G6 aparte y por evidencia vigente. Sólo después de PRE-root + G6 + revisión final puede prepararse GO real de Sponsor durante una ventana supervisada de <=900 s.
5. Cualquier fallo, drift del corte, estado incierto o ausencia de evidencia: `STOP / GO_FISICO=NO_GO`. No reintentar el launcher sin conciliación.

## División de trabajo y continuidad
- ChatGPT: contratos, análisis cruzado de código, pruebas no privilegiadas, auditoría y registro.
- Clau: revisión independiente y negativos GO-H v2, Python 3.14 rootless y custodia; compartir hallazgos en #525/PR #1000 con SHA.
- Operador: medir únicamente host cuando se solicite un preflight read-only seguro, sin exponer secretos.
- Sponsor: GO de un solo uso exclusivamente después de pruebas; no se solicita en esta etapa.

**Este archivo documenta requisitos, no es un PLAN/ADMISSION definitivo, no trae un SHA256 final ni habilita root.**
