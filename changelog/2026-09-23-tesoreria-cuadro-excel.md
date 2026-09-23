# 2026-09-23 — Alineación del Cuadro de Tesorería

- Alineada la nómina de Tesorería del Taller a `CUADRO PAGO GRAN TESORERÍA.xlsx`: orden por grado, RUT, nombre y apellidos, cargos abreviados, tipo/valor de cuota y respaldo.
- Cónyuge, estudiante y tercera edad requieren una referencia a Plancha de autorización para generar, ingresar manualmente o enviar el Cuadro.
- La API entrega RUT y nombres completos sólo en proyecciones detalladas; Gran Tesorería mantiene minimización inicial.
- La demo usa datos sintéticos; QA-036 incorpora la aceptación física en `srv01`.
- Validación local: 188/188 pruebas frontend, lint/build, JSON de clasificación y plantilla QA de 36 controles SUCCESS. Backend/PostgreSQL espera CI exact-head por falta de .NET SDK local.
