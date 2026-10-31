# Handoff #812 — Taller 00 reservado para soporte (2026-10-30)

- Sponsor: Taller 00 fijo, exclusivo del personal técnico (Máster y superusuarios), sin adscripción a logias institucionales ni actividad contable, ceremonial o secretarial.
- GitHub: Issue #812, #604, #525. Inicio dev `0b1b31e59ee1c65364d3a82885815e0c4e1ce865`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Rama `fix/support-workshop-00-20261030-gpt`.
- QA físico: `support_orgs=0, support_number_00=0` en PostgreSQL glmdch con transacción READ ONLY del Sponsor. No se insertó ni desplegó nada.
- Esta PR pretende crear una organización técnica `Type=support,Number=00` en una migración aditiva idempotente y bloquear fail-closed perfiles de Taller sin organización única; no asignar cargos ni membresías a Master, ni tocar Keycloak.
- **Front-end Claude (GOV-003):** `frontend/src/api/pmgmApi.ts` método `startSupportView` omite Content-Type JSON; requiere sustituir `request` por `postJson`, y agregar regresión `pmgmApi.test.ts` para perfil y usuario. Separar PR frontend. HTTP 415 pendiente hasta integración.
- Estado inicial DRAFT, pruebas/CI y revisión pendientes. NO merge o deploy sin gates exactos, firma de release y actualización supervisada de #525. PR #771/#737/#766 y scripts srv01 prohibidos para QA. Sin promoción main.
- Cierre: completar evidencia de CI, postmerge SHA, releases e informe a hilo montaje QA, sin asumir que este handoff equivalga a instalación física.

## Implementación realizada en ramas DRAFT (no dev)

- PR #813: migración `20261030223000_SeedSupportWorkshop00.cs` que introduce registro fijo de soporte con UUID persistente, validación transaccional de conflictos, índice único de tipo soporte y reserva de número 00. Su Down conserva la fila técnica para no destruir posibles referencias, retira únicamente las restricciones. Revisión DBA/CI aún requerida.
- PR #813: `SupportWorkshopPolicy.IsConfiguredSupportWorkshop`, y `SupportViewAs.cs` valida exactamente una organización support número 00 antes de crear sesión de un perfil de Taller y antes de leer cada petición; devuelve 503 y auditoría ante configuración ausente o inválida. Perfiles Gran Logia usan alcance order sin dependencia del 00.
- PR #813: pruebas de tipo+número, exclusión de directorios institucionales y test de migración PostgreSQL con UUID estable, sin pertenencias/tesorería/hospitalaria predeterminadas. No se ha probado aún ausencia en runtime mediante test de mutación BD sintética; pendiente de control negativo.
- PR #814: `frontend/src/api/pmgmApi.ts` cambia solo llamada JSON a `postJson`; `pmgmApi.test.ts` valida perfil y usuario objetivo, Accept, Authorization, método, cabecera y cuerpo. PR separado por coordinación multi-IA: revisión de Claude solicitada en Issue #604 comentario 6535236784.
- Revisión de adendas Drive: adenda 9 v0.185/v0.186 (matriz 24 perfiles) y adenda 10 v0.187 (carga español), sin contradicción con #812.
- **No reportar QA listo mientras PR #813/#814 no estén integradas en dev con CI SUCCESS, nuevo release firmado y preflight G6/rollback completo.** Traspaso al chat QA mediante mensaje explícito solo cuando se compruebe SHA integrado e instalable seguro.
