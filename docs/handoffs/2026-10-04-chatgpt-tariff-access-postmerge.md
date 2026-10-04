# Cierre técnico — permisos efectivos del tarifario

Issue #266 / PR funcional #320. Inicio dev `be69bc341f99daa9e9542b776c073adab31ae651`; HEAD validado `507bac11a1e7b807a55d58997d5711b45a39805c`; squash funcional dev `59e270bc9cb37d5209b4aaed52bd2c7ce90d4446`. Árbol `8ce7cc434844e10c29974b96556e8c9404dcec15` idéntico al probado. Main sigue `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

## Comportamiento entregado

El catálogo/versiones de decretos exige treasury/view y el registro de un nuevo decreto treasury/create, para sujetos administrados en Orden, intersectados con CanManageTreasuryRegularity vigente. Un perfil técnico no crea cargos. La asignación revocada/futura/vencida conserva condición administrada: no retorna automáticamente al acceso antiguo. Proyección propia mínima, privada/no-store; pantalla retira datos/asistente cuando pierde acceso. API real revisa cada petición y demo reproduce la misma política.

Se preservan tarifas, históricos, snapshots de pagos/cargos, vigencia futura, inmutabilidad/concurrencia y auditoría de decretos. Sin migración. [DB-008](../modelo-datos/PMGM-DB-008-acceso-efectivo-tarifario.md) y [registro GOV-004](../modelo-datos/cambios/2026-10-04-issue-266-acceso-tarifario.md) integrados con los 12 archivos funcionales. [Handoff inicial](2026-10-04-chatgpt-tariff-access.md) incluye revisión Drive previa y reservas.

## Evidencia exact-head

| Gate | Run | Resultado |
|---|---|---|
| PMGM CI | 37241084375 | SUCCESS |
| Showcase | 37241084357 | SUCCESS |
| QA Installable | 37241084369 | SUCCESS |

Backend PostgreSQL 485/485 PASS, cero fallos/omisiones; nuevas pruebas de política y HTTP de consulta, creación, ausencia de escalamiento y revocación PASS. Frontend 414 PASS, lint/build PASS. Smoke autenticado, infraestructura y piloto/recuperación SUCCESS. Gate local de privacidad/clasificación y 61 migraciones PASS.

TRX artifact `11316852797`: SHA-256 `82f5d6c558296285d32c0156c1321ec05cb1f71775fdc7f1ae0730b396610fee`; TRX interno `9b649dc5290c2142f9ee8883647fea9ac7390179d9bf921a5ebbe25bfd895bbf`, descargados/verificados. PostgreSQL configurado en logs.

Responsive artifact `11317768108`: SHA-256 `ede9f8f28a8b58b891b6fd4a754af11cac0369e6e063e5d4afa67673090797ea`, descargado con CRC verificado: 761 PNG; 21 perfiles / 593 recorridos móviles. Lista y asistente inspeccionados directamente a 360 y 1440; sin cambios de identidad. No certificar capturas especiales de perfiles dinámicos administrados: esa política está cubierta por pruebas de API/demo.

QA artifact `11317601512`: SHA-256 `8917d55d5b678396eaff23f820d85cfa216065c12e4f0364e7989458fca21eaa`; ZIP `Proyecto-Centenario-QA-srv01-507bac11a1e7.zip`, SHA-256 `bb362b63c9782f2fde5524e9dd6fd303d0328c2c4e12e3cca07026882b1fc257`. CRC, 1015/1015 entradas MANIFEST, SOURCE_SHA y BUILD_RUN_ID 37241084369 verificados. Evidencia de rama, no confundir con el paquete público posterior al merge.

## Publicación y registro

Postmerge/publicación de `59e270b…` en curso al crear este handoff; los recibos de #320, PR documental e Issue #266 completarán SHA publicado, gates postmerge y checksum del paquete público. Pages previo verificado en `be69bc3…`; no se reutiliza como evidencia del nuevo corte. URL: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/

Línea Base Drive actualizada y leída de retorno con historial previo preservado; cierre final se agregará tras verificar publicación. PR documental añade este handoff y sólo una línea enlazada en START-HERE según GOV-003.

## Seguimiento y pendientes

#275 y #308 cerradas como completadas tras comprobar PR #276/#309 integradas y sus cuatro workflows postmerge SUCCESS. Admisiones sigue pendiente de UAT institucional separada en #97; #116 conserva su bloqueo histórico.

#266 permanece abierta: quedan otras operaciones de Gran Tesorería, Hospitalaria, navegación global y otros módulos. Este corte sólo aplica permisos a catálogo/versiones/registro de decretos; no certifica protección global. #191 conserva definiciones institucionales contables pendientes. Main/srv01/UAT pausados; **despliegue QA pendiente**, sin instalación/schema físico ni aceptación institucional certificados. Reservas funcionales liberadas; documental se libera al integrar/verificar sus gates.
