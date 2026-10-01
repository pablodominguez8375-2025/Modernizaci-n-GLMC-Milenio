# QA — Portal transversal de insinuados: autorización HTTP

Complementa QA-032 y v0.60 sin modificar el runbook reservado por otros PR. Issue #44; PR #233. Se prueba el endpoint real mediante TestServer, PostgreSQL y el servicio de permisos de producción. La autenticación de pruebas usa claims sintéticos por petición; no reemplaza el servicio de autorización. El objeto binario usa un adaptador en memoria; esta suite no valida conectividad S3 ni OIDC real.

## Casos automatizados

1. Hermano sin privilegio administrativo del Taller B consulta publicaciones vigentes del Taller A. Comprueba nombre completo/Taller, conjunto exacto de campos permitidos, ausencia de valores sensibles, foto con ruta protegida y publicación sin foto con null.
2. Publicaciones vencidas, futuras y retiradas no aparecen; su fotografía responde 404.
3. Usuario anónimo recibe 401 para lista, fotografía publicada, ficha y fotografía privada.
4. Hermano de otro Taller obtiene los bytes de una foto publicada vigente, tipo image/png, Cache-Control private/no-store y nosniff; no se expone clave de almacenamiento ni nombre privado. Al vencer la publicación, el mismo cliente obtiene 404 y deja de verla en la lista.
5. Hermano de otro Taller, hermano del mismo Taller y secretario de otro Taller reciben 403 para ficha, foto privada y descarga documental directa. La foto pública no amplía esos permisos.
6. Secretario del Taller propietario conserva lectura de ficha y fotografía privadas (200), demostrando aislamiento sin bloquear acceso legítimo.

Las tres pruebas están en CandidatePublicationHttpWorkflowTests.cs, colección PostgreSQL serializada. El CI debe definir PMGM_TEST_POSTGRES; el retorno sin esa variable no se acepta como evidencia de integración. Revisar TRX y logs para comprobar ejecución de los tres casos y cero fallos.

## Límites y continuidad

Sólo datos ficticios y una imagen PNG sintética. Sin nuevas migraciones, reglas o cambios de UI. Las pruebas de S3/OIDC y el despliegue real conservan sus propios gates. srv01 sigue pausado: despliegue QA pendiente, sin QA física ni UAT. PR #45 mantiene su corrección documental de REQ-025; no se cierra desde este incremento de pruebas.
