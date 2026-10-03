# Handoff — ChatGPT — validación de identidad en POST de admisiones

Issue #275 / PR draft #276. Reserva previa registrada en comentario 5970816810 antes de programar. Base propia `de6f7b7e1c6dd59aadef77b4a7e0077159bd16c5`; dev incorporado `32e02ab71d9c999cf040cfd66d71b11a290400d8` (Claude #293/UI v0.80), mediante merge automático sin editar su CSS/App. Main `6dfb9546a4873baff15955cf86abfd7d47e3d111` congelado. Corte anterior CI #1845/Showcase #1207/QA #845 SUCCESS, sólo como antecedente.

## Cambios propios

- AdmissionIdentityGuard.cs y llamada desde AdmissionEndpoints.cs: destino Taller, alcance local de reutilización de identidad en incorporación, rechazo de Persona con Member incluso si MemberId está vacío y rechazo de fecha CRV en incorporación. Controles previos de afiliación/CRV preservados.
- AdmissionIdentityPostHttpTests.cs: HTTP real PostgreSQL, rechazos sin inserción, permisos local/central/Tesorería, casos válidos y conservación de identidad. No contar ejecución de DB si no existe PMGM_TEST_POSTGRES.
- admissionLookup.ts, pmgmApi.ts, admissionIdentity.test.ts y ajuste de fixture admissionCase.test.ts: demo local coherente con controles de identidad. Once pruebas nuevas; no se simula autoridad central ni alta externa nueva.
- Registro GOV-004: `docs/modelo-datos/cambios/2026-10-03-issue-275-identidad-post.md`; antes/después, diccionario parcial, estructuras sin migración y límites de acceso documentados.

Frontend 353/353 PASS, lint/build PASS. Sin SDK .NET local: backend y gates pendientes al escribir este handoff. SHA final, runs, ZIP/SHA-256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID y recibos GitHub/Drive se completan en conversación del PR con lecturas de retorno. No usar resultados/paquetes previos como certificación del nuevo HEAD.

## Continuidad

Siguiente brecha: alta externa nueva segura de Persona+expediente, sin convertirla en iniciación, sin Member antes de resolución y sin acceso global a Personas privadas. Después: evidencia/fecha CRV revisada y modalidad posterior al alta, comisión normativa y autoridades, materialización/traslado idempotente, circuito completo y demo. Documento Claude sobre vista operativa Carga de insinuados/balotaje leído y pendiente; no marcar EJECUTADA ni tocar App/CSS/Authorization reservados.

No se crea control de duplicación ni se declara POST totalmente resuelto: una Persona ya autorizada puede abrir otro expediente conforme al comportamiento existente; revisar esa brecha con el circuito, sin inventar política. PR #276 sigue draft por residual; #116 mantiene NO FUSIONAR TODAVÍA. Sin merge funcional, START-HERE no se actualiza. No instalar srv01, no QA física/UAT ni promoción main. Despliegue QA pendiente.
