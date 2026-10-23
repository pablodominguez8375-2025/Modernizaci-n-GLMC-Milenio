# #604 — bloqueo de lecturas genéricas por ID para Taller 00 sintético

Fecha: 2026-10-23, Chile. Responsable backend ChatGPT, área #604 asignada `agente:chatgpt`. Base `dev@eec77c53f3bd0c8ad5120f6fa97ca3af2c4c9c6a`; Clau UI/Keycloak y multiperfil protegidos (#650/#651).

## Problema
PR #652 oculta organizaciones `Type=support` en el selector general, pero rutas generales GET por ID aún podrían devolver detalles del soporte:
- `GET /api/institutional/organizations/{id}/profile`;
- `GET /api/members?organizationId={id}`;
- `GET /api/members/{id}/profile` y `/history` para un miembro sintético afiliado a `support`.

Como el PO exige Taller 00 sintético aislado de cuadros, directorios y reportes reales, **no debe bastar conocer el UUID para consultar esos registros desde rutas de miembros/talleres institucionales**. Las futuras rutas privadas del simulador tendrán autenticación propia, alcance limitado y solo lectura.

## Corte backend acotado
Incorporar 404 genérico para una organización tipo `support` en perfil general, y 404 para el catálogo de miembros por id de organización `support` antes de cargar nombres. Para ficha/historial de miembro cuyo `MemberId` aparezca afiliado a `support` (incluso vínculo mixto accidental), 404 antes de proyectar el RUT, historial o datos del miembro. Consulta de tipo `support` sin distinguir capitalización. Esto no transforma cuentas reales, ni crea datos sintéticos, ni modifica permisos Keycloak.

Test HTTP/PostgreSQL con usuario autenticado de orden, Taller real, support y SUPPORT sintéticos, miembro sintético sin RUT/correo y miembro real de Taller válido. Verificar 404 a GET genéricos de soporte y que el Taller real siga operativo; sin datos reales ni migraciones.

## Dependencias / límites
- No sembrar Taller 00 antes de verificar aislamiento de escrituras y agregaciones del resto de módulos (#604).
- No implementar `Ver como` (#574), administración de Master bootstrap, roles, MFA o UI de Clau.
- No alterar `main`, glmdch ni producción.
- Triple CI, Showcase e Instalador QA exact-head SUCCESS y behind=0 antes de proponer merge solo a `dev`.
- Cuando se fusione, revalidar Pages público por SHA servido; QA físico es otra entrega.
