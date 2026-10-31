# PMGM-AUDIT-001 — Trazabilidad de cada intervención y mejora continua

**Estado:** propuesta ejecutable, registro documental de la orden del Product Owner del 31-10-2026. **Aplica a cada tarea posterior una vez incorporada a `dev`.** En la PR draft ya se aplica como procedimiento provisional a esta misma entrega. No sustituye AGENTS.md, PMGM-GOV-001/003, ADR ni reglas de permisos/QA.

**Orden del PO:** «Deja registro de todo tu proceso para que pueda ser auditado y mejorado, siempre revisa antes si hay mejoras que hacer».

## 1. Objeto y fuentes verificables

Una intervención comprende análisis, consulta, código, documentación, QA, revisión, diseño, decisión y seguimiento, aun sin cambios de archivos. Debe quedar una trazabilidad **externa, verificable, minimizada y transferible**. No basta un resumen de éxito sin fuentes.

- GitHub (`dev`, Issues, PR, commits, checks, Actions, comentarios) es el registro técnico versionado y el tablero de coordinación. Drive (solo carpeta «Proyecto Centenario», ID `1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO` y subcarpetas) guarda adendas documentales oficiales. Nunca usar memoria conversacional como fuente de verdad.
- Respetar GOV-003 §16: lectura mínima y Drive por diferencias; **un registro por entrega** (PR como handoff, handoff nuevo solo para trabajo abierto) y **una adenda Drive por sesión/lote**; sin triplicar resúmenes ni consultar CI repetidamente.
- Regla de autoridad: norma institucional oficial vigente > Línea Base y decisiones aprobadas > `dev`/ADR/gobierno > `main` histórico > chats. Si hay discrepancia, documentarla y bloquear cambios afectados hasta resolverla.
- Regla de auditoría: registrar las **decisiones, hipótesis declaradas, fuentes, acciones observables y pruebas**, no transcripciones completas de herramientas, material sensible ni razonamiento interno privado de una IA. Un resumen reproducible de los criterios y alternativas sí es obligatorio.

## 2. Preflight de mejora obligatorio ANTES de cada tarea

Antes del primer cambio o de una instrucción de ejecución, dejar una sección `PRE-REVISION` en el Issue/PR o handoff activo:

1. **Identidad y alcance:** fecha/hora ISO 8601 con zona, actor real (`ChatGPT IA`, `Claude IA`, humano acreditado), Issue/PR, objetivo solicitado, cambio autorizado y **exclusiones**.
2. **Estado exacto:** SHA completo de `dev` y `main` vivos; ramas, PR y reservas/etiquetas `agente:*`; últimos checks por HEAD exacto, estado Pages/artefacto/QA físico solo cuando exista evidencia. No inferir despliegue de un `SUCCESS` en Actions.
3. **Drive por diferencias:** consultar metadatos `modifiedTime` de la carpeta Centenario desde el último corte, abrir solo adendas relevantes, registrar fecha/fuente o `NO_VERIFICADO` con motivo. Nunca extraer ficheros fuera de la carpeta por coincidencia de nombre.
4. **Código existente / decisiones:** buscar solución y pruebas ya implementadas, ADR y GOV pertinentes; revisar control de cambios y archivos calientes; evitar reconstruir, duplicar o sustituir por mocks.
5. **Análisis de mejoras:** buscar al menos los cinco ejes aplicables: corrección/regresión; seguridad/privacidad; recuperación/rollback; rendimiento/complejidad/duplicación; auditabilidad/testabilidad. Si alguno no aplica, anotarlo brevemente. Ofrecer alternativas y la elegida **con criterios verificables**, no con suposiciones.
6. **Puerta de riesgo:** identificar acciones destructivas, datos sensibles, identidad, credenciales, migraciones, permisos, recursos institucionales, `main`, QA físico, backup G6 y dependencias de terceros; delimitar pruebas negativas y autorizaciones. En caso de bloqueo, detener ese alcance; no sustituir un gate faltante por un texto de aprobación.

**Resultado de preflight:** `MEJORA_APLICABLE` con Issue/criterio, `SIN_MEJORA_ADICIONAL_IDENTIFICADA` con alcance de la búsqueda, o `BLOQUEADO` con evidencia y próximo paso. La ausencia de hallazgos no equivale a auditoría de seguridad exhaustiva.

## 3. Registro DURANTE la intervención (material y cronológico)

Registrar en el hilo del PR/Issue **cada transición material**, no cada pulsación o cada consulta idéntica:

| Campo obligatorio | Contenido esperado |
|---|---|
| `PASO` | ID consecutivo y fecha/hora observada, con zona; orden real de las acciones |
| `ACTOR` | IA identificada o humano real; no simular auditor independiente |
| `OPERACIÓN` | Examinar, comparar, crear issue/branch, modificar archivo, ejecutar test, consultar CI, documentar, bloquear, etc. |
| `SISTEMA/OBJETO` | GitHub issue/PR/commit/ruta; Drive documento/revisión; host solo si existe comprobación real |
| `ANTES → DESPUÉS` | Estado o SHA/digest antes y después; `sin cambios` cuando corresponda |
| `EVIDENCIA` | URL/ID de tool result/commit/check/run/revisión o acta **sin secretos** |
| `RESULTADO` | `SUCCESS`, `FAILED`, `PENDING`, `BLOCKED`, `NO_VERIFICADO` |
| `FALLO/CORRECCIÓN` | Error observado, causa visible, cambio propuesto, nuevo commit y nuevo test; sin ocultar iteraciones fallidas |
| `DECISIÓN` | Criterios resumidos, alternativa descartada y motivo comprobable; riesgos y supuestos |

Si una escritura falla después de una respuesta ambigua, **consultar estado antes de repetirla**. Nunca registrar como hecho una operación no confirmada. Mantener historial append-only mediante commits/comentarios y revisiones de Drive; las correcciones se anexan y no reescriben una afirmación histórica sin dejar rastro.

## 4. Cierre POST y transferencia multi-IA

El cierre debe tener: referencias del preflight, cambios y SHAs finales, decisiones con sus límites, verificación de CI del **último HEAD** exacto, prueba local y fallos, estado de release/firma y si realmente se probó en host, estado Demo/QA/UAT/main, riesgos residuales, reversibilidad, próximo paso ejecutable y **mejora pendiente priorizada**.

| Capa de estado | Usar solo si existe evidencia |
|---|---|
| `DOCUMENTADO` | Contenido propuesto y ruta/PR |
| `PR_DRAFT/IMPLEMENTADO` | HEAD exacto del PR; no integrado |
| `INTEGRADO_DEV` | Merge SHA verificado |
| `CI_EXACT_HEAD` | Runs/checks de ese SHA; pendientes = pendientes |
| `FIRMADO/EMPAQUETADO` | Release, digest y firma corroborada, no solo CI |
| `QA_FISICO_GLM DCH` | Acta y prueba física de host, composición, identidad, backup/rollback |
| `UAT` | Evidencia funcional y aceptación explícita |
| `MAIN/PRODUCCION` | Promoción/autorización independiente |

En el handoff del PR dejar las rutas, referencias y siguiente IA; si otra sesión debe continuar una tarea abierta, **crear un archivo único y nuevo** en `docs/handoffs/`, sin reescribir `START-HERE.md` antes del merge. En Drive, añadir una **sola adenda** a la bitácora/Linea Base pertinente y comprobar que se lea con la nueva revisión. Reportar el estado como `REGISTRADO_EN_AMBOS`, `PARCIAL_GITHUB`, `PARCIAL_DRIVE` o `SIN_REGISTRO`; nunca suponer sincronización.

## 5. Lista priorizada de mejoras y seguimiento

Cada oportunidad se registra en Issue/PR con: `ID`, severidad `P0/P1/P2`, evidencia, riesgo/beneficio, alternativa, decisión, criterio de aceptación, responsable/etiqueta y estado. Priorización:

- **P0:** riesgo de pérdida de datos, brecha de privacidad, omisión de firma o privilegios, migración irreversible, identidad/Compose alterados o rollback engañoso. Bloquear el alcance afectado.
- **P1:** control de CI exacto ausente, prueba adversarial ineficaz, informe no reproducible, firma/release sin prueba, conflicto documental que afecta la tarea. Corregir antes del corte al que se aplique.
- **P2:** optimización de documentación, duplicaciones, sondeo repetitivo, legibilidad o mejora de desempeño sin riesgo inmediato. Programar sin interrumpir tareas P0/P1.

Una mejora fuera de alcance se anota y se convierte en Issue independiente: no mezclarla con una PR de corrección sin reevaluar regresión, aprobación y archivo caliente. Antes de merge y antes de ejecutar en QA físico, repetir preflight de seguridad, HEAD, gates y estado real. El Product Owner no debe reaprobar rutinariamente una decisión vigente; **operaciones físicas sensibles conservan sus permisos específicos**.

## 6. Seguridad del propio registro

No copiar al registro: contraseñas, tokens, claves, PII real de miembros, archivos root0600, datos médicos, correos privados, configuraciones SMTP sensibles, respaldos cifrados ni rutas que expongan secretos. Poner solo nombres de evidencia, tipo, hash no secreto, fuente controlada y responsable. No publicar dumps brutos de CI/host si contienen datos sensibles. Respetar Ley 21.719 y segregación de permisos de la GLMC.

Si se carece de permiso para leer o escribir una fuente, marcar `NO_VERIFICADO`/`PARCIAL_*`, no improvisar URL ni reutilizar SHA de un chat. Una auditoría documental no prueba instalación física. La comprobación del espejo GitHub/Drive debe limitarse a metadatos y a los registros autorizados.

## 7. Plantilla breve reutilizable (Issue, PR o handoff)

~~~text
CORTE: fecha_hora= ; agente= ; issue= ; PR= ; rama= ; dev_sha= ; main_sha=
PRE-REVISION: fuentes_Github= ; Drive_delta= ; cambios_otros_agentes= ;
             funciones_existentes= ; alternativas= ; oportunidades_mejora= ;
             riesgos= ; decision_y_criterios= ; autorizaciones= ; bloqueos=
PASOS:
  01 / hora / actor / acción y objeto / antes -> después / evidencia / resultado
  02 / hora / actor / pruebas/CI / SHA exacto / evidencia / resultado
FALLOS: incidente= ; evidencia= ; corrección= ; nuevo_commit= ; prueba=
MEJORAS: [ID; P0/P1/P2; evidencia; opción; criterio_aceptación; dueño; estado]
POST: PR_head= ; CI_exact_head= ; reviews= ; merge_dev= ; Pages= ; firma= ;
      host_QA= ; backup_G6= ; UAT= ; main= ; estado_registro= ;
      pendiente_proximo_agente= ; no_hacer= ; incertidumbres=
VERIFICACION_ESPEJO: comentario_Github_url= ; doc_Drive_url= ; revision= ;
                     readback=SI/NO ; fecha_verificacion=
~~~

## 8. Ejemplo de mejora detectada en QA525 (corte 31-10-2026)

- Código PR #820 (recuperación de preparado expirado) y PR #823 (preflight de launcher firmado) son **borradores**, no actualización física de glmdch. CI de distintos HEAD debe citarse por SHA; un resultado de HEAD anterior no garantiza el nuevo.
- En un corte anterior de PR #823, el workflow QA525 no ejecutaba las pruebas nuevas porque faltaban rutas en `paths`. **Mejora P1:** comprobar antes del commit la relación archivos modificados ↔ triggers ↔ jobs; ante paths excluidos, corregir workflow sin asumir que CI global cubre el código.
- Las primeras pruebas estáticas produjeron falsos positivos por texto de plantilla `os.execv` y normalización `str.replace`. **Mejora P1:** inspección AST/evaluación semántica de llamadas y pruebas locales antes de push; no declarar un fallo de tests como vulnerabilidad real sin examinar el log.
- Los snapshots de CI se consultaron varias veces sin un cambio material. **Mejora P2:** una consulta inicial y otra al cierre; si quedan runs activos, registrarlos `PENDING` y usar el monitor read-only existente para cambios posteriores (GOV-003 §16).
- Los registros previos presentaban «revisión independiente pendiente» como posible bloqueo genérico. **Mejora P1:** AGENTS.md §12 autoriza integrar a `dev` los alcances aprobados por el PO con CI exact-head y permisos GitHub, sin exigir necesariamente un tercero; mantener revisión técnica específica para operaciones QA sensibles y no fusionar/promover `main` por analogía.
- **Mejora P0 pendiente:** PR #823 aún no implementa sustitución atómica auditada del launcher; antes de cualquier comando físico requerir cadena firmada, lock, journal, respaldo, reinicio seguro y reversión probada. No habilitar deploy por un preflight read-only.

## 9. Implantación y restricciones

Este archivo es un suplemento documental propuesto desde `dev` vía PR, no una modificación unilateral de archivos calientes. Para hacerlo visible al iniciar otras IA, añadir tras su integración un enlace mínimo desde AGENTS.md/GOV-003 **cuando no haya otra PR reclamando dichos archivos**. Mantener Issue abierto hasta verificar ese enlace, CI y espejo Drive. No se requiere nueva infraestructura, base de datos ni aplicación para el registro.

**Límite QA vigente al redactar:** único host `glmdch`; ningún acceso físico nuevo desde este corte; `main` inalterada y ninguna instrucción para `srv01`, otra VM, PR #771/#737/#766 o repetir EF100.
