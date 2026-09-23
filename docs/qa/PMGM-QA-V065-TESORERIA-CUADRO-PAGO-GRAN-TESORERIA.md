# PMGM-QA-V065 — Cuadro de Tesorería del Taller

**Estado:** implementado en rama; pendiente de CI exact-head, publicación y QA física.  
**Fuente de formato:** `CUADRO PAGO GRAN TESORERÍA.xlsx` en Drive, archivo `1nPEZsVr5QPNS-Z_SBmjEs33wjfqsUNTN`.

## Criterios de aceptación

1. La nómina se agrupa en el orden Maestros, Compañeros y Aprendices.
2. Cada fila muestra RUT, nombre y apellidos, grado, todos los cargos vigentes abreviados, tipo de cuota y monto a pagar, y respaldo.
3. Una cuota normal no necesita respaldo. Cónyuge, estudiante y tercera edad deben mostrar la referencia de su Plancha de autorización.
4. La generación, ingreso manual y envío del Cuadro se bloquean si una cuota especial carece de referencia de Plancha.
5. El Tesorero del Taller puede consultar la nómina de su propio Taller. Gran Tesorería ve inicialmente sólo totales y, al solicitarlo para resolver una diferencia, el detalle autorizado.
6. El backend no entrega RUT ni nombres completos en la proyección minimizada de Gran Tesorería.
7. La demo usa personas y RUT ficticios claramente identificados como demostración.

## Verificación local de la rama

- Suite frontend: 188/188.
- Lint frontend: SUCCESS.
- Build de producción: SUCCESS.
- JSON del catálogo de clasificación: válido.
- Backend/PostgreSQL: no ejecutable en este entorno porque no está instalado .NET SDK; exigir validación CI exact-head.
- La QA-036 de `docs/qa/PMGM-SRV01-REGRESSION-KIT.md` queda pendiente hasta ejecutarse sobre el SHA instalado en `srv01`.
