# Registro GOV-004 — permisos efectivos en decretos

Issue #266 / PR #320, rama `feature/tariff-access-20261004-gpt`, base dev `be69bc341f99daa9e9542b776c073adab31ae651`. Diccionario y estructuras: [DB-008](../PMGM-DB-008-acceso-efectivo-tarifario.md).

| Elemento | Antes | Después |
|---|---|---|
| GET catálogo de decretos | Autoridad institucional solamente | Autoridad institucional ∩ treasury/view en Orden administrada |
| POST decreto nuevo | Autoridad institucional solamente | Autoridad institucional ∩ treasury/create; el grant exige view |
| Acceso propio | Sin proyección específica del tarifario | DTO mínimo version/managed/actions, privado/no-store |
| Demo/pantalla | Perfiles institucionales, asistente siempre habilitado | Misma intersección; consulta sin registrar; pérdida de acceso retira datos/formulario |

Motivo: continuidad aprobada de #266, aplicada al nuevo tarifario v2 entregado por #318/#319. Fuente oficial y revisión Drive registradas en [handoff](../../handoffs/2026-10-04-chatgpt-tariff-access.md). Sin migración: sólo acceso y contrato derivado sobre catálogo persistido existente. Datos tarifarios, payload, inmutabilidad, concurrent writers, auditoría del registro y cálculos de cuotas/pagos se preservan. Privacidad minimizada, sólo fixtures sintéticos.

Pruebas: frontend consulta/registro/revocación/vigencia/ámbito/no escalamiento/transporte; backend política y HTTP PostgreSQL con consulta sin alta, habilitación explícita, rechazo por cargo local incluso con grant completo, revocación y persistencia/auditoría sin inserciones rechazadas. Gates y SHA entregado constarán en recibo de cierre y handoff postmerge; no atribuir el corte a srv01 instalado. Restantes dominios #266 pendientes.
