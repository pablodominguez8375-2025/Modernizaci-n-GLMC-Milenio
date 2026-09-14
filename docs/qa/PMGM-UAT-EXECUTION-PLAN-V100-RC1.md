# PMGM UAT — Plan operativo de ejecución v1.0.0-rc1

## 1. Candidato congelado

La aceptación institucional de `v1.0.0-rc1` se ejecutará exclusivamente contra:

- SHA: `739ba0b3a8d89087177b1edabd61c2981eed08a2`
- Rama congelada: `uat/v1.0.0-rc1-739ba0b3`
- Entorno objetivo de prueba: `pilot-operational`
- Documento base: `docs/qa/PMGM-UAT-V100-RC1.md`
- Evidencia procesable: copia de trabajo de `release/PMGM-UAT-1.0.0-rc1.template.json`

Cualquier cambio del código bajo prueba invalida la correspondencia directa con la evidencia reunida y obliga a evaluar una nueva RC o una regresión formal.

## 2. Regla de ejecución

La UAT no se considera aprobada por una CI verde. La CI valida reproducibilidad técnica; la UAT valida que los flujos institucionales satisfacen el comportamiento esperado.

Antes de comenzar:

1. desplegar exactamente la rama congelada o el SHA indicado;
2. comprobar `HTTPS`, OIDC/PKCE, health checks y `/api/system/info`;
3. verificar que la versión informada sea `1.0.0-rc1`;
4. ejecutar y verificar un respaldo previo;
5. confirmar que los datos utilizados sean ficticios o expresamente autorizados para pruebas;
6. registrar URL HTTPS, SHA y timestamp de la sesión UAT;
7. no almacenar secretos, tokens ni datos personales innecesarios en Git.

## 3. Olas de aceptación

### Ola A — Plataforma, identidad y membresía

Casos: `UAT-001` a `UAT-005`.

- ingreso OIDC/PKCE y cierre de sesión;
- segregación Superadmin / Gran Logia / Taller;
- bootstrap dry-run/apply controlado e idempotente;
- ficha de miembro y grado actual;
- traslado entre Talleres preservando historial y grado.

Evidencia mínima recomendada:

- referencia de captura controlada del login y logout;
- respuesta 403/permitida según perfil sin exponer token;
- resultado del dry-run y del segundo apply sin duplicación;
- referencia de ficha de miembro ficticio;
- referencia de historial antes/después del traslado.

### Ola B — Régimen Interior, regularidad y ceremonias

Casos: `UAT-006` a `UAT-010`.

- reportería ejecutiva Orden/Taller;
- calidad de datos y corroboración;
- regularidad Gran Tesorería / Gran Hospitalaria;
- bloqueo de ceremonia por requisito pendiente;
- autorización de ceremonia cuando todos los requisitos cumplen.

Evidencia mínima recomendada:

- reporte agregado con datos ficticios;
- código de caso de calidad de datos y resolución;
- referencia de estados de regularidad utilizados;
- referencia de intento bloqueado;
- referencia de autorización final y evento de auditoría.

### Ola C — Insinuados, Gran Secretaría, calendario y Gestión Logial

Casos: `UAT-011` a `UAT-014`.

- plazo mínimo configurable de publicación de insinuados;
- minimización de datos visibles en portal;
- reserva de templo/sala y prevención de conflicto;
- proyección del calendario con masking `Ocupado`;
- Tenida, acta y docencia limitadas al Taller correspondiente.

Evidencia mínima recomendada:

- referencia de fecha de publicación y fecha habilitante;
- captura controlada del portal minimizado;
- referencia de reserva válida y segundo intento conflictivo;
- captura de evento protegido mostrando `Ocupado`;
- referencia de operación autorizada y denegación fuera de alcance.

### Ola D — Documentos, Biblioteca, Gran Archivero y Notificaciones

Casos: `UAT-015` a `UAT-019`.

- carga/versionado documental seguro;
- rechazo de archivo riesgoso o inválido;
- Biblioteca Virtual por grado/permisos;
- planchas de trabajo sólo en Biblioteca Virtual;
- Gran Archivero restringido y separado de planchas;
- notificaciones, lectura y obligatoriedad.

Evidencia mínima recomendada:

- referencia de nueva versión documental y hash persistido;
- referencia de rechazo antimalware/política sin almacenar el archivo riesgoso en Git;
- comparación de catálogo para dos perfiles/grados;
- referencia que demuestre exclusión de planchas del Gran Archivero;
- referencia de acceso autorizado/denegado al Gran Archivero;
- referencia de notificación recibida y marcada como leída.

### Ola E — Recuperación y cierre

Caso: `UAT-020` más validación integral de evidencia.

- backup verificable del piloto;
- restauración controlada;
- smoke posterior a la restauración;
- ejecución de `tests/uat_evidence_gate.py` sobre la evidencia final;
- revisión 20/20 `pass`;
- decisión final del Sponsor/Product Owner.

Evidencia mínima recomendada:

- identificador del backup;
- reporte de verificación;
- referencia de restore exitoso;
- resultado del smoke post-recovery;
- salida exitosa del gate UAT;
- registro formal de aprobación o rechazo.

## 4. Tratamiento de evidencia

La evidencia debe ser suficiente para probar el resultado y mínima desde el punto de vista de protección de datos.

Permitido como referencia en el JSON de evidencia:

- `ticket:...`;
- `captura-controlada:...`;
- `audit-event:...` con identificadores minimizados;
- `backup-report:...`;
- `test-data:dataset-ficticio-...`.

No se debe registrar en Git:

- contraseñas;
- access/refresh tokens;
- llaves privadas;
- client secrets;
- encabezados `Authorization`;
- RUT, correo, teléfono u otros datos personales reales usados en una captura;
- archivos de respaldo;
- documentos institucionales reales o evidencia documental sensible.

Cuando una captura o respaldo contenga información sensible, Git sólo debe conservar la referencia controlada a su ubicación autorizada.

## 5. Criterio por caso

Un caso puede quedar en `pass` únicamente cuando:

- se ejecutó sobre el SHA congelado;
- el resultado observado coincide con `expected`;
- existe al menos una referencia de evidencia;
- la evidencia no contiene secretos ni datos personales innecesarios.

Un caso queda en `fail` cuando el comportamiento funcional, de autorización, privacidad o recuperación difiere del esperado. Todo `fail` debe abrir un defecto separado y bloquear la aprobación final.

`not_applicable` no es válido para ninguno de los 20 casos base de esta RC.

## 6. Manejo de defectos

Ante un fallo UAT:

1. registrar el caso como `fail`;
2. abrir issue de defecto con descripción reproducible y sin datos sensibles;
3. corregir en rama separada;
4. ejecutar CI completa;
5. generar un nuevo SHA candidato;
6. desplegar ese nuevo SHA;
7. repetir al menos el caso fallido y su regresión asociada;
8. para la primera `v1.0.0`, repetir los 20 casos si cambia el SHA candidato.

No se debe modificar la rama `uat/v1.0.0-rc1-739ba0b3` para incorporar una corrección.

## 7. Gate final de v1.0.0

No se autoriza promoción a `main` ni publicación de `v1.0.0` estable hasta cumplir simultáneamente:

- CI verde sobre el mismo SHA candidato;
- UAT `20/20 pass`;
- evidencia válida para todos los casos;
- dominio/TLS del entorno objetivo validados;
- secretos operacionales fuera de Git;
- bootstrap institucional ejecutado con dry-run previo;
- backup/restore verificado en el entorno objetivo;
- revisión legal/privacidad previa a datos personales reales;
- aprobación expresa del Sponsor/Product Owner.

## 8. Límites funcionales ratificados

- CENDOC no forma parte del Proyecto Milenio.
- Biblioteca Virtual y Gran Archivero son módulos distintos.
- Las planchas de trabajo pueden formar parte de la Biblioteca Virtual según grado/permisos, pero no se incorporan al Gran Archivero.
- La UAT no autoriza por sí misma el uso productivo de datos personales reales.
