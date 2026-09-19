# PMGM-ARCH-011 — Hospitalaria integral: Taller + Gran Hospitalaria

## 1. Propósito

Completar el circuito de Hospitalaria del Proyecto Centenario reutilizando:

- movimientos existentes de Hospitalaria del Taller;
- Consejo de Administración auditable;
- snapshots de regularidad de Gran Hospitalaria;
- reglas de elegibilidad de Ceremonias.

No se crea un segundo módulo financiero ni se mezcla el Tronco de Beneficencia con Tesorería.

## 2. Fuentes institucionales

### Constitución y Reglamento General — art. 12.13

El Hospitalario:

- es fiel guardador y depositario del **Tronco de Beneficencia**;
- administra un fondo **independiente del Tesorero**;
- recauda los aportes del Tronco en las Tenidas;
- satisface socorros acordados por el **Consejo de Administración o el Venerable Maestro**;
- presenta mensualmente al Consejo un estado de caja y cuando lo solicite el Venerable;
- presenta balance al Consejo para su aprobación e incorporación a la Memoria Anual;
- atiende obras de beneficencia aprobadas por la Logia.

### Matrices 2026 de Drive

- Hospitalario: Hospitalaria, Tronco, ayudas, visitas, reposiciones, rendiciones y Consejo.
- Venerable Maestro: inspección y autorización, sin edición ordinaria del libro del Hospitalario.
- Secretaría: no administra Hospitalaria.
- Gran Hospitalaria: reposiciones, regularidad, requisito de Ceremonias y reportería protegida/consolidada.

## 3. Segregación de funciones

### Hospitalario del Taller

Puede:

- registrar ingresos/aportes;
- registrar socorros/egresos;
- conservar respaldo;
- vincular un socorro con acuerdo real del Consejo;
- preparar la rendición mensual agregada;
- registrar reposición pagada y referencia;
- vincular la revisión mensual del Consejo;
- enviar la rendición a Gran Hospitalaria.

No puede autoaprobar un egreso.

### Venerable Maestro

Puede:

- leer/inspeccionar la Hospitalaria de su Taller;
- aprobar egresos/socorros directamente.

No puede editar movimientos registrados por Hospitalaria como operación ordinaria.

### Consejo de Administración

No se simula mediante un botón individual.

La autorización por Consejo se acredita mediante un `LodgeCouncilDecision` real:

- categoría `benevolence_aid_proposal`;
- resultado `approved`;
- mismo Taller;
- monto exacto del socorro.

La revisión mensual se acredita mediante `LodgeCouncilFinancialReview`:

- área `hospitalaria`;
- mismo Taller;
- referencia de período/conclusión.

### Gran Hospitalaria

Puede:

- recibir/listar rendiciones mensuales enviadas;
- observar;
- conciliar;
- generar regularidad institucional.

No puede leer el libro privado de ayudas del Taller.

## 4. Frontera de privacidad

### Proyección local reservada

Puede contener, con minimización:

- referencia interna de beneficiario;
- destino resumido;
- referencia de respaldo;
- observación reservada.

No debe utilizarse para registrar diagnóstico médico detallado salvo futura necesidad normativa explícita y evaluación de privacidad.

### Proyección de Gran Hospitalaria

Sólo expone:

- Taller;
- período/corte;
- total ingresos;
- total egresos aprobados;
- neto del período;
- número de movimientos;
- número de egresos pendientes;
- reposición debida;
- reposición pagada;
- diferencia;
- referencia de pago;
- referencia de revisión del Consejo;
- estado/revisión institucional.

**No expone**:

- beneficiario;
- `memberReference`;
- destino de la ayuda;
- observaciones personales;
- detalle individual de socorros.

Esta frontera es obligatoria por diseño y se valida por tests.

## 5. Autorización de socorros

Todo egreso nace con `pending_approval`.

Vías válidas:

1. **Venerable Maestro**
   - endpoint de aprobación existente;
   - `ApprovalSource = venerable_master`.

2. **Consejo de Administración**
   - requiere acuerdo real aprobado;
   - mismo Taller;
   - categoría de socorro;
   - monto exacto;
   - `ApprovalSource = lodge_council`;
   - se conserva `CouncilDecisionId`.

## 6. Estado mensual del Taller

Una `HospitalariaMonthlySubmission` es única por Taller/año/mes.

Snapshot agregado:

- ingresos del período;
- egresos aprobados;
- neto;
- cantidad de movimientos;
- egresos pendientes;
- reposición debida/pagada;
- diferencia;
- referencia de pago;
- revisión del Consejo.

### Gate de envío

No se puede enviar si:

- existen egresos pendientes;
- no existe revisión mensual de Hospitalaria del Consejo;
- se declaró reposición pagada sin referencia/comprobante.

La rendición enviada no puede modificarse silenciosamente.

## 7. Revisión de Gran Hospitalaria

### Observada

- requiere motivo;
- deja la rendición en `observed`;
- genera regularidad `overdue` si existe diferencia de reposición;
- en otro caso genera regularidad `pending`.

### Conciliada

Sólo si:

- la rendición está enviada;
- `DifferenceAmount <= 0`.

Resultado:

- rendición `reconciled`;
- nuevo `HospitalariaRegularitySnapshot` con `up_to_date`;
- `SourceReference = hospitalaria-rendicion:{id}`.

Ceremonias continúa consumiendo el snapshot institucional, sin leer la caja privada.

## 8. Compatibilidad

Se conserva el endpoint manual histórico de regularidad de Gran Hospitalaria para excepción/compatibilidad.

El flujo ordinario objetivo pasa a ser:

`Hospitalaria Taller → autorización VM/Consejo → revisión mensual Consejo → rendición agregada → Gran Hospitalaria → conciliación → regularidad → Ceremonias`.

## 9. Auditoría

Acciones nuevas/prioritarias:

- `lodge.hospitalaria.movement.recorded`;
- `lodge.hospitalaria.expense.approved_by_venerable`;
- `lodge.hospitalaria.expense.approved_by_council`;
- `lodge.hospitalaria.monthly_submission.saved`;
- `lodge.hospitalaria.monthly_submission.submitted`;
- `hospitalaria.monthly_submission.observed`;
- `hospitalaria.monthly_submission.reconciled`.

## 10. Demo y pruebas

La demo funcional debe permitir comprobar con datos ficticios:

- Hospitalario: gestión local;
- Venerable: lectura + aprobación;
- Gran Hospitalaria: sólo agregados;
- Consejo: vínculo mediante decisiones/revisiones existentes;
- conciliación y regularidad posterior.

Pruebas:

- RBAC de Hospitalaria;
- mock frontend de privacidad;
- PostgreSQL end-to-end;
- QA-025 en srv01.

## 11. Estado

Implementado en rama `feature/hospitalaria-flujo-integral`, PR #114 abierto.

Pendiente antes de integrar:

- gates exact-head PMGM CI, Showcase y QA Installable;
- merge a `dev`;
- triple salida post-merge del mismo SHA;
- actualización de Issue #97 y Línea Base Maestra.

El despliegue físico en `srv01` sigue siendo un hito operacional separado y no se declara realizado.
