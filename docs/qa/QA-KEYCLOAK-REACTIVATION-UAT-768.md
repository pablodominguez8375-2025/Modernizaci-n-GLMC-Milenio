# QA #768 — comprobación solo lectura de JWT previos a la reactivación

**Estado:** herramienta preparada en PR #780, NO se ha ejecutado UAT física ni
se han habilitado las mutaciones de cuentas. Exclusivamente cuentas sintéticas
con autorización del responsable de QA. La rama main queda fuera.

## Riesgo y criterio de aceptación

La mera introspección active=true y el estado enabled=true de Keycloak
pueden ser insuficientes para demostrar que la sesión original sigue abierta
tras desactivar/reactivar una identidad. El PR #778 integró comprobación en vivo
de sid / session_state contra la lista actual de sesiones Keycloak,
bajo la bandera desactivada RequireLiveTokenIntrospection=false.

La aceptación completa exige:
1. En **entorno QA aislado** y en una cuenta de prueba qa.* distinta de
   qa.admin, obtener un JWT previo a desactivar, aún no vencido y con
   sid o session_state; resguardar el token en archivo privado.
2. Por procedimiento autorizado **fuera del script**, desactivar y cerrar
   sesiones anteriores, y después reactivar la misma cuenta con un nuevo
   inicio de sesión, verificando permisos efectivos del Máster. No realizar
   estas acciones mientras las banderas permanezcan deshabilitadas.
3. Obtener otro JWT nuevo de la **misma identidad/UUID**, con nueva sesión.
   Ambos tokens deben permanecer dentro de exp para que el rechazo
   del antiguo no sea simplemente expiración.
4. Con RequireLiveTokenIntrospection=true activado únicamente por un
   procedimiento de pruebas **ya autorizado**, el nuevo token debe recibir
   HTTP 200 en GET /api/session/me y el antiguo HTTP 401. Una respuesta
   200 del antiguo falla; una respuesta 403/5xx del nuevo no constituye
   aceptación. La herramienta se limita a observar, nunca habilita flags.
5. Corroborar con Keycloak y la auditoría institucional que la sesión
   anterior terminó, sin alterar ni borrar hechos históricos.
6. Volver al estado de configuración aprobado al terminar la UAT.
   Ningún resultado de este script autoriza por sí solo producción o
   eliminación definitiva de identidades.

## Herramienta incluida

scripts/qa/check-keycloak-session-uat.py hace **solo dos GET HTTPS**
al endpoint fijo https://sistema.granlogiamixta.cl/api/session/me.
No consulta administrativamente Keycloak ni ejecuta POST/DELETE.
Solo admite un mismo usuario qa.* no qa.admin, UUID igual, issuer
institucional fijo y dos sesiones distintas. Verifica TLS de servidor
con CA del host, rechaza redirects y HTTP 403/5xx, y no conserva respuestas.

Los JWT de prueba se suministran exclusivamente por dos archivos
regulares, propiedad root, permisos 0600, sin enlaces simbólicos.
Nunca pasar JWT en línea de comandos, GitHub, Drive, comentarios,
pantallazos, artefactos ni logs. En el host QA, una vez realizado
el procedimiento autorizado y depositados los archivos privados:

~~~bash
sudo python3 scripts/qa/check-keycloak-session-uat.py \
  --old-token-file /run/pmgm/old-synthetic-jwt \
  --fresh-token-file /run/pmgm/new-synthetic-jwt \
  --confirm-synthetic-only
~~~

Las rutas son ilustrativas, **no asumen** que /run/pmgm exista. El
operador debe proporcionarlas por canal seguro, revisar propiedad/modo
de los archivos y retirarlos conforme a la política institucional.
Evitar volcar variables de entorno y activar trazas (set -x).
La herramienta no ejecuta el procedimiento de desactivar/reactivar.

Salida permitida: QA_KEYCLOAK_REACTIVATION_READONLY: PASS
o BLOCKED con código genérico; no contiene tokens, UUID ni nombres.
El resultado debe asociarse por SHA de API/web, versión de Keycloak
y fecha en un registro privado. No copiar tokens como evidencia.

## Pruebas herméticas antes de UAT

~~~bash
python3 -m unittest discover -s tests -p 'test_keycloak_session_qa_readonly.py' -v
python3 -m py_compile scripts/qa/check-keycloak-session-uat.py
~~~

tests/test_keycloak_session_qa_readonly.py usa únicamente JWT imposibles
de autenticar, y un stub HTTP local para probar 401/200, rechazos de
identidad y emisor, sesiones inconsistentes, fechas, redirects y fallos.

## Bloqueos y alcance de este PR

- PR #780 entrega **el instrumento de medición**, no ejecución de UAT.
- QA glmdch no debe actualizarse con un Silo que falle la puerta de
  suministro (#771 / #777, CVE y validación incompleta).
- EnableLifecycleMutations=false y RequireLiveTokenIntrospection=false
  se mantienen por defecto. Habilitarlas requiere procedimiento,
  respaldo, control de permisos y autorización independiente.
- Los roles de Máster y la excepción qa.admin tienen pruebas y
  autorización propias. Este script jamás realiza el borrado.
- El CI de PR y dev, la publicación Pages y el instalable son
  controles distintos; identificar siempre SHA y estados exactos.

Continuidad: issue #768, PR #778 integrado, PR #780 y el estado
ejecutivo vigente en la carpeta Proyecto Centenario de Drive.
