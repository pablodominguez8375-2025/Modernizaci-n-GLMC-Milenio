# Cuadro Mensual de Tesorería — segregación Taller / Gran Tesorería

Fecha: 2026-09-19  
PR: #112 — `feat(tesoreria): segregar Cuadro Mensual Taller y Gran Tesorería`

## Fuentes institucionales

- Constitución/Reglamento art. 12.12.
- `CUADRO PAGO GRAN TESORERÍA.xlsx`.
- Matriz Funcional Normativa de Cargos de Taller.
- Matriz Perfiles/Vistas/Firmas Proyecto Centenario.

## Cambio funcional

El Cuadro Mensual deja de operar como una pantalla exclusiva de Gran Tesorería y se separan responsabilidades:

`Tesorero del Taller → preparar/cuadrar/enviar → Gran Tesorería → revisar/conciliar institucionalmente`

### Tesorero del Taller

Puede, sólo para su Taller:

- crear el Cuadro del período;
- generar la nómina desde pertenencias vigentes;
- revisar grado/cargo al corte;
- aplicar ajustes estructurados con referencia autorizante;
- registrar transferencias/depósitos;
- enviar el Cuadro cuando está cuadrado.

El envío se bloquea si:

- `DifferenceAmount != 0`;
- existen identidades pendientes;
- el Cuadro no contiene líneas.

Después del envío no se permiten nuevos pagos silenciosos.

### Gran Tesorería

- lista/revisa Cuadros por Taller y período;
- no recibe controles de preparación en la UI ordinaria;
- ejecuta la conciliación institucional;
- la conciliación sigue generando `FinancialRegularitySnapshot`, fuente consumida por Ceremonias/reportes.

## RBAC

Se agrega `CanPrepareTreasuryStatement`:

- permitido al Tesorero/administrador técnico del Taller dentro de su organización;
- bypass administrativo de Gran Logia;
- no otorgado al rol Gran Tesorero como operación ordinaria.

`CanManageTreasuryRegularity` permanece como permiso institucional de Gran Tesorería.

## Demo

- el perfil “Tesorero del Taller · Demostración” recibe `canManageLodgeTreasury`;
- la pantalla distingue Preparación y Revisión;
- recupera Cuadros existentes por Taller/período;
- el mock reproduce el bloqueo de envío con diferencia pendiente.

## QA

- QA-024: Cuadro Mensual preparado por Taller y conciliado por Gran Tesorería;
- matriz srv01: 24 controles;
- pruebas unitarias de autorización;
- prueba PostgreSQL cubre bloqueo de envío descuadrado, pagos antes del envío, congelamiento posterior y conciliación.

## Estado

Integrado a `dev` mediante PR #112.

- merge funcional: `f2679b7cccd652d7cbb9fcd7688eee4d0c3a4bc3`;
- PMGM CI post-merge: `success`;
- Showcase/Pages post-merge: `success`;
- QA srv01 Installable: `success`;
- Pre-UAT Installable: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-f2679b7cccd6.zip`;
- SHA-256 ZIP público: `cae2bae623554ec0b42daedc78dda3703dd1042a63d47b059d480485b45ba159`;
- QA vigente: 24 controles, QA-001..QA-024;
- Issue #97 y Línea Base Maestra actualizados al corte funcional.

Pendiente operacional: despliegue físico `srv01`, smoke real, regresión 24/24 y posterior UAT institucional.
