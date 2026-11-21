# QA525 — contrato del corte firmado de esquema y API/WEB

Estado: propuesta de integración para #1072, **sin autoridad física**. Base de diseño `dev@eb1c4cf51176a70a71ec2174f976225232573b68` (21-11-2026). Este documento no es GO ni comando de glmdch.

## Separación de alcances

`qa525-go-cut-v1 / API_WEB_QA_ONLY` conserva su significado: el hijo `qa525-vnext-root-apiweb-child.py` rechaza migración, psql, identidad y volumen. No ampliar ese GO, ni invocar EF desde un callback de su host. Crear un esquema de GO y scope distintos, con configuración exacta y child dedicados, cargados únicamente por PRE-4 desde un manifiesto autenticado con pin externo. Cambiar de forma coordinada el contrato GO, config, launcher, cierre transitivo, pruebas y manifiesto. Una entrada nueva no se admite con config vieja ni viceversa.

## Autoridad que debe quedar fijada antes de root

La configuración autenticada y el GO deben ligar exactamente:

| Campo | Comprobación |
| --- | --- |
| Host y entorno | `glmdch`, QA solamente, usuario operador `soporte`; sin fallback a otra VM |
| Fuente y paquete | SHA de commit APP y SHA-256 del ZIP interno, bundle Sigstore offline y recibo OPS v3 de namespaces independientes |
| Migrador | digest OCI `sha256:…` de la imagen construida fuera de glmdch, atestación/manifest del mismo código; ejecutable/assembly y parámetros cerrados, sin tag mutable |
| Base | hash de lista ordenada de 100 IDs EF observados en T0, exactamente cuatro IDs siguientes y hash de lista ordenada de 104; fingerprint de rol/ownership y DB objetivo |
| SQL | hashes de `grant-pilot-db2.sh`, H7/H7b y `qa525-workshop-postmigration-gate.sql`, todos del mismo corte revisado |
| Imágenes API/WEB | IDs por digest de contenido de fuente APP, baseline físico T0, no nombres o tags |
| Continuidad | hash de admisión, plan, política, GO, nonce único y caducidad corta; G6 v2 backup identificable y restauración aislada revisada |

No basta introducir las ramas #1068–#1071 al ZIP: integrar y probar el **mismo SHA** final, congelar digests de salida y recién entonces firmar un GO para esa revisión. El dato T0 se mide físicamente en el volumen existente, no se presume a partir del historial de desarrollo.

## Fronteras de efectos

| Fase | Invariante antes de pasar | Si falla |
| --- | --- | --- |
| 0. Lectura y autenticidad | PRE-4, firmas y pins, expiración, cierre de módulos, T0 y plan exactos, ausencia de journal incompatible | STOP sin nonce gastado ni efectos |
| 1. Reserva | Cinco locks históricos + lease, nonce consumido una vez, journal `RESERVED` durable | STOP; ningún segundo intento reutiliza el nonce |
| 2. Respaldo | G6 v2 de BD/objetos con hash y prueba aislada correspondientes al corte, journal `BACKUP_VERIFIED` | STOP antes de DDL |
| 3. Aislamiento | API no admite escrituras, cero sesiones de escritura al DB objetivo, lease/locks vigentes; journal `WRITES_QUIESCED` | STOP, restaurar servicio previo solo si no hubo DDL |
| 4. EF | OCI pin por digest, `pmgm_migrator` no superusuario, exactamente 100→104; journal registra inicio antes del primer DDL y `SCHEMA_104` solo tras historia validada | `PARTIAL_SCHEMA_STOP`; sin Down ni API nueva |
| 5. Grants | H7/H7b bajo `pmgm_migrator`, owner guard sin fallback, privilegios runtime esperados; journal `GRANTS_VERIFIED` | `PARTIAL_SCHEMA_STOP` |
| 6. Gate | #1069 verifica 104, constraints, índices, triggers y ACL; `POSTGATE_PASS` durable | `PARTIAL_SCHEMA_STOP`, no reabrir API |
| 7. Aplicación | Revalidar imágenes/pins y G6, usar vuelo API/WEB revisado sin relajar su contrato; `APP_APPLIED` solo con resultado terminal claro | mantener esquema 104; rollback de imágenes únicamente si compatible, marcar parcial |
| 8. Servicio/UAT | Smoke autenticado, Talleres, Ficha del Taller, carga inicial Taller/Hermanos/Cargos, permisos y persistencia con datos sintéticos; capacidad real queda habilitable tras aceptación y fuente institucional | `UAT_STOP`; QA no se declara operativo |

El journal registra hash de entradas, fase en curso y salida terminal antes/después de cada efecto mediante escritura durable. Una caída entre DDL y el registro de éxito se trata como parcial desconocido, aun si la historia parece 104; recuperar requiere lectura/reconciliación supervisada y GO nuevo. El GO se consume antes del primer efecto y jamás se reusa. Mantener locks hasta finalizar la fase terminal; verificar su posesión antes y después de cada frontera. Nunca ejecutar una reversión EF `Down` automática ni afirmar restauración de base por revertir contenedores.

## Entregables bloqueantes antes de host

1. Integración exacta #1068, #1069, #1070 y #1071, revisión independiente H7b y ownership T0.
2. Child + host + config + GO/launcher nuevos, errores opacos, inputs de DB cerrados, pin OCI real y empaquetado del mismo SHA; suite de fallo en cada fase y reinicio con journal parcial.
3. Ensayo completo fuera del host con ZIP/bundle y firma desechable, incluyendo PostgreSQL 17 y volumen sintético de Clau (70 Talleres/≈1050 Hermanos), carga en tandas reales de hasta 30 hermanos.
4. Operación física supervisada en glmdch solo por Clau como interlocutora del Sponsor, con respaldo G6 v2 y T0 verificados. No pedir a Pablo comandos desde este hilo.
5. UAT de talleres y carga inicial. Datos personales reales ingresan solo por el circuito institucional de QA privado; no en GitHub, Drive, chat ni demo.

**Estado actual:** #1068 listo para revisión; #1069, #1070 y #1071 draft con CI exact-head reportada. No están integrados en dev, no hay GO nuevo, OCI pin, T0 físico ni despliegue. `QA_DEPLOY=NO_GO`.
