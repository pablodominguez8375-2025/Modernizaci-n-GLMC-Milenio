# Proyecto Centenario — Kit de regresión QA para srv01

## Propósito

Este kit acompaña el instalable QA actual y permite ejecutar una regresión trazable sobre el **SHA exacto instalado en srv01**.

No reemplaza la UAT institucional formal ni la aprobación del Sponsor/Product Owner. El plan histórico de 20 casos de `v1.0.0-rc1` se conserva intacto. El kit suma controles posteriores para **Consejo de Administración** y para el **flujo reglamentario integral de insinuaciones**.

## Matriz

La plantilla `release/PMGM-QA-SRV01-REGRESSION.template.json` contiene 36 controles:

- QA-001 a QA-020: cobertura equivalente a los 20 casos base históricos;
- QA-021: Consejo de Administración por cargo, Taller y fecha.
- QA-022: flujo reglamentario de insinuaciones desde presentación en 1.er grado hasta solicitud de Iniciación.
- QA-023: cierre documental de Tenidas regulares y ceremoniales, distinguiendo Realizada de Cerrada.
- QA-024: Cuadro Mensual de Tesorería con segregación Taller / Gran Tesorería, cuadre previo y conciliación institucional.
- QA-025: Hospitalaria integral: Tronco independiente, socorros autorizados, revisión mensual del Consejo y rendición agregada a Gran Hospitalaria.
- QA-026: Afiliación/Incorporación 2026, decisiones reglamentarias, permisos y materialización.
- QA-027: cartola personal en Mi ficha y control de duplicidad de pagos del Taller.
- QA-028: Cuadro Logial Mensual agregado por tipo de cuota y detalle individual minimizado para Gran Tesorería.
- QA-029: navegación de Tesorería por cargo.
- QA-030: navegación de Secretaría por cargo.
- QA-031: segregación Tesorero / Secretaría.
- QA-032: insinuados publicados y fotografía protegida.
- QA-033: logotipo oficial e identidad institucional.
- QA-034: reposición por fallecimiento, conciliación de transferencia y cuota de cónyuge.
- QA-035: tarifas del Decreto 1.759, clasificación por Oriente y separación del aporte institucional frente a la cuota del Taller.
- QA-036: nómina del Cuadro de Tesorería alineada a `CUADRO PAGO GRAN TESORERÍA.xlsx`, planchas para cuotas especiales y detalle personal protegido.

Todos parten en `pending`. Ningún control cambia automáticamente a `pass`.

## Preparación

Después de instalar el ZIP QA y completar el smoke:

```bash
bash scripts/prepare-srv01-regression.sh
```

El script toma:

- `SOURCE_SHA` desde `BUILD-INFO.txt`;
- la URL QA desde `/etc/pmgm/srv01.env`;
- timestamp UTC;
- la matriz versionada del mismo paquete.

Genera un archivo bajo `evidence/` ligado al SHA exacto.

## Registro de resultados

Ejemplo de control aprobado:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-021 pass \
  --evidence "audit-event:consejo-sesion-ficticia" \
  --evidence "captura-controlada:qa-consejo-venerable"
```

Ejemplo de flujo de insinuaciones aprobado:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-022 pass \
  --evidence "audit-event:insinuacion-deliberacion-demo" \
  --evidence "audit-event:insinuacion-balotaje-demo" \
  --evidence "captura-controlada:qa-insinuacion-flujo-completo"
```

El control QA-022 debe comprobar, con datos ficticios: fecha de presentación en 1.er grado; bloqueo antes de 7 días; deliberación unánime; publicación; al menos tres entrevistas privadas; revisión de 3.er grado; cumplimiento del plazo de publicación; balotaje; y solicitud formal de Iniciación.

Ejemplo de cierre documental aprobado:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-023 pass \
  --evidence "audit-event:tenida-regular-cerrada-demo" \
  --evidence "audit-event:tenida-ceremonial-cerrada-demo" \
  --evidence "captura-controlada:qa-cierre-documental-tenidas"
```

El control QA-023 debe comprobar, con datos ficticios: que una Tenida regular Realizada no cierre sin Extracto de Acta y sí cierre con Extracto; que una Tenida ceremonial no cierre con sólo uno de sus documentos y requiera simultáneamente Extracto de Acta y Plancha de Autorización de Gran Secretaría; que la Plancha corresponda al Taller/tipo de ceremonia; y que el cierre deje trazabilidad auditable.

Ejemplo de Cuadro Mensual de Tesorería aprobado:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-024 pass \
  --evidence "audit-event:treasury-statement-submitted-demo" \
  --evidence "audit-event:treasury-statement-reconciled-demo" \
  --evidence "captura-controlada:qa-cuadro-mensual-tesoreria"
```

El control QA-024 debe comprobar, con datos ficticios: que el perfil Tesorero del Taller crea el Cuadro sólo para su Taller; genera la nómina desde el Cuadro vigente; toda rebaja conserva referencia de autorización; registra transferencia/depósito antes del envío; el envío se bloquea con Diferencia distinta de 0 o identidades pendientes; el Cuadro enviado queda congelado para pagos; y sólo Gran Tesorería ejecuta la conciliación institucional que actualiza la regularidad financiera.

Ejemplo de Hospitalaria integral aprobada:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-025 pass \
  --evidence "audit-event:hospitalaria-socorro-autorizado-demo" \
  --evidence "audit-event:hospitalaria-rendicion-conciliada-demo" \
  --evidence "captura-controlada:qa-hospitalaria-privacidad"
```

El control QA-025 debe comprobar, con datos ficticios: independencia del Tronco de Beneficencia respecto de Tesorería; aportes por Hospitalaria; socorros con respaldo; autorización válida por Venerable Maestro o acuerdo auditable del Consejo; estado mensual revisado por Consejo; rendición agregada hacia Gran Hospitalaria sin beneficiarios, destinos ni observaciones privadas; reposición/comprobante; conciliación institucional; y actualización de la regularidad que consume Ceremonias.

### QA-027 — Mi ficha / cartola personal de Tesorería

1. Ingresar con un Hermano vinculado a una ficha institucional.
2. Abrir `Mi ficha` y desplegar `Ver cartola` en el estado de Tesorería.
3. Verificar totales cargado, pagado y saldo, sin mostrar información de otros hermanos.
4. Confirmar que cada fila distingue el período de la cuota de la fecha efectiva del pago.
5. Confirmar que cada pago presenta su comprobante correlativo y monto.
6. Desde Tesorería del Taller, intentar repetir un pago con igual cargo, fecha, monto, medio y referencia: la API debe rechazarlo sin alterar el saldo.
7. Repetir un abono legítimo con referencia distinta: debe registrarse respetando el saldo pendiente.

Resultado esperado: el Hermano consulta únicamente su cartola; Tesorería conserva la fecha real de caja y el período de la obligación; un reintento idéntico con referencia no duplica el ingreso.

### QA-028 — Gran Tesorería / Cuadro Logial Mensual

1. Preparar un Taller ficticio con cuotas normal, tercera edad, estudiante, cónyuge y Past Activo.
2. Generar el Cuadro Logial Mensual desde los miembros activos y las cuotas vigentes del período.
3. Ingresar como Gran Tesorero y verificar que la vista inicial muestre sólo tipo de cuota, cantidad de miembros, monto por línea y total mensual.
4. Confirmar que identificadores, nombres, cargos, grados y referencias individuales no aparezcan inicialmente.
5. Usar `Consultar datos mínimos` sólo para revisar una diferencia y comprobar que se presenta el detalle restringido.
6. Registrar un pago inferior al total: la conciliación debe quedar bloqueada y el Taller no puede marcarse al día.
7. Completar el pago íntegro: la diferencia debe quedar en cero y Gran Tesorería puede conciliar.
8. Confirmar que la conciliación genera la regularidad institucional del Taller consumida por los demás circuitos.

Resultado esperado: Gran Tesorería valida el pago íntegro contra el total calculado del mes sin recibir innecesariamente la nómina individual completa.

### QA-029 — Navegación de Tesorería por cargo

1. Ingresar como Tesorero del Taller y confirmar que existe un único acceso lateral `Tesorería`.
2. Abrirlo y comprobar las opciones internas `Resumen`, `Cuotas y cobranza`, `Egresos` y `Cuadro mensual`.
3. Verificar que las operaciones anteriores siguen disponibles dentro de la opción correspondiente.
4. Ingresar como Venerable Maestro y confirmar que sólo ve `Egresos por autorizar`, sin formularios para cuotas, cobranza o Cuadro mensual.
5. Ingresar como Gran Tesorero y confirmar que existe un único acceso lateral `Gran Tesorería`.
6. Abrirlo y comprobar las opciones `Cuadros mensuales` y `Estado de Talleres`.
7. Confirmar que Gran Tesorería no recibe acciones para administrar caja, cobranza o egresos locales.
8. Repetir la navegación en ancho móvil y confirmar que todas las opciones autorizadas siguen siendo legibles y operables.

Resultado esperado: cada cargo encuentra intuitivamente todas sus tareas financieras en un único menú propio, sin duplicidad ni ampliación de permisos.

Ejemplo de fallo:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-009 fail \
  --evidence "ticket:QA-ceremonia-001" \
  --notes "La ceremonia fue autorizada pese a regularidad pendiente."
```

## Gate

Durante la ejecución parcial:

```bash
python3 tests/qa_srv01_regression_gate.py evidence/PMGM-QA-srv01-<sha>.json --allow-pending
```

Para cerrar la regresión:

```bash
python3 tests/qa_srv01_regression_gate.py evidence/PMGM-QA-srv01-<sha>.json
```

El cierre exige 36/36 `pass`, evidencia en cada control y `result.decision=pass`.

## Política de evidencia

Se permiten referencias minimizadas:

- `ticket:...`;
- `audit-event:...`;
- `captura-controlada:...`;
- `backup-report:...`;
- `test-data:dataset-ficticio-...`.

No se guardan en Git:

- contraseñas;
- tokens;
- encabezados Authorization;
- secretos;
- RUT/RUN reales;
- correo/teléfono reales;
- documentos personales reales;
- respaldos reales.

## Relación con UAT formal

La regresión QA sirve para detectar defectos antes de solicitar aceptación institucional. Aun con 36/36 pass:

- no promueve automáticamente a `main`;
- no convierte QA en producción;
- no autoriza datos personales reales;
- no sustituye la aprobación expresa del Sponsor/Product Owner.
