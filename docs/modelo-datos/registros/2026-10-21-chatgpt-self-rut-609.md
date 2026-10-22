# GOV-004 — Diccionario de salida #609: identidad privada «Mi ficha»

| Estructura | Antes | Después | Protección |
|---|---|---|---|
| `core.people.Rut` / `Person.Rut` | Campo existente, nullable | Sin cambio físico ni migración | Dato personal de identificación |
| `GET /api/member-self/profile` | `member={id,institutionalNumber,firstNames,lastNames}` | Añade `member.rut:string|null` | Solo sujeto ligado por identidad institucional; no-store y HTTPS |
| `MemberSelfProfile` cliente | Sin dato `rut` en `member` | `member.rut:string|null` | Solo autoservicio, no reutilizar en vista de terceros |
| `MemberPortalPage` | Dato ficticio solo en mock | Presenta RUT del titular autenticado | No se agrega a URL, cache de terceros ni exportación |

No se modifica esquema PostgreSQL, índices, claves, historiales, ni registros de auditoría. La lectura ya existente del propio perfil se amplía con un dato personal protegido. No se agrega una capacidad de revelar identidad de terceros. Pruebas de persistencia y GET sobre datos sintéticos en `MemberSelfServicePostgreSqlTests`. Revisión jurídica y UAT pendientes, `main` y glmdch intactos.
