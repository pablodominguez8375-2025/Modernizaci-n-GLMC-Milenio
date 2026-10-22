# GOV-004 — Diccionario de salida #609: identidad privada «Mi ficha»

| Estructura | Antes | Después | Protección |
|---|---|---|---|
| `core.people.Rut` / `Person.Rut` | Campo existente, nullable | Sin cambio físico ni migración | Dato personal de identificación |
| `GET /api/member-self/profile` | `member={id,institutionalNumber,firstNames,lastNames}` | Añade `member.rut:string|null` | Solo sujeto ligado por identidad institucional; no-store y HTTPS |
| `MemberSelfProfile` cliente | Sin dato `rut` en `member` | `member.rut:string|null` | Solo autoservicio, no reutilizar en vista de terceros |
| `MemberPortalPage` | Dato ficticio solo en mock | Presenta RUT del titular autenticado | No se agrega a URL, cache de terceros ni exportación |

No se modifica esquema PostgreSQL, índices, claves, historiales, ni registros de auditoría. La lectura ya existente del propio perfil se amplía con un dato personal protegido. No se agrega una capacidad de revelar identidad de terceros. Pruebas de persistencia y GET sobre datos sintéticos en `MemberSelfServicePostgreSqlTests`. Revisión jurídica y UAT pendientes, `main` y glmdch intactos.

## Adenda — formato y validación módulo 11

Se incorporan utilitarios de normalización sin modificación física de `Person.Rut`:
- `frontend/src/chileanRut.ts`: `parseChileanRut` produce `{compact,normalized,formatted}` o `null` para entrada incorrecta, `formatChileanRut` y `isValidChileanRut`.
- `backend/src/PMGM.Api/Modules/Core/ChileanRut.cs`: `TryNormalize(input, out normalized, out formatted)` e `IsValid`, validación idéntica en servidor de módulo 11, con tests .NET.
- `MemberPortalPage` muestra exclusivamente RUT propio *validado* en grupos `XX.XXX.XXX-K`, o señal neutra `RUT registrado requiere revisión` para dato heredado matemáticamente inválido; no reescribe base de datos.
- Canonical para futura persistencia: `XXXXXXXX-K` (sin puntos; con guion y DV mayúscula); formato amigable: `XX.XXX.XXX-K`. Validez aritmética no garantiza existencia ni titularidad. Números de ejemplo en pruebas son sintéticos.
- Fuente SII https://www.sii.cl/declaraciones_juradas/ingreso_upload_f3500.html; Mineduc/DEMRE PAES M2 2026, pág. 5 https://acceso.mineduc.cl/wp-content/uploads/2025/06/PAES-INVIERNO-M2.pdf.
