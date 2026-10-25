# GOV-004 — Registro de salida #604: organización soporte sintética (corte 1)

Fecha: 2026-10-23. Base `dev@99cbc8ad084c804e31f8280de95709437a7ca445`. Tema: solo aislamiento del **directorio institucional general**, no provisión del Taller 00.

| Estructura/contrato | Antes | Después | Impacto |
|---|---|---|---|
| `core.organizations` / `Organization.Type` | Tipos institucionales existentes | Se reserva valor lógico futuro `support`, **no se crea registro en este PR** | Sin cambio de columnas, claves, constraints ni migración |
| `SupportWorkshopPolicy` | No existente | `OrganizationType = "support"`, `IsSupportOrganization`, `IncludeInInstitutionalDirectory` | Regla explícita y testeable de clasificación, independiente del nombre o número |
| `GET /api/institutional/organizations/options` | Incluía todos los tipos con alcance de lectura | Filtra tipos `support` insensibles a mayúsculas en SQL antes de ordenar/proyectar | Organización sintética excluida incluso para actor de alcance Orden; Talleres reales siguen disponibles |
| Pruebas | Ninguna exclusión para soporte | XUnit de tipos y HTTP/PostgreSQL como admin de la Orden, dos formas `support/SUPPORT` y Taller real control | Acredita solo **esa API**, no toda superficie |
| Auditoría y datos personales | Sin cambios | Sin mutaciones ni retorno de PII | No se generan ni transmiten RUT, cuentas ni Hermanos |

**Pendientes que no deben interpretarse como cubiertos**: seed singleton e idempotente de Taller 00; los ejemplos inventados; bloqueo de todas las rutas de escritura por ID, ámbitos y rol; exclusión de reportes/finanzas, padrones, bibliotecas, búsqueda de Talleres en **otros endpoints**; lectura privada de soporte autorizada solo a Master/superusuarios; MFA y «Ver como». **No integrar datos de soporte hasta que existan esas garantías completas.**

PO 23-10-2026: Taller 00 con datos sintéticos base, no editable, aislado. Clau tiene UI y PR #651 de doble perfil (no modificar). `main`/QA/producción sin cambios. Comprobación 3/3 gates exact-head obligatoria.
