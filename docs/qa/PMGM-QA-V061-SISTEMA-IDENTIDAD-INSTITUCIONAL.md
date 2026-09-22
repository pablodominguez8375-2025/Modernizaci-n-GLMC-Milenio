# QA-033 — Identidad institucional oficial en Sistema

**Versión demo:** v0.61  
**Base de desarrollo:** `dev@3ae5ecb514b5d9a525d0a271512c94dbe0f65f1b`  
**Estado:** implementado en rama; pendiente CI/PR, publicación Pages e instalable QA.

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
6. Ejecutar CI, Showcase y QA Installable sobre el mismo SHA antes de declarar la salida disponible.
7. Verificar Pages y `qa-current.json` contra el SHA integrado; el despliegue físico en `srv01` continúa sujeto a Issue #97.

## Restricciones de identidad

El logotipo no se deforma, recolorea, recorta ni reconstruye. Usar la versión oficial con fondo blanco según la guía. No extraer una captura del manual como nuevo activo gráfico.
