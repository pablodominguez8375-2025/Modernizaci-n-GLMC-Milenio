# Hospitalaria integral — Taller + Gran Hospitalaria

Fecha: 2026-09-19  
PR: #114 — `feat(hospitalaria): flujo integral Taller y Gran Hospitalaria`

## Fuentes

- Constitución y Reglamento General, art. 12.13.
- Matriz Funcional Normativa de Cargos de Taller.
- Matriz Perfiles/Vistas/Firmas Proyecto Centenario.
- Línea Base Maestra vigente.

## Funcionalidad

### Hospitalaria del Taller

- Tronco de Beneficencia separado de Tesorería.
- Registro de aportes e ingresos.
- Registro de socorros/egresos con respaldo.
- Egresos pendientes de autorización.
- Venerable Maestro puede aprobar.
- Consejo puede aprobar socorros a través de acuerdo real auditable.
- Estado mensual y revisión por Consejo.
- Rendición agregada de reposiciones hacia Gran Hospitalaria.

### Consejo

La autorización por Consejo no se simula.

Se exige:

- acuerdo `benevolence_aid_proposal`;
- `approved`;
- mismo Taller;
- monto exacto.

La rendición mensual vincula además una revisión `hospitalaria` del Consejo.

### Gran Hospitalaria

Recibe únicamente:

- Taller/período;
- ingresos;
- egresos aprobados;
- neto;
- cantidades agregadas;
- reposición debida/pagada;
- diferencia;
- comprobante/referencia;
- revisión del Consejo;
- estado de rendición.

No recibe:

- beneficiarios;
- referencias personales locales;
- destino de socorro;
- observaciones privadas.

Puede observar o conciliar. Conciliar genera `HospitalariaRegularitySnapshot(up_to_date)` para Ceremonias.

## RBAC

- Hospitalario: gestiona y lee Hospitalaria propia.
- Venerable: lee y aprueba, no edita.
- Secretaría: sin acceso administrativo de Hospitalaria.
- Gran Hospitalaria: regularidad/rendiciones agregadas, sin libro privado.
- GranLogiaAdmin: bypass técnico controlado.

## Datos y migración

- nueva `HospitalariaMonthlySubmission`;
- `ApprovalSource` y `CouncilDecisionId` en movimientos;
- migración `20260919043000_AddHospitalariaMonthlySubmissions`;
- una rendición por Taller/año/mes.

## Pruebas

- RBAC Hospitalaria.
- PostgreSQL end-to-end.
- mock frontend de privacidad.
- demo por perfiles.
- QA-025; regresión total: 25 controles.

## Estado

Integrado mediante PR #114.

- merge SHA: `44b6cc90a56e508c8c45e85d040e928738e1b5d8`;
- PMGM CI: `success`;
- Showcase/Pages: `success`;
- QA srv01 Installable: `success`;
- Pre-UAT Installable: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-44b6cc90a56e.zip`;
- SHA-256: `c5474a135c879e7df99768bc19130f7774e0583b2e8f06560e99f11bf2d2a6e6`;
- QA vigente: 25 controles;
- Issue #97 y Línea Base Maestra actualizados.

Pendiente operacional: despliegue físico en `srv01`, smoke real, regresión 25/25 y UAT institucional.
