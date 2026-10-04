# Registro GOV-004 — acceso Hospitalaria local

Issue #266 / PR #322 / feature/hospitalaria-access-20261004-gpt. Base dev a07aab8e37a8998812850de61e0b80a5503f69cd; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Antes: endpoints locales basados en autoridad institucional, sin restricción del catálogo dinámico. Después: dicha autoridad intersectada con hospitalaria/view,create,write por sujeto/Taller exactos, incluida revocación en cada petición; proyección propia mínima privada y pantalla/demo compatibles.

Motivo: alcance autorizado #266. Fuentes: Línea Base revisada antes del código, GOV-001, ARCH-011 y autoridades existentes, sin nueva norma. Drive sólo presenta registro del cierre #320/#321. [Diccionario, contratos y estructuras DB-009](../PMGM-DB-009-acceso-efectivo-hospitalaria.md).

Sin migración: no cambian tablas, relaciones, cálculos, tarifas, estados financieros ni snapshots históricos. Se añade un DTO derivado y restricciones de acceso. Auditoría de operaciones autorizadas preservada. No se añade impresión/exportación ni autorización institucional.

Pruebas: política de ámbito/vigencia/revocación/inactivación; HTTP PostgreSQL real de consulta, creación, escritura denegada, ausencia de escalamiento, autoaprobación impedida, Venerable autorizado y Taller persistido frente a parámetro ajeno; demo impide mutaciones directas y lecturas tras revocación; pantalla inicial no expone formularios antes del permiso. Frontend 416 PASS; lint/build PASS. Backend/gates remotos pendientes al registrar este corte, resultados exactos en recibos de cierre.

Entrega parcial de #266: Gran Hospitalaria/regularidad, navegación global y módulos restantes pendientes. App.tsx reservado en #116, sin tocar; sin migraciones/identidad/workflows. Handoff propio: [implementación](../../handoffs/2026-10-04-chatgpt-hospitalaria-access.md). SHA exacto, CI/Showcase/QA y paquete público se completan en cierre posterior; no confundir con el paquete anterior a07aab8. Main/srv01/UAT pausados; despliegue QA pendiente.
