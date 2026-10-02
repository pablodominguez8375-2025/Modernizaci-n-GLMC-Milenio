# Registro de cambio de datos — Issue N — Tema

> Plantilla: sustituir instrucciones por evidencia real. No publicar filas/datos personales reales. Aplicar PMGM-GOV-004 y declarar el alcance exacto.

## Identificación y alcance

- Issue/PR, fuente aprobada y motivo:
- Autor/agente y responsable de revisión; fecha:
- SHA base de dev, rama y registro anterior:
- Dominios/entidades/contratos incluidos y excluidos:
- Estado: en rama / integrado / empaquetado / desplegado / QA/UAT. SHA exacto de entrega: completar con el recibo de cierre.

## Diccionario del alcance actualizado

| Entidad/tabla/contrato | Campo funcional y técnico | Significado/finalidad | Tipo lógico / SQL o JSON | Tamaño / precisión / escala | Nullable / default | Valores / validación / cálculo | Clasificación / acceso | Fuente versionada |
|---|---|---|---|---|---|---|---|---|

Declarar campos persistidos, calculados, sólo de contrato o de demo. No usar “N/A” sin justificar. Incluir el alcance vigente, además de los campos cambiados.

## Estructuras y relaciones

| Schema / tabla | PK | FK y destino | Cardinalidad | Unique / índices / checks | Regla de borrado | Fuente versionada |
|---|---|---|---|---|---|---|

Agregar diagrama ER del alcance cuando cambien relaciones, contrastado con mapeos/migraciones. Identificar relaciones lógicas sin FK física y entidades sólo propuestas.

## Antes y después

| Elemento | Antes | Después | Motivo / fuente | Impacto en datos existentes y consumidores |
|---|---|---|---|---|

## Migración, compatibilidad e impacto

- Migración(es) y orden; si no existen, motivo explícito:
- Transformación/backfill, nulos/duplicados, integridad, preservación histórica:
- Reversibilidad/recuperación y riesgos de pérdida de datos:
- API/DTO/importación/exportación, frontend/demo y reportes afectados:
- Permisos, auditoría, clasificación/retención y minimización:
- Instalación/QA: probado o pendiente. No ejecutar srv01 durante la pausa.

## Evidencia y control de salida

| Comprobación | SHA exacto | Resultado | Evidencia / enlace |
|---|---|---|---|
| Consistencia diccionario ↔ entidades/mapeos/migraciones/contratos | | | |
| Integridad y transformación de datos | | | |
| Contratos, cálculos y privacidad | | | |
| CI, Showcase y QA Installable aplicables | | | |
| GitHub y Línea Base: lectura de retorno | | | |

- SHA final y recibo del corte; exportaciones SOURCE_SHA y checksum si existen:
- Archivos afectados y enlace al diccionario/estructura previo:
- Observaciones/revisión pendientes; distinguir aceptación técnica e institucional:
- Main congelado; srv01/QA física/UAT según autorización vigente:
