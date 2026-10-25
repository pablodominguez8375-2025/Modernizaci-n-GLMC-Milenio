# #694 Fase P — idempotencia técnica y separación de decisión jurídica
Corte 24-10-2026 CL · Agente ChatGPT · dev base `c840bc426ffcf0b76bed5b4c9edc8f0a0388ac5e`.

## Decisión aclaratoria del Sponsor
La valoración jurídica de la licitud, la base legal y la aprobación institucional de tratar datos reales corresponde **a personas/institución fuera del software**. La aplicación no constituye comité jurídico, motor de cumplimiento ni validador automático de leyes. Lo que exige el servidor, como control técnico, es autenticación del Máster, ámbito de autorización, integridad de un registro previo de visto bueno institucional, evidencia mínima trazable, auditoría y protección contra cambios/reintentos. El campo histórico interno `LegalApprovalVerifiedByServer` significa solamente «existe constancia externa autorizada verificada desde una fuente confiable». Su nombre aún no se cambia por compatibilidad con políticas/migraciones ya integradas. El digest en DB acredita correspondencia de referencia, **NO** conformidad jurídica.

En #694, el Sponsor había exigido «visto bueno de Jurídico» previo a usar datos reales en QA. Este requisito es un paso **humano externo**, no inspección automatizada de leyes. Para QA exclusivamente sintético, no hay que solicitar trámite jurídico. No promover a `main` ni cargar datos de miembros reales antes de cumplir ese paso humano y las autorizaciones del Sponsor. Respeto a Ley 19.628 vigente y transición Ley 21.719 (01-12-2026).

## Alcance fase P
- Nuevo `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadBatchIdempotencyPolicy.cs` (pura, read-only): toma huella HMAC de lote ya verificada por recibo fase O más snapshot de reserva persistida del mismo lote; propone o bloquea reintentos, nunca aplica ni escribe.
- Nuevo `backend/tests/PMGM.Api.Tests/InitialLoadBatchIdempotencyPolicyTests.cs`: cubrir fingerprint nula/inválida, reserva nueva, intentos en curso, lote aplicado, reversión terminal, conflicto y corrupción; sólo huellas ficticias.
- Comentario de `InitialLoadAdmissionPolicy` para distinguir registro técnico de decisión jurídica; sin modificar guarda de seguridad, requisito externo, rutas o estado.
- Este handoff.

## Implementación futura
Persistir ledger con UNIQUE fingerprint, estado `pending→applied→reversed`, referencias de eventos de auditoría, idempotencia transaccional, retención/reversión y control de concurrencia. El plan puro no reemplaza las restricciones PostgreSQL. Future apply debe verificar comprobante HMAC vigente fase O, Master real, vista preflight intacta, referencia institucional externa y UAT; no admitir valores falsificables desde HTTP como si provinieran del servidor.

No crea endpoint, carga, clave, cuenta, migración ni acceso a datos personales. PR DRAFT, gates exact-head 3/3 antes de integrar solo a dev. Log en GitHub y Drive.

## Conciliación PR #719 Biblioteca de Claude
24-10-2026 noche: `dev` avanzó a `a9eba61c0bf4ed21972188a88661294a40503b57` por PR #719. Rebase mediante árbol basado exactamente en el tree del nuevo `dev`, con 4 archivos precisos de fase P; se conservó la configuración de shards, las pruebas Biblioteca y sus archivos de frontend/backend. Gates anteriores 40152341633/40152341626/40152341677 fueron SUCCESS para HEAD viejo, pero **no se reutilizan** al rebasar. Revalidar 3/3 gates nuevo SHA.
