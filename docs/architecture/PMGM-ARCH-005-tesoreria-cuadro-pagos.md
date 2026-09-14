# PMGM-ARCH-005 — Gran Tesorería: Cuadro mensual de pagos y conciliación

## 1. Fuente institucional analizada

Fuente: `CUADRO PAGO GRAN TESORERÍA.xlsx`, hoja `Cuadro TESORERÍA`.

El archivo modela una nómina mensual del Taller para pago de cuotas sociales a Gran Tesorería. Contiene:

- identificación del Taller y fecha de corte del cuadro;
- secciones separadas de Maestros, Compañeros y Aprendices;
- por persona: RUT, nombre, cargo cuando aplica, valor de cuota y observaciones;
- ejemplos de rebajas/exenciones justificadas por Plancha en la columna Observaciones;
- total de cuotas del mes;
- forma de pago por transferencia y/o depósito;
- RUT/nombre de quien paga o deposita, fecha y monto;
- total por transferencia;
- total por depósito;
- diferencia entre total adeudado y total pagado.

En el archivo de referencia, la conciliación se expresa como:

`Diferencia = Cuotas a pagar del mes - Total transferencias - Total depósitos`.

El propio formulario instruye que el Taller debe remitir el Cuadro de Pagos para que el pago pueda ser registrado y que la diferencia debe revisarse para confirmar que lo adeudado coincide con lo pagado.

Los importes y textos de rebajas presentes en la plantilla se consideran ejemplos del documento fuente. No se codifican como reglas permanentes sin una norma institucional vigente que los confirme.

## 2. Situación actual del sistema

El módulo Treasury ya dispone de `FinancialRegularitySnapshot`, que conserva la regularidad por Taller o miembro en una fecha determinada y es consumido por la elegibilidad de ceremonias.

Ese snapshot es adecuado como resultado institucional de regularidad, pero no conserva el detalle que explica cómo se obtuvo: nómina, monto individual, ajuste autorizado, pagos y conciliación mensual.

Por lo tanto, el Cuadro de Pagos debe incorporarse como fuente transaccional/auditable y `FinancialRegularitySnapshot` debe seguir siendo una proyección/resultado derivado.

## 3. Principios obligatorios

1. El RUT puede utilizarse como dato de conciliación/importación, nunca como clave primaria técnica.
2. Cada línea debe vincularse al `MemberId`/membresía institucional cuando la identidad haya sido resuelta.
3. El grado utilizado para el cobro debe ser el grado vigente a la fecha de corte y debe quedar congelado en el cuadro para auditoría.
4. Cargos, rebajas, exenciones y observaciones no deben alterar retroactivamente cuadros cerrados.
5. Una rebaja/exención debe registrar su fundamento y referencia de autorización cuando corresponda; no se deduce automáticamente desde texto libre.
6. El sistema debe calcular el total esperado, total pagado y diferencia; la diferencia no se captura manualmente.
7. La regularidad de Gran Tesorería debe derivarse de información registrada por Tesorería, evitando solicitar nuevamente al Taller un comprobante que el sistema ya posee y validó.
8. Los cuadros cerrados y sus conciliaciones deben ser auditables e inmutables salvo procedimiento explícito de rectificación.
9. Los datos financieros y personales se muestran según rol y mínimo privilegio, conforme a Ley 21.719.

## 4. Modelo propuesto

### 4.1 `TreasuryMonthlyStatement`

Representa el Cuadro de Pagos de un Taller para un período.

Campos mínimos:

- `Id: Guid`;
- `OrganizationId: Guid`;
- `PeriodYear: int`;
- `PeriodMonth: int`;
- `CutoffDate: DateOnly`;
- `Status`: `draft | submitted | observed | reconciled | closed | rectified`;
- `ExpectedAmount`;
- `TransferAmount`;
- `DepositAmount`;
- `PaidAmount` derivado;
- `DifferenceAmount` derivado;
- `SourceReference` opcional para importación/legado;
- `SubmittedAtUtc`, `ReconciledAtUtc`, `ClosedAtUtc`;
- usuario/actor responsable mediante auditoría.

Restricción: un Taller no puede tener dos cuadros activos para el mismo período, salvo versión de rectificación vinculada al original.

### 4.2 `TreasuryMonthlyStatementLine`

Congela la composición del cuadro a la fecha de corte.

Campos mínimos:

- `Id`;
- `StatementId`;
- `MemberId`;
- `MembershipId` cuando corresponda;
- `DegreeCodeAtCutoff`;
- `OfficeCodeAtCutoff` opcional;
- `BaseAmount`;
- `AdjustmentAmount`;
- `PayableAmount` derivado;
- `AdjustmentType` opcional;
- `AuthorizationReference` opcional;
- `Observation`;
- `IdentityMatchStatus` para importaciones que aún requieren corroboración.

El RUT mostrado/importado se resuelve contra la identidad maestra, pero no forma parte de claves ni relaciones.

### 4.3 `TreasuryPayment`

Registra pagos asociados al cuadro.

Campos mínimos:

- `Id`;
- `StatementId`;
- `PaymentMethod`: `transfer | deposit`;
- `PaymentDate`;
- `Amount`;
- `PayerDisplayName`;
- `PayerRut` cuando el proceso institucional lo requiera, clasificado como dato personal;
- `Reference`/comprobante controlado;
- `RecordedAtUtc`;
- `RecordedBy` vía auditoría.

Un cuadro puede tener múltiples pagos parciales. Los totales se suman desde estas filas.

### 4.4 `TreasuryAdjustment`

Las rebajas/exenciones deben ser datos estructurados y versionables, no sólo observaciones de texto.

Campos mínimos:

- `Id`;
- `MemberId`;
- `OrganizationId` si la autorización es contextual al Taller;
- `AdjustmentType`;
- `EffectiveFrom` / `EffectiveUntil`;
- monto fijo o regla parametrizada;
- `AuthorizationReference`;
- estado `active | expired | revoked`;
- auditoría.

La existencia en la plantilla de ejemplos como tercera edad, cónyuge, estudiante o Past Activo no se convierte automáticamente en catálogo normativo definitivo. Ese catálogo debe confirmarse con la normativa vigente.

## 5. Conciliación

Cálculos del sistema:

```text
ExpectedAmount = SUM(StatementLines.PayableAmount)
TransferAmount = SUM(Payments.Amount WHERE PaymentMethod = transfer)
DepositAmount = SUM(Payments.Amount WHERE PaymentMethod = deposit)
PaidAmount = TransferAmount + DepositAmount
DifferenceAmount = ExpectedAmount - PaidAmount
```

Un cuadro puede pasar a `reconciled` sólo si:

- contiene las líneas exigibles del período;
- los pagos están registrados y son trazables;
- `DifferenceAmount == 0`, salvo excepción/ajuste formal expresamente autorizado;
- no existen observaciones de identidad o conciliación abiertas.

## 6. Derivación de regularidad

`FinancialRegularitySnapshot` no se elimina. Se genera/actualiza como resultado de Gran Tesorería cuando el cuadro del período queda conciliado/cerrado.

Regla de diseño:

- fuente de detalle: `TreasuryMonthlyStatement` + líneas + pagos + ajustes;
- resultado consumible por otros dominios: `FinancialRegularitySnapshot`;
- Ceremonias consulta el snapshot vigente y su `SourceReference` apunta al cuadro/conciliación que lo sustenta;
- el Taller no debe volver a adjuntar la misma prueba si Gran Tesorería ya la registró y validó en el sistema.

## 7. Flujo funcional

1. Taller/Tesorería genera borrador del período.
2. El sistema propone miembros activos y su grado/cargo a la fecha de corte.
3. Se aplican sólo ajustes vigentes y autorizados.
4. El responsable revisa y envía el cuadro.
5. Gran Tesorería revisa identidad, composición y montos.
6. Se registran transferencias/depósitos.
7. El sistema recalcula la diferencia en tiempo real.
8. Si diferencia = 0 y no hay observaciones abiertas, Gran Tesorería concilia.
9. Al cerrar, se emite el snapshot de regularidad y queda disponible para elegibilidad de ceremonias/reportes.
10. Una rectificación crea trazabilidad nueva; no reescribe silenciosamente el cuadro cerrado.

## 8. Importación del Excel actual

El importador debe:

- identificar Taller, fecha de corte y período;
- reconocer secciones Maestros/Compañeros/Aprendices;
- resolver identidades usando RUT como criterio de matching, no como ID;
- importar cargo, monto y observación;
- importar pagos y fechas;
- comparar el total esperado y la diferencia calculada;
- enviar identidades no resueltas o inconsistencias a corroboración/calidad de datos;
- conservar referencia al archivo fuente sin almacenar innecesariamente datos duplicados.

El importador nunca debe dar por válida una rebaja sólo porque exista texto en Observaciones: debe asociarla a una autorización estructurada o dejarla como observación pendiente de validación.

## 9. Integración con ceremonias

Para una solicitud de ceremonia:

- la API de elegibilidad consulta la regularidad de Gran Tesorería vigente;
- si el cuadro requerido está conciliado y el snapshot es habilitante, el requisito queda satisfecho desde el dato institucional;
- no se exige cargar nuevamente el cuadro o comprobante en Ceremonias;
- el snapshot y la referencia de conciliación quedan congelados en la evidencia de autorización.

Esto mantiene a Gran Tesorería como fuente de verdad y elimina duplicación documental.

## 10. Seguridad y privacidad

- RUT y datos de pagador: datos personales con acceso restringido.
- Montos individuales y ajustes: información financiera/institucional restringida.
- Reportes transversales deben exponer agregados cuando el detalle individual no sea necesario.
- Auditoría para envío, observación, conciliación, cierre, rectificación y generación de regularidad.
- Archivos/comprobantes en almacenamiento privado S3/MinIO, nunca como blobs en PostgreSQL.
- Retención y eliminación conforme a política institucional y Ley 21.719.

## 11. Criterios de aceptación del incremento

- importar/recrear el Cuadro de Pagos sin depender del RUT como PK;
- calcular automáticamente totales y diferencia;
- soportar múltiples pagos parciales y ambos métodos observados en la plantilla;
- estructurar ajustes/rebajas con referencia autorizante;
- impedir conciliación con diferencia distinta de cero u observaciones abiertas, salvo excepción formal modelada;
- emitir `FinancialRegularitySnapshot` desde un cuadro conciliado/cerrado;
- Ceremonias reutiliza esa regularidad sin pedir evidencia duplicada;
- auditoría y permisos cubiertos por pruebas;
- migración, PostgreSQL, API y pruebas integrales verdes antes de integrar a `dev`.

## 12. Estado de implementación posterior a RC1

La rama `feature/treasury-payment-table-v1`, aislada del candidato UAT, implementa:

- creación, carga manual, envío, consulta y conciliación del cuadro mensual;
- generación automática de líneas desde pertenencias vigentes a la fecha de corte;
- selección del último grado conocido y del cargo vigente para cada miembro;
- aplicación de un ajuste activo y autorizado por persona;
- bloqueo por grado faltante, ajustes activos simultáneos, cuota resultante negativa, diferencia de conciliación o identidad pendiente;
- pagos parciales por transferencia y depósito;
- emisión de regularidad y auditoría al conciliar;
- pruebas unitarias e integración HTTP/PostgreSQL.

Las cuotas base por grado se suministran al generar el cuadro. No se consideran una regla permanente hasta que exista un tarifario institucional versionado y aprobado.
