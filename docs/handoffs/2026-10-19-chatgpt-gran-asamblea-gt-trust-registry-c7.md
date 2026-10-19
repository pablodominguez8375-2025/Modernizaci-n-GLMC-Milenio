# #533 — C7 claves de confianza para firmas GT — 19-10-2026

Base `dev@c0eb8d55d00490a3638a09fbf3754b9700fefe1c`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegida. QA glmdch atendido en otro hilo. Archivo de continuidad de PR draft.

## Objetivo
Resolver claves **públicas** para verificar revisión documental C6 desde configuración **del servidor**, rechazando cualquier lista de claves propuesta por HTTP o frontend. Sin claves privadas ni firmas reales; pruebas con llaves efímeras de laboratorio.

## Contrato
Sección `AssemblyGrandTreasury:TrustedReviewKeys:<n>`: `KeyId`, `ReviewerSubject`, `PublicKeyPem`, `ValidFromUtc`, `ValidUntilUtc`, `RevokedAtUtc` opcional, `Approved` exacto `true`. Falta de configuración, registro incompleto, IDs duplicados, clave no RSA pública 2048+, fechas no ISO8601 o no aprobadas bloquean **toda** la verificación. Revocación y ventanas se validan también al consumir la firma. Los eventos de propuesta/revisión deben conservar el mismo Taller y Asamblea; salida `MayAuthorizeSuffrage=false`.

## Fuera de alcance
No se establece autoridad de firma institucional ni jurídica por solo existir una clave. Pendientes: registro **institucional** y ciclo de vida auditado de claves, custodia/rotación, firma/atestación persistente, autorización dinámica GT de escritor y lectura de saldos históricos reales, derechos V.M./Tribunal, decisión RI, cierre del padrón y exportaciones. Sin endpoints nuevos, datos reales, migración, intervención QA, main ni srv01. El merge exige PMGM CI, Showcase y QA Installable SUCCESS exact HEAD.

Revisado protocolo GOV-003, fuente canónica de Issue #533, adenda Drive 19-10 de Claude (Docencia / Mis Pagos /brand), sin cambios normativos de firma.
