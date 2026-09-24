# 2026-09-24 — Validación de identificador de correlación en auditoría

## Cambio
- Se valida `X-Correlation-ID` antes de persistirlo en la bitácora.
- Se aceptan únicamente caracteres ASCII alfanuméricos y `-`, `_`, `.`, `:`, con máximo de 128 caracteres.
- Si falta o no cumple el formato, se utiliza el identificador generado por ASP.NET para la solicitud.
- Se agregan pruebas para valor aceptado, valor vacío, CR/LF y exceso de longitud.

## Motivo
Evitar inyección de líneas/eventos y entradas desproporcionadas mediante una cabecera controlada por el cliente, preservando la correlación de solicitudes.

## Verificación
Pendiente de CI exact-head del PR. No representa despliegue en `srv01`, QA física ni UAT.
