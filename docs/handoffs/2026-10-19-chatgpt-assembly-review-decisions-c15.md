# #533 C15 — decisiones de Régimen Interior por evento append-only
Fecha 2026-10-19. Base dev@ced407ca0cbd4afde4a7723fad6060dfa85d71e9, main@6dfb9546a4873baff15955cf86abfd7d47e3d111 protegida. QA físico en otro hilo.

Objetivo: conservar decisiones manuales de revisión de Asamblea como eventos INMUTABLES con Asamblea/MemberStatus, actor autorizado RI, acción, motivo y evidencia. Acciones prudentes de fase inicial: `request_correction`, `refer_validation`, `confirm_ineligible`, `attendance_only`. No se permite `confirm_eligible` mientras las fuentes externas GT/VM/RI/Tribunal y la certificación de saldo histórico no estén conectadas. Una decisión manual nunca debe conceder sufragio.

Migración aditiva de `core.assembly_review_decision_events` con FK compuesta al miembro de la misma Asamblea, checks de acción/fundamento/sujeto/fecha y triggers PostgreSQL que bloquean actualizar, borrar y truncar eventos y rechazan inserción posterior a cierre. Política `AssemblyReviewDecisionAdmissionPolicy` exige identidad autenticada, scope Orden, rol explícito Régimen Interior y evidencia/motivo; no aceptar autoafirmación de roles desde JSON. Pruebas unitarias y PostgreSQL. C15 comienza como persistencia/control, un endpoint podrá vincularla posteriormente con auditoría de acceso y transacción.

Sin expediente disciplinario ni RUT en estos eventos, sin aprobar voto, cierre, PDF/CSV, UI ni QA físico. PR draft, merge solo 3/3 gates. No modificar `RegimenInteriorEndpoints.cs` reclamado por PR C13 hasta que se integre.
