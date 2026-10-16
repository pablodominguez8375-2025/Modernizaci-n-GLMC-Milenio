# PMGM-QA-005 — Proceso de montaje del QA funcional (revisión y versión mejorada)

Estado: propuesta de Claude del 16-10-2026, a pedido del Sponsor / Product Owner. Es un **proceso**, no una orden de despliegue: no autoriza tocar `glmdch`, DNS, certificados ni firewall. Cada paso irreversible sigue necesitando la aprobación expresa del PO (#97).

## 1. Qué se revisó
PMGM-QA-001 (despliegue manual), `docs/installation/QA-SRV01-INSTALL.md` y `QA-SRV01-AUTODEPLOY.md` (instalable y despliegue por SHA), `QA-GLMDCH-SILO-CANDIDATE.md` (rama de #505/#506), QA-002/003/004, `infrastructure/caddy/Caddyfile.pilot`, `docker-compose.srv01.yml` y los Issues #97, #470, #501, #505.

## 2. Problemas encontrados
| # | Problema | Efecto |
| --- | --- | --- |
| P1 | Hay **tres procedimientos** que se pisan: QA-001 (compose.qa en `/opt/pmgm`), AUTODEPLOY/INSTALL (compose.srv01 en `/opt/centenario`) y el candidato Silo de `glmdch`. | Cada persona o IA puede seguir uno distinto. |
| P2 | El servidor se llama `srv01` en scripts y `glmdch` en las decisiones. | Confusión al leer evidencias. |
| P3 | El perfil `srv01` publica web (8081) y Keycloak (8180) por **HTTP en puertos separados** y dice «no exponer a Internet». El dominio `https://sistema.granlogiamixta.cl` exige HTTPS y un solo punto de entrada. | Falta la pieza de entrada TLS; hoy no se puede abrir el dominio con seguridad. |
| P4 | El patrón seguro ya existe en el piloto (`Caddyfile.pilot`: HSTS, CSP, Keycloak bajo `/identity`, consola y realm `master` negados) pero no se usa en QA. | Se corre el riesgo de repetir la exposición de la consola (#469). |
| P5 | Las **puertas** (seguridad/licencia, CI, BD2, S3, respaldo, host, despliegue, paridad) están repartidas en documentos y comentarios. | Nadie ve de un vistazo qué falta ni quién lo cierra. |
| P6 | `dev` se mueve con cada fusión; Pages tarda ~13 min y una fusión intermedia puede cancelar la publicación. | Se puede validar un SHA y desplegar otro. |
| P7 | La regresión de 42 controles prueba infraestructura y humo, pero **no** las cuentas de prueba por perfil ni la pasada de interfaz (QA-004). | «QA operativo» sin poder probar lo que ve cada perfil. |
| P8 | No hay cuentas sintéticas para hermanos sin cargo de grados 1/2/3 ni cargo vigente/vencido. | No se puede probar Agenda por grado ni «Tu situación». |
| P9 | Se pegaron credenciales SSH en un chat. | Incidente de secretos; hay que rotar. |
| P10 | Trabajo secuencial: seguridad, TLS y cuentas se esperan entre sí sin necesidad. | Plazo más largo. |

## 3. Proceso propuesto
### 3.1 Un solo camino y un solo nombre
- Documento canónico: **este** (QA-005) para el orden y las puertas; los demás quedan como detalle técnico (QA-001 manual de referencia; AUTODEPLOY/INSTALL para el instalable por SHA).
- Nombre único en documentos nuevos: «QA `glmdch`» (`srv01` se conserva solo en nombres históricos de archivos y proyecto Compose).
- Una sola dirección: `https://sistema.granlogiamixta.cl` (QA-004 §0).

### 3.2 Corte candidato congelado
1. ChatGPT propone un **SHA candidato** y lo etiqueta (`qa-AAAAMMDD-N`).
2. Mientras dure la validación, no se fusiona nada que lo cambie sin decirlo en #97 (si se fusiona, hay candidato nuevo y se repiten las puertas afectadas).
3. Pages, instalable y servidor deben mostrar **el mismo** SHA (`qa-current.json`, BUILD-INFO, MANIFEST). Si no coincide, no se despliega.

### 3.3 Tablero de puertas (una fila por puerta; el estado vive en #97)
| Puerta | Qué se prueba | Responsable | Evidencia | Estado al 16-10 (según los informes de #97/#505) |
| --- | --- | --- | --- | --- |
| G1 Seguridad del suministro | Silo/imagen sin HIGH/CRITICAL, licencia, procedencia | ChatGPT | Trivy, SBOM, VEX por CVE | **Rojo**: 11 HIGH (propuesta de orden en #505) |
| G2 CI del candidato | CI, Showcase, instalable, pruebas del perfil | ChatGPT | Runs con el SHA exacto | En curso (#506 borrador) |
| G3 Entrada pública segura | DNS, certificado, 443, HSTS, Keycloak bajo `/identity`, admin negado, realm sin `localhost`/IP | ChatGPT + operador | Salida de comprobaciones, sin secretos | **Sin diseñar** para QA (ver P3/P4) |
| G4 BD/OIDC segregados | API sin DDL, Keycloak con rol propio, auditoría solo inserción | ChatGPT | Pruebas BD2 | Pendiente en el perfil QA |
| G5 Almacenamiento real | Cuentas sin root, 403 a otros buckets, respaldo sin `DeleteObject` | ChatGPT | Prueba real contra Silo | Pasó en laboratorio (runner efímero) |
| G6 Respaldo y restauración | Ida y vuelta de PostgreSQL ×2 y objetos; custodia cifrada | ChatGPT | Informe de restauración | Parcial |
| G7 Estado del host | CPU, RAM, disco, servicios y puertos, solo lectura | ChatGPT (#501) + operador | Informe validado | **Pendiente** de activar #501 |
| G8 Cuentas de prueba | Usuarios `qa.*` por cargo y por grado, claves fuera del chat | ChatGPT | Lista de usuarios (sin claves) | **Faltan** (P8) |
| G9 Despliegue | Instalación por SHA, humo autenticado, 42/42 | Operador con aprobación del PO | Evidencia JSON + adenda Drive | No iniciado |
| G10 Interfaz | QA-004 y `pasada-ui-qa.mjs` por cuenta | Claude | `informe.md` por cuenta, Issues por hallazgo | No iniciado (guion listo) |

Regla: **no se avanza a G9 con ninguna puerta G1–G8 en rojo.** G10 se hace después de G9 y su resultado va a #97.

### 3.4 Pistas en paralelo
No hay motivo para esperar en serie. Pueden avanzar a la vez, cada una con su responsable:
- **Pista A (seguridad):** G1 y G4.
- **Pista B (entrada):** G3 (diseño con el patrón del piloto, ajuste de `redirectUris`/`webOrigins` al dominio, retiro de `localhost`/IP del realm publicado).
- **Pista C (datos y cuentas):** G8 con clave tomada de variable de entorno (`PMGM_QA_USER_PASSWORD`), como ya hace el realm.
- **Pista D (host):** G7 tras corregir y activar #501 (#514).
Se juntan en el SHA candidato (3.2) y recién entonces G9.

### 3.5 Quién hace qué
- **PO / Sponsor:** aprueba cada paso irreversible (activar la comprobación SSH, DNS/certificado, despliegue, excepciones de seguridad). Rota las credenciales expuestas.
- **ChatGPT:** infraestructura, seguridad del suministro, scripts, evidencia de G1–G9.
- **Operador humano de `glmdch`:** ejecuta lo que requiere el servidor, con comandos revisados; no se les pegan claves a los chats.
- **Claude:** revisión de cambios de infraestructura, parches puntuales vía PR separado, guía y pasada de interfaz (G10), registro del seguimiento.

### 3.6 Secretos (reglas duras)
- Credenciales solo por canal seguro y variables de entorno; nunca en chats, Git, Drive, logs ni capturas.
- SSH solo con la clave de solo lectura y comando forzado (#501) hasta que el PO autorice más.
- Si una clave aparece en un chat: se considera comprometida y se **rota** antes de seguir.

### 3.7 Definición de «QA funcional listo»
1. G1–G9 en verde con el mismo SHA que muestra Pages.
2. Cuentas de prueba creadas (G8) y entregadas por canal seguro.
3. G10 ejecutada sobre cada perfil: informe sin fallos de nivel P1 y hallazgos P2/P3 con Issue.
4. Evidencia única por despliegue: SHA, resultados G1–G10, 42/42, informe de interfaz, decisión del PO, persona responsable (adenda en Drive).
5. Declaración explícita de lo que **no** se probó (datos reales, producción, alta disponibilidad).

## 4. Cambios concretos que propone este documento
1. Adoptar QA-005 como camino canónico y avisar en QA-001 y QA-SRV01-INSTALL (nota de una línea; no cambia sus pasos).
2. Diseñar G3 reutilizando el patrón de `Caddyfile.pilot` para QA (Issue para ChatGPT).
3. Crear las cuentas de G8 (Issue para ChatGPT; ya pedidas en #97).
4. Congelar el candidato con etiqueta y registrar el SHA en #97.
5. Mantener el tablero de puertas como un comentario fijado en #97, actualizado por quien cierre cada puerta.
