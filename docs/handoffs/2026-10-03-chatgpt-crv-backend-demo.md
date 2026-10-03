# Handoff — CRV backend y demo — ChatGPT

Issue #275 / PR draft #276. Inicio rama `8dca4e5bfc0827163d2f742b4ce3ca4e34a483df`; dev `b070dbc71d25f811f0b70999dab8f378a5343cc8`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Base actualizada mediante merge automático local; preserva íntegra entrega Claude #289/UI v0.78, sin resolver conflictos ni reescribir UI.

Gates previos 8dca4e5: CI #1840, Showcase #1201 y QA #839 SUCCESS. No certifican este nuevo corte.

Alcance registrado antes de programar en #276: política CRV backend extraída sin cambio de regla, reloj civil de Chile capturado una vez y protección de extremo DateOnly; 11 pruebas de política y seis HTTP PostgreSQL; respuesta frontend declara fecha y demo rechaza fecha/modalidad incoherente; ocho pruebas frontend. Sin nueva migración, tarifas, permisos ni autoridades. No SDK .NET local: ejecución backend pendiente de CI del SHA final. Resultados frontend y gates vivos en recibo.

Salida GOV-004 [registro CRV contrato/pruebas](../modelo-datos/cambios/2026-10-03-issue-275-crv-contrato-pruebas.md), con cobertura parcial explícita y enlace a corte anterior. Archivos propios: WithdrawalLetterPolicy.cs, AdmissionEndpoints.cs, WithdrawalLetterPolicyTests.cs, AdmissionWithdrawalLetterHttpTests.cs, pmgmApi.ts, admissionCase.test.ts y dos documentos nuevos. Archivos de #289 incorporados exclusivamente desde dev, sin programación propia.

Drive: no novedad normativa; nueva adenda Claude #289 confirma incorporación en b070dbc, UI v0.78 y coordinación pendiente de carga de insinuados/balotaje. Se registra seguimiento de este corte en Línea Base con lectura de retorno. GitHub/Drive prevalecen sobre chats.

Pendientes: esperar/verificar CI PostgreSQL y gates propios; búsqueda de retirados/otros Talleres y Personas externas; fecha contra evidencia; modalidad después del alta; comisión/autoridades, traslado/materialización e idempotencia; demo del circuito completo. #116 conserva restricción y no se fusiona su rama histórica. #276 sigue draft y no se declara cerrado.

Código/demo/paquete no se declaran publicado/instalado por tests locales. SHA-256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID del corte final pendientes si no constan en recibo. Main congelado, srv01 pausado, despliegue QA pendiente; no QA física/UAT. START-HERE no cambia antes del merge.
