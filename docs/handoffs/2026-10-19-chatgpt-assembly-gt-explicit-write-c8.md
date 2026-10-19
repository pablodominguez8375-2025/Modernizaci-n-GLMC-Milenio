# #533 — C8: autorización de escritura explícita de Gran Tesorería para expediente de padrón

Fecha 19-10-2026. Base `dev@ed5c71538a4186aa1162a37ada68e78d017845cc`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegida. QA físico en otro hilo.

## Objetivo
Regla **más estricta** que `DynamicGrandTreasuryAccess.Allows` (su compatibilidad heredada permite usuarios GT no enrolados). Para futuras propuestas y revisiones firmadas del padrón, exigir en forma concurrente: `scope=order` y rol **explícito** `GranTesoreria` en ClaimsPrincipal autenticado + `sub` inequívoco; asignación dinámica **explícita y vigente** con `treasury/view+write` en scope order; preflight documental C2 para propuestas; para revisar, acto C3 vinculado con revisor distinto y firma C6/C7 verificada mediante clave servidor. Nunca usar el fallback heredado ni permitir administradores sin rol GT.

El resultado exclusivamente indica si los hechos están listos para ser procesados por un endpoint seguro/auditado todavía NO implementado. No abre HTTP ni escribe DB. `MayAuthorizeSuffrage=false` siempre. La validación criptográfica no sustituye certificación histórica real GT ni acreditación institucional del derecho a sufragio.

## Pruebas y continuidad
Casos positivos y negativos: actor no autenticado, admin plataforma y roles ajenos, sin rol/scope/sub, catálogo sin asignación, permiso vista solamente, asignación caducada/inactiva, perfil retirado, taller versus Gran Logia, propuesta documental inválida, autorrevisión y revisor que no coincide con la firma. El gate final será triple PMGM CI/Showcase/Instalable PASS exact SHA. No alterar QA, main, producción, secretos o los archivos UI Clau.

Trabajo posterior: persistencia de atestación firmada append-only auditada, rol y RBAC en ruta HTTP, facturación histórica GT con saldo de apertura/corte/moneda, revisiones/resoluciones RI, cierre congelado, PDF/CSV.

## Código entregado
- `AssemblyGrandTreasuryWriteAdmissionPolicy.AssessProposal`: exige simultáneamente identidad GT explícita y permiso dinámico `treasury/view+write` vigente de alcance Gran Logia (`IsGranted`), más el preflight de documentación C2. Sin perfil, aun cuando `DynamicGrandTreasuryAccess.Allows` heredado responda verdadero, la solicitud queda bloqueada.
- `AssessSignedReview`: aplica los mismos controles al revisor autenticado, comprueba `sub` contra el autor de la revisión y el sujeto del sobre firmado; verifica documento/eventos/clave pública aprobada con registro servidor C7 y firma C6.
- Ambas rutas devuelven únicamente `ReadyForAuditedProcessing`, jamás una decisión electoral; `MayAuthorizeSuffrage=false`. No son escritura persistida, endpoint ni certificado de saldo.
- `AssemblyGrandTreasuryWriteAdmissionPolicyTests`: positiva documental/firmada con RSA efímera; sin perfil dinámico, solo lectura, asignación vencida, desactivada, perfil desactivado, alcance Taller, identidad sin scope o sin autenticar, administradores sin rol GT, documento incompleto, autor actor distinto de firma y firma inválida.
- PR [#575](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/575) es DRAFT. CI del commit exacto se requiere antes de integración. QA físico fuera de este hilo. No declarar Issue #533 concluido.
