# #609 — RUT íntegro únicamente en «Mi ficha» autenticada

Fecha: 2026-10-21. Rama `feature/609-own-rut-private-self-profile-20261021` creada desde `dev@4852b0c8757653d9fc77b7d8e709c156b6a4b11d`.

## Decisión institucional
El PO aprobó que **solo el titular vea su RUT completo en «Mi ficha»**. Otros módulos deben mostrarlo enmascarado por defecto; la acción de «Ver completo» de un tercero necesita atribución institucional, motivo y auditoría. Este incremento **solo implementa la parte de autoservicio** y no interpreta ni extiende permisos de terceros.

## Contrato y cambios
- El endpoint protegido `GET /api/member-self/profile` resuelve al hermano desde `IInstitutionalMemberContextResolver.ResolveAsync(httpContext.User)`, luego filtra por `context.MemberId` al leer `Person.Rut`. Con usuario no vinculado, conserva 404 sin datos; el resto de permisos no cambia. Respuesta en `member.rut` (`string|null`). `Cache-Control: private, no-store` existente.
- El frontend tipa `MemberSelfProfile.member.rut` y lo visualiza en la tarjeta «Mi ficha» en la fila ya existente (antes solo demo). Si es nulo, muestra «Sin registrar». Demo ficticia.
- Prueba PostgreSQL `MemberSelfServicePostgreSqlTests`: GET del titular devuelve RUT sintético únicamente bajo `member.rut`, no duplica dato en `contact`, la actualización de contacto PUT no altera el RUT en la base.
- No se crea columna ni se modifica esquema. `Person.Rut` ya existe; se añade una proyección privada y un campo del contrato. No se envían RUT en log, auditoría, URL, GitHub Actions ni un repositorio real.

## Pendientes #609 (NO incluidos)
1. Revisar todas las proyecciones de fichas de terceros, Tesorería, Secretaría, Regímenes, reportes y exportaciones para aplicar enmascaramiento por defecto.
2. Endpoint de revelar temporalmente el RUT a tercero: autorización estricta, razón y auditoría inmutable; sin exponer en listados.
3. Foto: carga, quitar, validación del archivo por contenido, recorte y eliminación EXIF, acceso autenticado transversal y validación jurídica de visibilidad.
4. Capturas y revisión visual PC/tablet/móvil 360/768/1440, QA/UAT autenticado y verificación de versión en glmdch.
5. CI/Showcase/QA Instalador 3/3 SUCCESS exact HEAD y compare behind=0 antes de integrar **solo dev**. No `main`, producción ni QA físico.

Responsable de continuación: integrar este incremento si CI aprueba y no hay bloqueo de herramienta; después pactar con UI (Claude) las restantes partes de privacidad sin duplicarlas.

## Normalización y verificación del RUT chileno (21-10-2026)

**Fuentes públicas oficiales:** SII formulario 3500 https://www.sii.cl/declaraciones_juradas/ingreso_upload_f3500.html (RUT y DV módulo 11) y PAES matemática 2026 de acceso Mineduc/DEMRE https://acceso.mineduc.cl/wp-content/uploads/2025/06/PAES-INVIERNO-M2.pdf (página 5, regla de cálculo del DV).

- Formato de presentación: `XX.XXX.XXX-K` (o dígito numérico). Aceptar entrada compacta, opcional guion, puntos bien situados, K minúscula; normalizar a `XXXXXXXX-K` y mostrar `XX.XXX.XXX-K`.
- Algoritmo: pesos cíclicos 2,3,4,5,6,7 **de derecha a izquierda** sobre el cuerpo; `DV=11-(suma%11)`; 10=K, 11=0. Rechazar cuerpo cero, caracteres no permitidos y DV incorrecto. Cuerpo de 1–8 cifras para tolerar identificadores históricos menores; la mayoría tienen 7–8.
- Implementado helpers puros `ChileanRut.TryNormalize / IsValid` (.NET) y `parseChileanRut / isValidChileanRut / formatChileanRut` (TypeScript), con pruebas para DV K, DV 0, número, siete/ocho dígitos y entradas malformadas; **sin números de identidad reales como fixtures**.
- `MemberPortalPage` ahora **formatea matemáticamente** el RUT propio de la respuesta privada. Cuando un legado tiene RUT no nulo pero DV inválido muestra `RUT registrado requiere revisión` en lugar de presentar un formato erróneo como válido; jamás modifica el dato original de forma automática.
- Pendiente integrar la validación .NET en endpoints de alta/edición **específicos y autorizados**, cuidando registros históricos y documentos extranjeros; el helper no reemplaza la validación de identidad real ni justifica bloquear servicios existentes.
