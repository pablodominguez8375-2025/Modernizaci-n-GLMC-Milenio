# Proyecto Centenario — Kit de regresión QA para srv01

## Propósito

Este kit acompaña el instalable QA actual y permite ejecutar una regresión trazable sobre el **SHA exacto instalado en srv01**.

No reemplaza la UAT institucional formal ni la aprobación del Sponsor/Product Owner. El plan histórico de 20 casos de `v1.0.0-rc1` se conserva intacto. El kit suma controles posteriores para **Consejo de Administración** y para el **flujo reglamentario integral de insinuaciones**.

## Matriz

La plantilla `release/PMGM-QA-SRV01-REGRESSION.template.json` contiene 26 controles:

- QA-001 a QA-020: cobertura equivalente a los 20 casos base históricos;
- QA-021: Consejo de Administración por cargo, Taller y fecha.
- QA-022: flujo reglamentario de insinuaciones desde presentación en 1.er grado hasta solicitud de Iniciación.
- QA-023: cierre documental de Tenidas regulares y ceremoniales, distinguiendo Realizada de Cerrada.
- QA-024: Cuadro Mensual de Tesorería con segregación Taller / Gran Tesorería, cuadre previo y conciliación institucional.
- QA-025: Hospitalaria integral: Tronco independiente, socorros autorizados, revisión mensual del Consejo y rendición agregada a Gran Hospitalaria.
- QA-026: Afiliación/Incorporación 2026: clasificación estándar/reintegro/traslado, comisión del art. 2.5, dispensa sólo para traslado, cierre documental y materialización idempotente.

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

Ejemplo de Admisiones 2026 aprobado:

```bash
python3 scripts/record-srv01-regression-result.py \
  evidence/PMGM-QA-srv01-<sha>.json \
  QA-026 pass \
  --evidence "audit-event:admission-information-commission-demo" \
  --evidence "audit-event:admission-ceremony-completed-demo" \
  --evidence "captura-controlada:qa-admisiones-cierre-documental"
```

El control QA-026 debe comprobar, siempre con datos ficticios: clasificación explícita `standard/reentry/transfer` separada de `simple/activation`; comisión de tres Maestros para reintegro e incorporación; para traslado, comisión salvo dispensa expresa de Cámara del Medio con acta y fecha; conclusión vinculada al último grupo nombrado y anterior a la decisión de 3.er grado; balotaje posterior en 1.er grado con recuento agregado sin voto individual; Plancha de Gran Secretaría y Extracto de Acta adjuntos a una Tenida ceremonial del mismo Taller/tipo/fecha; Tenida cerrada antes de materializar; creación de miembro/pertenencia exactamente una vez; reintento sin duplicados; y actor real conservado en auditoría/reconciliación.

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

El cierre exige 26/26 `pass`, evidencia en cada control y `result.decision=pass`.

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

La regresión QA sirve para detectar defectos antes de solicitar aceptación institucional. Aun con 25/25 pass:

- no promueve automáticamente a `main`;
- no convierte QA en producción;
- no autoriza datos personales reales;
- no sustituye la aprobación expresa del Sponsor/Product Owner.
