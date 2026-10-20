# #533 / C17 — Alta y lista de Asambleas preliminares

Fecha 20-10-2026. PR apilada sobre la C16 (#591), todavía pendiente de integración.

## Contrato de API para Clau, condicionado a gates y a dev

- `GET /api/regimen-interior/asambleas?pageSize=50` (1–100). Respuesta `{items:[{id,name,meetingDate,cutoffDate,status,provisional,officiallyClosed:false,mayVoteConfirmed:false}],pageSize}`. Requiere sesión válida, scope Orden, rol explícito Régimen Interior **o** Gran Secretaría, sujeto `sub` autenticado. Auditoría de lectura y sin datos de persona, RUT o expedientes.
- `POST /api/regimen-interior/asambleas`, cuerpo `{name,meetingDate,cutoffDate}` con fechas YYYY-MM-DD. Nombre obligatorio máximo 240; corte no posterior a reunión. Requiere mismos roles, scope y sujeto. Respuesta 201 y URL de revisión de la nueva Asamblea; objeto recién creado `status:"draft"`, `provisional:true`, `officiallyClosed:false`, `mayVoteConfirmed:false`. Auditoría SQL y creación en misma transacción PostgreSQL. No agrega asambleístas, certificaciones, padrón, papeletas ni sufragio.
- 400 entrada inválida; 403 rol no autorizado. Tests HTTP PostgreSQL usan solo sujetos sintéticos y comprueban persistencia y auditoría.

**Barrera institucional:** una Asamblea draft vacía no equivale a padrón: deben evaluarse Venerables, membresía, balances Gran Tesorería, Régimen Interior, restricciones Tribunal Honor con referencias probatorias, y aprobarse cierre de forma transaccional y exportación solo después de validar requisitos. Los triggers de cierre siguen cerrados. `main`, QA físico, srv01 y producción intactos.

Archivos de PR C17 sobre C16: `AssemblyDraftEndpoints.cs`, `RegimenInteriorEndpoints.cs`, `AssemblyDraftHttpTests.cs`, shard e informe. Para integrar, fusionar C16 primero, rebasar C17 al dev vivo conservando prueba C16, y verificar tres gates exact HEAD. No abrir UI de padrón definitivo con esto.
