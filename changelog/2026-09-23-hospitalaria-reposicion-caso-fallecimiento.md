# 23-09-2026 — Reposición por fallecimiento / cuota de cónyuge

Alcance autorizado por el Product Owner: tarifa versionada de $1.500 por hermano activo del Cuadro del Taller ante defunción; cobranza individual en Hospitalaria; transferencia total y conciliación por Gran Hospitalaria; regularidad consumida por Ceremonias; cuota de cónyuge con referencia de $15.000 mensuales, editable por vigencia.

Se amplía la matriz de regresión y su gate desde QA-025 a QA-034; el nuevo escenario queda identificado como QA-034 para preservar QA-033 del logotipo oficial.

Rama: `feature/hospitalaria-death-replenishment-spouse-fee`, base `dev@f90e6af186a1fe81f39ac4a77b6f26fd8721a017`. Las transferencias observadas permiten reenvío numerado con historial preservado.

Estado al iniciar el incremento: cambios en rama sin commit/PR ni gates de GitHub. Pruebas frontend, build y lint deben quedar registrados al cerrar el corte; el entorno actual no tiene .NET SDK y no puede compilar backend localmente. Demo Pages, instalable QA y srv01 no se consideran actualizados para este SHA hasta completar PR y sus workflows exact-head. `main` no se modifica.
