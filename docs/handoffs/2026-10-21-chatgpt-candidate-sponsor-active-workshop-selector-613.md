# Issue #613 — catálogo privado para selección de patrocinador formal del Taller

Fecha: 2026-10-21. Rama: `feature/candidate-sponsor-lookup-613-20261021-gpt`. Base de arranque `dev@7bedf646ad9e53522139027edbf980e67a820de9`.

## Funcionalidad
`GET /api/insinuados/taller/organizaciones/{organizationId}/patrocinadores?q=...`.

- Acceso autenticado exclusivo de Secretaría del Taller con `CanManageLodgeSecretariat` para el Taller solicitado. Otros usuarios o Talleres reciben 403, no el listado.
- Devuelve **como máximo 40** coincidencias `{id, name}`; no RUT, correo, grado ni otras fichas. Filtro de texto opcional `q`, hasta 80 caracteres.
- Exclusivamente miembros con membresía activa **del Taller receptor**, vigencia de fechas y último evento institucional vigente activo o reincorporado. Quedan excluidos miembros de otros Talleres, inactivos, retirados y fallecidos.
- `Cache-Control: private, no-store` para respuesta privada. La selección en UI siempre se revalida en el POST de nuevo candidato o PUT `/patrocinador`; un listado no concede permiso ni acredita de por sí la posterior presentación.
- Origen: `brother` puede ser de otro Taller, `internet`, `social_media` (PR #625 aún pendiente de integración al escribir este documento), `other`. Nunca inferir patrocinador del recomendante, aunque coincidan y sea elegible. Texto libre heredado `PresentersJson` no confiere patrocinio.
- Pruebas HTTP/PostgreSQL integradas en `CandidateWorkshopSponsorHttpTests`: rol miembro denegado, resultados locales activos, exclusión de inactivos y remotos, búsqueda y acceso cruzado denegado.

## Handoff UI / pendientes
Conectar selector institucional «Presenta/Patrocina formalmente» al endpoint del **Taller receptor**; mantener origen y recomendante separados. Eliminar botón UI v0.128 que agrega recomendante en `PresentersJson` sin verificar capacidad local. El PDF institucional requiere ambos roles diferenciados, y la prohibición de recomendante como entrevistador sigue vigente.

Verificar 3 gates CI exact-head sobre PR nueva, sin tocar `main`, sin afirmar despliegue QA físico ni cerrar #613. Si PR #625 se fusiona antes de esta, reconciliar con HEAD de `dev` y volver a ejecutar gates.
