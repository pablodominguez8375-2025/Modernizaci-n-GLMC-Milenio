# PMGM-QA-004 — Guía de pruebas de interfaz en el QA montado

Estado: guía preparada por Claude el 15-10-2026, a pedido del Sponsor / Product Owner (autorización registrada en #97). No instala ni despliega nada: complementa PMGM-QA-001 (despliegue), PMGM-QA-002 (checklist de ambiente) y PMGM-QA-003 (UAT de flujos). El montaje físico y los accesos al servidor los define ChatGPT en #501 con aprobaciones del PO por separado.

## 0. Dirección pública del QA (decisión del PO, 16-10-2026)

El dominio público del ambiente QA es **https://sistema.granlogiamixta.cl/**. Es la única dirección que se entrega a quienes prueban; la demo de Pages sigue siendo aparte y ficticia.

- Configuración: `PMGM_QA_WEB_PUBLIC_URL=https://sistema.granlogiamixta.cl` (alimenta las URL de retorno y los orígenes web de Keycloak en `infrastructure/keycloak/pmgm-realm.json`) y, si se usa el perfil piloto, `PMGM_PILOT_HOSTNAME=sistema.granlogiamixta.cl` y `PMGM_PILOT_PUBLIC_URL=https://sistema.granlogiamixta.cl`.
- Solo HTTPS con certificado válido; ninguna otra dirección ni IP se registra como URL de retorno.
- El QA expuesto en Internet usa **solo datos sintéticos** y las cuentas `qa.*`; no se publica sin cerrar los hallazgos de seguridad que aplican (consola de Keycloak, #469; auditoría, #470).
- El montaje, el DNS y el certificado los ejecuta ChatGPT con aprobación del PO (#97, #501). Esta guía no toca el servidor.

## 1. Reglas que no cambian

- Solo datos sintéticos. Capturas sin datos personales reales.
- Sin credenciales en chats, Git, Drive, logs ni capturas. Las cuentas de prueba y sus claves las entrega el operador por un canal seguro.
- La demo de Pages (`VITE_USE_MOCKS=true`) **no es QA**: tiene selector de perfil y datos ficticios. En el QA real cada persona entra con su cuenta (OIDC/Keycloak) y el backend decide qué ve.
- Un resultado de UI aquí no reemplaza el UAT (#25 y olas A–E); lo prepara.

## 2. Antes de probar: verificar qué se montó

1. Consultar `downloads/qa-current.json` en Pages (`sourceSha`, `filename`, `sha256`) y anotar el SHA.
2. Comprobar que el paquete instalado en el servidor corresponde a ese SHA: checksum del `.zip`, `BUILD-INFO` y `MANIFEST` (procedimiento de #97).
3. Comprobar que la versión visible en pantalla («UI QA vX.XXX» en el encabezado) coincide con la del SHA.
4. Registrar SHA, versión, fecha y quién verificó en la evidencia de despliegue (QA-001 §18).

Si algo no coincide, **no se prueba**: se informa en #97.

## 3. Cuentas de prueba necesarias

Existen en el realm de QA cuentas por cargo del Taller 23 (`qa.admin`, `qa.taller23`, `qa.venerable23`, `qa.exvenerable23`, `qa.primer23`, `qa.segundo23`, `qa.orador23`, `qa.secretaria23`, `qa.tesoreria23`, `qa.hospitalaria23`). **Faltan** para probar lo hecho en v0.107–v0.111 y la regla de Agenda por grado (#499):

| Perfil faltante | Para qué |
| --- | --- |
| Hermano **sin cargo**, grado 1 (Aprendiz) | «Tu situación» sin cargo; Agenda solo de 1°; Avisos de hermano |
| Hermano **sin cargo**, grado 2 (Compañero) | Agenda de 2° y 1° |
| Hermano **sin cargo**, grado 3 (Maestro) | Agenda de 3°, 2° y 1° |
| Hermano con cargo **vigente** y otro con cargo **vencido** | «Tu situación» muestra solo el cargo vigente |
| Gran Secretaría, Régimen Interior, Gran Archivero, Biblioteca editor/revisor | Menús y Mi mesa de trabajo (QA-001 §13) |

Pedido a ChatGPT: agregarlas al realm de QA con claves entregadas fuera del chat.

## 4. Qué debe ver cada perfil

| Perfil | Menú | Inicio | Avisos |
| --- | --- | --- | --- |
| Hermano sin cargo | Mi espacio (entrevistas, Mi ficha, Mis pagos, Mis asistencias, Privacidad y mis datos, Agenda, Avisos, Insinuaciones e Iniciación, Biblioteca) | «Tu situación»: Taller · grado (sin cargo) | Solo informativos: insinuaciones y agenda |
| Hermano con cargo | Lo anterior + herramientas del cargo | «Tu situación»: Taller · grado · cargo vigente | Los de su cargo |
| Venerable Maestro | Mi Taller: Gestión Logial, Tesorería del Taller, Hospitalaria del Taller | Contadores en una fila también en celular | Avisos de gestión |
| Administrador | Parámetros del sistema y gestión | Sin «Mis pendientes» repetido | Todos, incluida retención documental |

Regla general: **un solo menú por perfil, sin funciones repetidas, sin pestañas dentro de vistas, sin formularios abiertos** (se usan paneles que se abren con un botón).

## 5. Pruebas de interfaz (por perfil)

### 5.1 Pantalla de ingreso (sin sesión)
- [ ] Panel azul con logo GLMCh, título «Ingresar» y un solo botón «Ingresar con mi cuenta» (52 px).
- [ ] Mensaje «Ingrese con su cuenta institucional.»; en carga, botón deshabilitado y «Preparando acceso…».
- [ ] Error de ingreso: aviso como alerta y botón «Intentar de nuevo».
- [ ] Ayuda: «¿No puedes ingresar? Escribe a soporte@granlogiamixta.cl» (enlace de correo).
- [ ] 360, 390, 768 y 1440 px sin desborde horizontal.
- [ ] Al pulsar el botón se llega al formulario de Keycloak (el tema corporativo es el Issue #502; hasta entonces se ve el formulario estándar: anotarlo, no es defecto de la app).

### 5.2 Inicio
- [ ] «Tu situación» muestra Taller, grado (Aprendiz/Compañero/Maestro) y cargo vigente en español; nunca «Autenticado» ni códigos como `secretary`.
- [ ] Contadores en una fila de 3 columnas en 360/390 px.
- [ ] Sin bloque «Mis pendientes» repetido para el Administrador.

### 5.3 Menú y títulos
- [ ] El título de la página es igual al nombre del menú (Secretaría del Taller, Hospitalaria del Taller, Gestión Logial › Vista general).
- [ ] El menú funciona en PC y celular; los grupos se pliegan; ningún módulo sin permiso aparece.

### 5.4 Gestión Logial (Venerable / Secretaría)
- [ ] Tarjeta «Tenidas y actas» con número de tenidas pasadas sin cerrar; «Docencia» con instrucciones vencidas sin registrar. Contrastar los números con las listas internas.
- [ ] Todas las secciones abren su pantalla con «← Volver a Gestión Logial».

### 5.5 Mis asistencias (hermano)
- [ ] La lista aparece primero; los filtros están bajo «Filtrar» (se abre solo si hay un filtro activo y muestra cuántos).
- [ ] Tipo escrito como «Tenida», «Ceremonia», «Instrucción»; estados con mayúscula inicial.

### 5.6 Privacidad y mis datos
- [ ] Textos legibles, cinco derechos, correo de contacto visible (valor del parámetro `system.privacy.contact_email` cuando #480 esté integrado).
- [ ] Los textos están en revisión jurídica: reportar errores de redacción, no corregirlos aquí.

### 5.7 Avisos
- [ ] Cada perfil ve solo sus avisos; el hermano no ve avisos de gestión ni de administración. **Aquí se contrasta el reparto real del backend con el de la demo** (hermano 2, gestión 6, administrador 7); cualquier diferencia se informa a ChatGPT.
- [ ] Un aviso informativo para el hermano no pide «decidir».

### 5.8 Agenda por grado (cuando se integre #500 / #499)
- [ ] Aprendiz solo ve eventos de 1° y generales; Compañero 2° y 1°; Maestro 3°, 2° y 1°; cargo vigente ve todos los grados de su Taller; evento sin grado lo ve todo hermano del Taller; otro Taller no ve nada.
- [ ] Etiqueta de grado visible y resumen «Mostrando: tu grado y los inferiores» (UI de Claude, pendiente del contrato).

## 6. Accesibilidad y responsive (cada vista tocada)
- [ ] Anchos 360/390, 768 y 1440 px: sin desborde horizontal ni texto cortado.
- [ ] Botones y filas tocables de al menos 48 px; texto de al menos 14 px.
- [ ] Foco visible con teclado; contraste legible; sin dependencia solo del color.
- [ ] Fechas en `America/Santiago`, dinero en CLP, textos en español de Chile.

## 7. Cómo reportar un hallazgo

Un Issue por hallazgo, con el SHA probado, perfil/cuenta, ancho de pantalla, pasos, resultado esperado y obtenido, y captura sin datos personales.

| Tipo | Etiqueta | Responsable |
| --- | --- | --- |
| Pantalla, textos, responsive, accesibilidad, identidad | `agente:claude` | Claude |
| API, permisos, datos, avisos reales, Keycloak, infraestructura | `agente:chatgpt` | ChatGPT |
| Regla de negocio dudosa | pregunta al PO en el Issue | Product Owner |

Severidad: **P1** impide trabajar o expone datos que no corresponden; **P2** confunde o hay que rodear; **P3** estético o menor.

## 8. Resultado
Al terminar una pasada, dejar en #97: SHA probado, perfiles recorridos, anchos revisados, hallazgos con su Issue y conclusión («apto para continuar con UAT» o «con observaciones»). Esa conclusión es de interfaz; no acredita QA/UAT ni autoriza promover a `main`.

## 9. Pasada automatizada (opcional, de solo lectura)

`scripts/qa-ui/pasada-ui-qa.mjs` repite en el navegador las comprobaciones de §5 y §6 que se pueden automatizar: pantalla de ingreso (un botón, 48 px, ayuda a soporte, uso restringido), versión visible, «Tu situación», menú sin repetidos, título igual al menú, sin desborde horizontal a 360/390/768/1440 px, textos menores de 13 px, formularios abiertos fuera de panel y los grados visibles en Agenda. No escribe nada.

- Requiere `npm i playwright` fuera del repo y un Chromium (`CHROME_PATH` opcional).
- QA real: `QA_BASE_URL=https://sistema.granlogiamixta.cl QA_USER=… QA_PASSWORD=… QA_LABEL="hermano sin cargo grado 1" node scripts/qa-ui/pasada-ui-qa.mjs`. Las credenciales van **solo por variables de entorno**; el script exige HTTPS y no guarda ni imprime credenciales ni cookies.
- Demostración: `QA_MODE=demo QA_BASE_URL=<url de la demo> node scripts/qa-ui/pasada-ui-qa.mjs`.
- Salida en `QA_OUT` (`informe.json` e `informe.md`). Capturas solo con `QA_SCREENSHOTS=1` y únicamente con cuentas y datos sintéticos.
- Un «warn» pide mirar; un «fail» es un hallazgo. Para cada uno, un Issue según §7. El script no reemplaza el recorrido humano ni el UAT; la lectura de Agenda debe contrastarse con el grado y cargo de la cuenta usada.
