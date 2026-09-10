# PMGM UAT — v1.0.0-rc1

**Objetivo:** ejecutar una aceptación institucional trazable antes de promover Proyecto Milenio a v1.0 estable.

**Plantilla:** `release/PMGM-UAT-1.0.0-rc1.template.json`  
**Validador:** `tests/uat_evidence_gate.py`  
**Release Candidate:** `1.0.0-rc1`

## Regla principal

Una CI verde demuestra que el software es técnicamente reproducible. La UAT demuestra que los flujos institucionales se comportan como la organización espera. Son controles diferentes y ambos son obligatorios antes de una versión estable.

La UAT debe ejecutarse contra un único SHA candidato claramente identificado. Si el código cambia durante la validación, la evidencia anterior no aprueba automáticamente el nuevo SHA.

## Preparación

1. Desplegar la RC en el entorno `pilot-operational` mediante el procedimiento oficial.
2. Confirmar que `/api/system/info` informa `1.0.0-rc1`.
3. Confirmar HTTPS, OIDC, health checks y smoke piloto.
4. Ejecutar y verificar backup antes de comenzar cambios de UAT.
5. Utilizar datos ficticios o datos expresamente autorizados para prueba.
6. Copiar `release/PMGM-UAT-1.0.0-rc1.template.json` a un archivo de trabajo de evidencia.
7. Registrar el SHA exacto, URL HTTPS del entorno y timestamp de ejecución.

Si la evidencia incluye capturas con nombres, referencias internas u otra información sensible, **no debe subirse al repositorio**. El JSON puede registrar una referencia controlada a la evidencia, por ejemplo un código de caso, ticket interno o ubicación del repositorio documental autorizado, sin insertar secretos ni datos personales innecesarios.

## Estados permitidos

- `pending`: aún no ejecutado o no concluido.
- `pass`: resultado esperado confirmado y con al menos una referencia de evidencia.
- `fail`: el comportamiento no cumple; `notes` debe explicar el hallazgo.
- `not_applicable`: reconocido por el formato, pero no válido para aprobar ninguno de los 20 casos base obligatorios de esta RC.

## Casos obligatorios

| ID | Área | Verificación |
| --- | --- | --- |
| UAT-001 | Identidad | Ingreso OIDC/PKCE y cierre de sesión. |
| UAT-002 | Autorización | Segregación Superadmin / Gran Logia / Taller. |
| UAT-003 | Bootstrap | Dry-run y apply sólo para Superadmin, sin duplicación. |
| UAT-004 | Membresía | Ficha de miembro y grado actual. |
| UAT-005 | Membresía | Traslado preserva historial y grado. |
| UAT-006 | Régimen Interior | Reportería por Orden/Taller. |
| UAT-007 | Calidad de datos | Detección, asignación y resolución de caso. |
| UAT-008 | Regularidad | Tesorería/Hospitalaria alimentan elegibilidad. |
| UAT-009 | Ceremonias | Bloqueo cuando falta un requisito. |
| UAT-010 | Ceremonias | Autorización cuando todos los requisitos cumplen. |
| UAT-011 | Insinuados | Plazo mínimo y minimización del portal. |
| UAT-012 | Gran Secretaría | Reserva de espacio y control de conflictos. |
| UAT-013 | Calendario | Proyección y masking `Ocupado`. |
| UAT-014 | Gestión Logial | Tenida, acta y docencia limitadas al Taller. |
| UAT-015 | Documentos | Carga/versionado seguro. |
| UAT-016 | Documentos | Rechazo de archivo riesgoso o inválido. |
| UAT-017 | Biblioteca | Acceso por grado/permisos y planchas sólo en Biblioteca. |
| UAT-018 | Gran Archivero | Acceso restringido y exclusión de planchas. |
| UAT-019 | Notificaciones | Recepción, lectura y obligatoriedad. |
| UAT-020 | Recuperación | Backup, restore y smoke posterior. |

El detalle del resultado esperado está dentro de la plantilla JSON para evitar discrepancias entre el checklist humano y la evidencia procesable.

## Evidencia recomendada

La evidencia debe ser suficiente para demostrar el resultado sin recopilar datos innecesarios. Ejemplos:

- `ticket:UAT-005-2026-001`;
- `captura-controlada:UAT-013-02`;
- `audit-event:correlation-id-redacted`;
- `backup-report:pilot-2026-09-10`;
- `test-data:dataset-ficticio-uat-v1`.

No registrar contraseñas, tokens, secretos de clientes, llaves privadas ni encabezados `Authorization`.

## Uso del gate

Validar la plantilla versionada, permitiendo pendientes:

```bash
python3 tests/uat_evidence_gate.py release/PMGM-UAT-1.0.0-rc1.template.json --allow-pending
```

Validar una evidencia que pretende aprobar la RC:

```bash
python3 tests/uat_evidence_gate.py /ruta/segura/PMGM-UAT-1.0.0-rc1-evidence.json
```

Para que la segunda orden sea exitosa se requiere:

- SHA Git real de 40 caracteres;
- URL HTTPS sin credenciales;
- fecha de ejecución con zona horaria;
- los 20 casos en `pass`;
- al menos una referencia de evidencia por caso;
- cero casos `fail`, `pending` o `not_applicable`;
- `approval.decision=approved`;
- rol aprobador `Sponsor/Product Owner`;
- fecha formal de aprobación.

El gate valida completitud y consistencia; no sustituye el juicio del aprobador sobre la calidad de la evidencia.

## Hallazgos

Si un caso falla:

1. registrar `status=fail` y describir el hallazgo en `notes`;
2. vincular el defecto a issue/backlog sin incluir datos sensibles;
3. corregir en una rama separada;
4. ejecutar CI completa sobre el nuevo SHA;
5. desplegar el nuevo candidato;
6. repetir el caso fallido y cualquier prueba de regresión afectada;
7. actualizar el SHA de la ejecución sólo cuando la UAT corresponda íntegramente al candidato evaluado.

Un cambio posterior al SHA aprobado invalida la promoción directa y exige una evaluación de regresión proporcional; para v1.0 inicial se recomienda repetir los 20 casos base.

## Aprobación

La decisión final corresponde al Sponsor/Product Owner dentro del proceso institucional. La aprobación debe registrarse sólo después de confirmar que:

- la UAT es 20/20 `pass`;
- CI del mismo candidato está verde;
- dominio/TLS y secretos son los del entorno objetivo;
- backup/restauración fueron verificados;
- bootstrap fue aplicado de forma controlada;
- se completó la revisión de privacidad/legal previa a datos personales reales.

Sólo después de estas condiciones corresponde preparar la promoción a `v1.0.0` estable.
