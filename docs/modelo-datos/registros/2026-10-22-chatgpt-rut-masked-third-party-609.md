# GOV-004 — registro de salida #609: proyección protegida en fichas de terceros

| Datos / contrato | Cambio | Alcance |
|---|---|---|
| `core.people.Rut` | Ninguna migración ni modificación de dato | Fuente sensible |
| `MemberProfilePrivacy.MaskRutForThirdParty` | Función pura (RUT completo válido → presentación enmascarada; inválido o ausente → null) | No registra valores originales |
| `GET /api/members/{id}/profile` | Añade `member.rutMasked`, sin añadir `member.rut` | Requiere autorización previa a ficha de miembro |
| `GET /api/members/{id}/history` | Añade `member.rutMasked`, sin añadir `member.rut` | Requiere autorización previa a historia de miembro |
| `GET /api/member-self/profile` | Sin cambios; único RUT completo visible al titular | Sesión vinculada al propio MemberId |
| Pruebas | xUnit + HTTP PostgreSQL contra datos exclusivamente sintéticos | Bloquea regresión por divulgación |

La máscara elimina los tres últimos dígitos del cuerpo y retiene DV para identificar visualmente un expediente, sin afirmar unicidad, existencia o titularidad. Invalidar la revelación de campos ajenos requiere revisar cada contrato de listados, reportes, exportaciones y todos los consumidores UI. **La máscara no sustituye autorización ni trazabilidad.** Sin cambio de esquema, claves, restricciones o datos históricos.
