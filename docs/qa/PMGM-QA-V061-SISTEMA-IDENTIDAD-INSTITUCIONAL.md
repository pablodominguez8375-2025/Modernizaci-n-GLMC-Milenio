# QA-033 — Identidad institucional oficial en Sistema

**Versión demo:** v0.61
**Base de desarrollo:** `dev@3ae5ecb514b5d9a525d0a271512c94dbe0f65f1b`
**Estado:** integrado en `dev`; Pages e instalable QA publicados desde el mismo SHA. Despliegue físico en `srv01` pendiente (Issue #97).

**PR:** #132
**SHA integrado:** `e1eff39fa380144ef69e200010ced86c787e23a4`
**Gates:** PMGM CI #1423 y post-merge #1424 SUCCESS; Showcase #658 SUCCESS y Pages #659 SUCCESS; QA Installable #296 (PR) y #297 (dev) SUCCESS; Pre-UAT #320 SUCCESS.
**Artefacto QA:** `proyecto-centenario-qa-srv01-e1eff39fa380144ef69e200010ced86c787e23a4`
**SHA-256:** `b8101f28c16af42da24d68d4d20f6a4575b3d0d921742fe5f041c3c8c3c771ae`
**Expiración del artefacto:** 22-10-2026 21:30 UTC.

## Fuente aprobada

Línea Base Maestra `LB-PC-2026-09-17`, sección “Corte de identidad visual y accesibilidad institucional”, y Guía de uso del logotipo de la GLMCh (Drive, actualizada el 22-09-2026).

| Parámetro | Valor |
|---|---|
| Azul institucional | `#06148E` |
| Azul complementario | `#004AD4` |
| Dorado | `#F3C609` |
| Dorado fuerte | `#FBAE17` |

## Controles

1. El catálogo backend expone los cuatro parámetros con valores oficiales.
2. El catálogo mock de Demo Pages coincide con esos valores.
3. La consola Sistema permite previsualizar los cuatro colores y sus usos visuales.
4. La vista previa mantiene referencia al archivo de logotipo y no modifica su contenido gráfico.
5. La prueba del contrato de configuración verifica cada código y valor de la paleta.
6. CI, Showcase, Pages, QA Installable y Pre-UAT completados para el SHA integrado.
7. Pages y artefacto instalable provienen del SHA integrado; el despliegue físico en `srv01` continúa sujeto a Issue #97.

## Restricciones de identidad

El logotipo no se deforma, recolorea, recorta ni reconstruye. Usar la versión oficial con fondo blanco según la guía. No extraer una captura del manual como nuevo activo gráfico.
