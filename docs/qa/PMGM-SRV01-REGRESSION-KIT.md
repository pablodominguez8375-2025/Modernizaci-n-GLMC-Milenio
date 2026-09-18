# Proyecto Centenario — Kit de regresión QA para srv01

## Propósito

Este kit acompaña el instalable QA actual y permite ejecutar una regresión trazable sobre el **SHA exacto instalado en srv01**.

No reemplaza la UAT institucional formal ni la aprobación del Sponsor/Product Owner. El plan histórico de 20 casos de `v1.0.0-rc1` se conserva intacto. El kit suma controles posteriores para **Consejo de Administración** y para el **flujo reglamentario integral de insinuaciones**.

## Matriz

La plantilla `release/PMGM-QA-SRV01-REGRESSION.template.json` contiene 22 controles:

- QA-001 a QA-020: cobertura equivalente a los 20 casos base históricos;
- QA-021: Consejo de Administración por cargo, Taller y fecha.
- QA-022: flujo reglamentario de insinuaciones desde presentación en 1.er grado hasta solicitud de Iniciación.

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

El cierre exige 22/22 `pass`, evidencia en cada control y `result.decision=pass`.

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

La regresión QA sirve para detectar defectos antes de solicitar aceptación institucional. Aun con 22/22 pass:

- no promueve automáticamente a `main`;
- no convierte QA en producción;
- no autoriza datos personales reales;
- no sustituye la aprobación expresa del Sponsor/Product Owner.
