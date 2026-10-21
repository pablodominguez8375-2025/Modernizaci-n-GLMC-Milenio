# #613 — UI de recomendantes de toda la Orden (selección restringida)

Fecha de corte: 2026-10-21. Rama `feature/613-recommender-ui-private-directory-20261021`, nacida desde `dev@4852b0c8757653d9fc77b7d8e709c156b6a4b11d`. Depende de la API de la PR #631.

## Diagnóstico vigente
La integración concurrente de frontend en `dev@4852b0c` ya captura `originKind`, `recommenderMemberId`, `originOtherDescription` y correcciones con motivo, pero `CandidateWorkshopIntakePage` usaba `membershipApi.getMembers(directoryOrgId,{status:'active',limit:250})`, que lista únicamente miembros del Taller actual y expone más atributos personales de los que requiere la selección. **No cumple** la decisión del Sponsor: una persona activa de otra Logia (aunque no sea Maestro) puede ser recomendante de ORIGEN, pero el patrocinante FORMAL es Maestro activo del Taller receptor.

## Incremento de interfaz
- Añadido `CandidateIntakeApiClient.getEligibleRecommenders(organizationId, query)`, con búsqueda de 3 a 80 caracteres, contra `GET /api/insinuados/taller/organizaciones/{organizationId}/recomendantes?q=...` de PR #631, autorización same-origin ya existente, respuesta mínima `{id,name}`.
- Selector de búsqueda por nombre, con debounce y consultas únicamente cuando se ingresan >=3 caracteres. Se usa tanto para apertura de expediente como para corregir origen con motivo, sin consultar un Cuadro completo ni leer el directorio de membresía local. No toca el selector distinto del Maestro/a patrocinante.
- La UI conserva `membershipApi?` en su firma de props por compatibilidad de consumidores, pero ya no lo utiliza para listar recomendantes.
- Demo con datos exclusivamente ficticios y lista mock existente; no presenta la consulta institucional real en GitHub Pages.
- Prueba de contrato de API del cliente: longitud mínima sin llamada HTTP, URI `?q=`, autorización bearer en Headers y respuesta con miembro externo.

## Criterios
- Primero integrar PR #631 (backend) con 3/3 gates del SHA reconciliado.
- Revalidar esta rama UI contra `dev` si avanza. PR de frontend con 3/3 SUCCESS exact-head antes de merge solo a `dev`. Comprobar Pages mediante `qa-current.json.sourceSha` del merge y no considerar QA glmdch actualizado sin prueba física.
- No registrar tokens personales, RUT/correo, nombres reales en trazas/QA, repositorios o Drive.
- PDF, tratamiento legado, y UAT institucional permanecen en #613. No tocar `main`.
