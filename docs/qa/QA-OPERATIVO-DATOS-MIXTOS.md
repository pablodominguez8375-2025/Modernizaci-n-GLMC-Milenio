# QA operativo — datos ficticios y registros reales controlados

**Decisión PO 23-10-2026.** Destino: `glmdch`, `https://sistema.granlogiamixta.cl`. Issue #678; PR #679. Este documento modifica la política de pruebas, no datos ni infraestructura por sí mismo.

## Objetivo de producto

QA es una instalación **funcional**, con API, PostgreSQL, Keycloak y módulos institucionales, para que usuarios autorizados puedan ingresar, consultar, modificar, corregir, buscar, imprimir y procesar casos representativos, incluidos casos reales voluntarios y controlados. Detectar errores de reglas, interfaces, permisos, persistencia, importación y cargas de datos.

No se limita a demostraciones estáticas ni exclusivamente a datos de fantasía. No equivale a producción. La demo pública de GitHub Pages sigue **únicamente ficticia**.

## Política de datos, sin burocracia artificial

1. **Sintéticos:** generados, repetibles y marcados `SYNTHETIC_ONLY`. Pueden poblar QA para regresión, pruebas destructivas y volúmenes representativos.
2. **Datos institucionales auténticos:** decretos, talleres, categorías, cargos, calendarios, cuotas y parámetros entregados por la fuente oficial, con vigencia, trazabilidad e identificación de procedencia.
3. **Registros personales reales controlados:** proporcionados voluntariamente y autorizados por el responsable para pruebas. **No implementar una casilla de consentimiento por cada registro ni impedir su carga por ausencia de ese campo QA.** Mantener accesos autorizados, propósito de prueba, cifrado, segregación, control de destinatarios, auditoría, retención y borrado. Voluntariedad no debe confundirse con ausencia de obligaciones legales; la base de licitud y el resguardo de la autorización corresponden al responsable institucional y no se suben documentos personales al repositorio.

La carga y edición real se hacen **mediante las interfaces y API autenticadas del producto**, en el QA restringido, no mediante volcados anónimos sobre tablas productivas ni capturas con información privada en las evidencias públicas. No se exige consentimiento ceremonial/documental innecesario: se documenta la habilitación del dataset y su responsable de forma interna sin publicar identidades.

**Restricción técnica preexistente:** Silo/MinIO de #470/#479 fue autorizado sólo para datos sintéticos. **No cargar adjuntos personales reales en ese objeto** hasta habilitar almacenamiento real protegido o cambiar de forma controlada la autorización de seguridad de esa capa; se pueden probar adjuntos ficticios de inmediato. Evitar importaciones de base de datos completa, emails/SMS reales automáticos y cobros de dinero reales.

## Estado comprobado, no extrapolado

- Último HEAD de `dev` al inicio: `84ddb69fe2d9b7500fcf2e1021e75d793ee61fcd`; `main` estable en `6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- Última API+WEB QA verificadas físicamente según #525: `33c805ec260e7cb80413647cf094067438711a7d`. No asumir código nuevo desplegado.
- Existe `FirstImplementationSeedService`, `QaIdentitySeedService` (21 identidades `qa.*` y 20 vínculos sintéticos), `PMGM_QA_DB2` y G8; se preservan.
- El nuevo generador `scripts/qa/qa_operational_fixtures.py` entrega un JSON sintético con entidades y referencias válidas que **no coincide automáticamente con EF Core ni se importa al servidor**. La herramienta declara `NOT_IMPORTED`. No sustituye un importador, no escribe a BD y nunca crea cuentas reales.

## Primera ejecución local, no en servidor

```bash
python3 scripts/qa/qa_operational_fixtures.py --members 120 --workshops 4 --months 12
python3 -m unittest discover -s tests -p test_qa_operational_fixtures.py -v
# Solo cuando se necesite archivo: ruta local NUEVA (0600), no subir a GitHub
python3 scripts/qa/qa_operational_fixtures.py --members 120 --workshops 4 --months 12 --output /ruta/privada/qa-fixtures.json
```

La vista previa imprime conteos y SHA-256, no contraseñas, nombres reales ni otros datos de personas.

## Casos institucionales obligatorios

| Módulo | Caso representativo |
| --- | --- |
| Identidades y perfiles | Admin técnico separado, cargos vigentes/vencidos, permisos por Taller, grados y acciones |
| Fichas y Talleres | Alta/corrección real controlada; miembros activos e inactivos; prueba entre Talleres |
| Secretaría | Tenidas, asistencia, actas, firmas y gestión documental |
| Tesorería | Caja por fecha de recepción separada de cuota imputada, abonos anteriores/futuros, mora y multiperíodos |
| Hospitalaria | Reposición, cobros, categorías de hermanos y Past Activos |
| Insinuados y ceremonias | Publicaciones, entrevistas, plazos, autorizaciones, rechazos y expediente |
| Afiliación | Carta de retiro, simple ≤3 meses y activación posterior, continuidad histórica |
| Gran Asamblea | Categoría de asambleísta, impedimentos, estado pendiente, padrón sólo con certificación oficial |
| Infraestructura | Logout/login, SMTP solo buzón controlado, errores 4xx, backup/restore, exportaciones y carga masiva |

## Implementación pendiente (no afirmar éxito antes de ejecutarla)

1. Probar código y fusionar PR #679 a `dev` sólo con gates exactos 3/3 y anti-conflictos.
2. Construir un **importador administrativo real y seguro** para la estructura del producto: vista previa/dry-run, archivo privado, lote versionado, mapeo válido, validación de datos, resolución de duplicados, transacción, idempotencia y reversión. Diferenciar importación sintética vs real. No dar permisos de escritura directos en PostgreSQL a operador de QA.
3. Enlazar tablas/usuarios G8 existentes, completar campos y casos de UAT; guardar evidencia sin valores reales.
4. Conectividad/almacenamiento/respaldos acorde con datos reales; rol permitido y perfiles aprobados.
5. Desplegar y poblar QA mediante canal operativo autorizado; verificar conteos, escrituras, lectura por perfiles, restauración y 26/26 controles. Recién ahí reportar datos **CARGADOS**.

**Segregación y reversibilidad:** cada registro de prueba debe tener procedencia y lote asociable; excluirlos de avisos y reportes institucionales reales, y permitir limpieza de QA por lote sin borrar fuentes ni datos ajenos. Nunca enlazar persona con voto secreto.

## Continuidad de control

GitHub #678 y PR #679; QA #97, automatización #525, seguridad #470/#471/#479, accesos #523, Asamblea #533, SMTP #669. Mantener documentación Drive «Proyecto Centenario» sin datos personales, anexos ni contraseñas. No promoción `main`, no despliegue de datos reales a `glmdch` por un archivo generado.
