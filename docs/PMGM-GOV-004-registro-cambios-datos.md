# PMGM-GOV-004 — Registro de salida de cambios de datos

**Estado:** instrucción del Sponsor/Product Owner Pablo, registrada en Issue #259.  
**Ámbito:** cualquier colaborador o IA del Proyecto Centenario. Complementa GOV-001/002/003 y la Definition of Done; conserva los gates y autorizaciones vigentes.

## 1. Disparador obligatorio

Cada incremento que altere el modelo lógico o físico, los contratos de datos (API/DTO/importación/exportación), catálogos, reglas de validación o cálculo, clasificación, retención o acceso a los datos debe entregar un **registro versionado de diccionario y estructuras de datos**, junto al código, demo y paquete QA que correspondan. Incluye cambios sin migración de base de datos.

Una edición operacional de ficha, pago, asistencia o movimiento no exige regenerar el diccionario: conserva su auditoría operacional vigente. Este registro controla el diseño y sus cambios; no sustituye esa auditoría.

## 2. Salidas para revisión y control

1. **Diccionario actualizado del alcance afectado:** entidades/tablas y campos, nombre funcional/técnico, significado, tipos lógico/físico, tamaño/precisión/escala, nulabilidad, default, valores/catálogos, validaciones y clasificación de privacidad. Marcar explícitamente lo derivado o calculado y su regla.
2. **Estructuras y relaciones:** esquemas, PK/FK, cardinalidades, unicidad, índices, restricciones, comportamiento de borrado y relaciones con otros dominios. Diagrama ER del alcance si cambia una relación, acompañado de detalle tabular verificable.
3. **Registro del cambio:** estado anterior/nuevo, motivo/fuente, módulo, migraciones y transformación de datos existentes, compatibilidad API/demo, impacto en permisos/reportes/exportaciones, pruebas/evidencias y riesgos. Sin migración también se registra “sin migración” y su motivo.
4. **Identificación del corte:** Issue, PR, rama y SHA base; SHA exacto entregado en el recibo de cierre/metadata de exportación, estado de integración y evidencias. No atribuir un diccionario a una base instalada sin verificarla.

Rutas e instrucciones: [PMGM-DB-004](modelo-datos/PMGM-DB-004-diccionario-y-estructuras.md) y [plantilla](modelo-datos/plantillas/registro-cambio-datos.md). No sobreescribir los registros históricos. El nuevo corte enlaza al anterior y enumera los dominios afectados y excluidos para no simular cobertura completa.

## 3. Definition of Done y revisión

- Declarar impacto en datos en todo PR. Si no existe, escribir “sin cambios de modelo/contratos/reglas de datos” con justificación; no fabricar un registro de schema.
- Si existe impacto, incluir diccionario, estructuras y registro antes/después **en el mismo PR del cambio** y enlazarlos en el handoff, Issue/PR y Línea Base Maestra.
- Comparar fuentes reales: entidades, configuración EF, migraciones, SQL versionado, contratos y pruebas. Distinguir modelo conceptual aprobado/propuesto, modelo implementado y schema desplegado.
- Revisar consistencia con el SHA exacto y completar las lecturas de retorno GitHub/Drive. Los cambios de dev obligan a actualizar alcance/evidencia y esperar gates frescos.
- El checklist de PR permite revisión explícita. Esta intervención documental no instala un generador ni un nuevo gate automático; no afirmar que CI detecta por sí solo toda omisión.

## 4. Seguridad y límites

Publicar exclusivamente metadatos de estructuras y ejemplos sintéticos. No incluir dumps, filas reales, RUT, contactos, comprobantes reales, secretos ni credenciales. Clasificación y minimización se verifican contra los catálogos de seguridad vigentes; no inferir nuevas políticas contables, tarifas, permisos o retención.

GitHub conserva la versión técnica y el historial; Drive conserva el resultado de revisión, enlaces y pendientes. Main congelado; srv01 pausado, despliegue QA pendiente. Un registro técnico no acredita instalación, QA física ni UAT.
