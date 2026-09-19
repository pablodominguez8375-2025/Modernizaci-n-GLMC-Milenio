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

Implementado en rama del PR #114. Pendiente gates exact-head, merge a `dev`, triple salida post-merge y actualización de Issue #97/Línea Base Maestra.
