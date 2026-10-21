# #613 — selector de Maestro patrocinante real en Secretaría del Taller

**Corte:** 21-10-2026. Rama `feature/candidate-real-sponsor-selector-613-20261021-gpt`, creada de `dev@d235d5cb3371ca2e9403b106ad959df2e053685b`. Ninguna intervención a `main` o QA físico.

## Contrato y decisión Sponsor
- «Origen»: página web, redes sociales, recomendante de otra Logia/otro grado, otros medios. **No otorga patrocinio**.
- «Maestro/a patrocinante formal»: miembro con grado de Maestro/a y afiliación activa vigente del **Taller receptor**, conforme a Protocolo de Insinuaciones 2026.
- Backend `/api/insinuados/taller/organizaciones/{organizationId}/patrocinadores?q=...` lista exclusivamente Maestros de ese Taller, según rol Secretaría, solo `id,name`, hasta 40 por búsqueda; `PUT /api/insinuados/taller/solicitudes/{requestId}/patrocinador` acredita por member ID, es idempotente para igual patrocinador y exige proceso formal para sustituir otro ya registrado.
- La ficha privada devuelve `sponsorMemberId` si existe. `PresentersJson` no acredita patrocinio formal.

## Cambios de este incremento
1. DTO privado `CandidateWorkshopQueueItemDto`: agrega campos opcionales `organizationId` (**ID del ámbito** autorizado) y `sponsorMemberId` (**identidad del patrocinante ya acreditado**) tanto al crear como en `GET /api/insinuados/taller/solicitudes`. No crea columnas ni altera DB. Estos dos IDs se entregan **solo en la bandeja privada con autorización de ámbito**, nunca en el portal anónimo. `sponsorMemberId` permite recordar la acreditación de un borrador que todavía no tiene ficha guardada; no afirma que el grado y la membresía sigan vigentes. Se mantiene compatibilidad de argumentos posicionales de DTO y consumidores antiguos.
2. Cliente frontend `CandidateIntakeApiClient`: `getEligibleSponsors(organizationId,q)` y `assignWorkshopSponsor(requestId,sponsorMemberId)`, perfil tipado con `sponsorMemberId` y `candidateOriginKind`, mock enteramente sintético con acreditación distinguible de presentantes libres.
3. UI Secretaría `CandidateWorkshopIntakePage`: selector privado de Maestros locales por ID, búsqueda remota de 40 por término con debounce, resultado de acreditación/errores; al cambiar de expediente se limpian datos para no cruzar Talleres. Patrocinio existente solo lectura: reemplazos requieren corrección formal. La vigencia se comprueba de nuevo al publicar, no se confunde «registrado» con «vigente». No inventar patrocinador para legacy.
4. Prueba de contrato frontend `candidateIntakeApi.test.ts`: alcance, búsqueda, rechazo de miembro no elegible, idempotencia, rechazo del reemplazo, persistencia de ID separado de presentantes texto.

## Límites y siguiente desarrollo
- **No** se integró todavía el selector real de hermano **recomendante de cualquier Taller** (origen `brother`). El selector de origen de nueva solicitud aún es ilustrativo de la demo: no enviar IDs falsos a backend. Futuro endpoint privado de búsqueda transversal con autorización/PII mínima + conexión de create request a `originKind/recommenderMemberId/other`. También faltan correcciones auditadas del patrocinante, impresión/PDF y UAT físico.
- El selector local está asociado a `organizationId` de la bandeja privada autorizada; sin ese dato no permite acreditar.
- La demo GitHub Pages es ficticia, no QA físico. La PR #628 de workflow Pages sigue bloqueada por controles de seguridad y no forma parte de este incremento. `dev` no debe fusionarse a `main` sin Sponsor/UAT.
- Antes de fusionar: 3/3 workflows SUCCESS exact HEAD + comparar `behind dev=0`, merge squash exclusivamente a dev, readback, registrar en issue #613 y bitácora Drive. No afirmar que se instaló QA ni que Pages sirva la versión sin verificar manifiesto público.
