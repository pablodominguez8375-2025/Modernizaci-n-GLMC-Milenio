# Handoff multi-IA — RUT enmascarado para fichas de terceros (#609)

Fecha: 2026-10-22, base `dev@3506fd7a6723d95d51db56016b5d24cebd00a9d2`. Dueño de contrato y backend: ChatGPT; interfaz/UX de ficha: Clau tras reclamar tarea y coordinar en PMGM-GOV-003.

## Contrato

`GET /api/members/{id}/profile` y `GET /api/members/{id}/history` mantienen sus comprobaciones de acceso a la organización y su objeto `member` (id, personId, institutionalNumber, firstNames, lastNames, createdAtUtc). Añaden exclusivamente `member.rutMasked:string|null`. **No** incorporan ni serializan `rut` completo. El backend obtiene temporalmente el campo `Person.Rut` para transformarlo ANTES de proyectar el DTO, nunca lo añade al objeto HTTP.

`MemberProfilePrivacy.MaskRutForThirdParty` verifica matemáticamente módulo 11 con `ChileanRut.TryNormalize`, muestra como máximo el prefijo y el DV y sustituye los últimos 3 dígitos del cuerpo con `***`. Ejemplo aritmético ficticio `10.000.013-K -> 10.000.***-K`. Un registro antiguo inválido, extranjero o no ingresado recibe `null`: no mostrar ni intentar dar formato a información desconocida. El endpoint privado `GET /api/member-self/profile` sigue siendo **el único** que entrega RUT completo al titular autenticado.

## Evidencia y restricciones
- xUnit `MemberProfilePrivacyTests`: DV K, numérico, guion/K minúscula, 7/8 dígitos, valores inválidos que nunca se divulgan.
- HTTP PostgreSQL `MemberSelfServicePostgreSqlTests`: confirma `rut` completo solo al titular, ausencia de la clave `rut` y ausencia del valor íntegro en el JSON de ambas rutas de terceros.
- Sin migraciones, datos reales ni cambios de roles. El permiso de lectura institucional sigue siendo obligatorio.
- Mantener la representación pública de `MaskedRut.tsx` coordinada con Clau; no activar `Ver completo` de terceros mientras no exista autorización y auditoría **servidor** con motivo y caducidad.
- Requiere 3/3 CI (PMGM, Showcase, Instalador) **SUCCESS del SHA exacto**, comparación `behind dev=0` y revisión antes de integrar solo a `dev`. No tocar `main`, producción ni QA físico.

## Pendiente #609
Enmascaramiento de otras rutas de reportes/exportación en cada ámbito pertinente; revelación excepcional auditada, fotos de perfil, base jurídica y UAT real. Solicitar a Clau revisión visual y capturas 360/768/1440 antes de declarar el ticket cerrado.

Las respuestas de ficha e historial de tercero establecen expresamente `Cache-Control: private, no-store`; el test PostgreSQL comprueba el encabezado para impedir almacenamiento compartido de datos aun enmascarados.
