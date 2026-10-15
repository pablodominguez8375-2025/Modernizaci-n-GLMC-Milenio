# PMGM-SEC-470 — recuperación lógica de objetos S3 en laboratorio Silo
**Corte: 15 de octubre de 2026 · Integración experimental, sin despliegue institucional.**

## Objetivo

Complementar el test de privilegios reales `tests/test_silo_real_iam.py` (PR #491) con una prueba **de recuperación lógica de un objeto sintético tras la pérdida total de la instancia S3**, confirmando SHA-256 y políticas IAM después de reconstruir un servidor nuevo.

**Esto NO es prueba de respaldo integral de la infraestructura Silo ni recuperación institucional.** No valida snapshots de volúmenes, versiones anteriores de objetos, retención legal/Object Lock, cifrado KMS, lifecycle, metadatos completos, políticas originales, logs de auditoría, secretos de producción ni los RPO/RTO de la GLMixta.

## Flujo automático reproducible (nuevo)

`tests/test_silo_real_recovery.py` se importa a `tests/test_pilot_keycloak_db_isolation.py` para que corra en el gate **Infrastructure configuration** existente de GitHub Actions. Sin tocar workflows reservados por el PR #479.

1. Descarga imagen **exacta** Silo 2026-10-15 `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62` (corrección SN-2026-015/016 declarada por proveedor).
2. Crea una red Docker efímera y el servidor **A** sin puertos publicados, sin volúmenes persistentes, con usuario administrador y usuario documental aleatorios. El root solo aprovisiona política estricta de lectura/escritura/borrado de objetos en `pmgm-documents` y ListBucket de ese bucket; existe segundo bucket prohibido.
3. Sube y descarga vía AWS S3 **SigV4 real** un objeto binario sintético (128 KiB), comprueba SHA-256 y guarda su contenido como respaldo lógico en un archivo temporal del runner.
4. **Destruye A** y confirma su ausencia mediante Docker inspect. Inicia **B**, totalmente independiente y con NUEVAS credenciales/root, reprovisiona buckets y política desde el contrato versionado.
5. Confirma que B no heredó el objeto antes de restauración (HEAD 404). Verifica SHA-256 del respaldo, restaura bytes mediante PUT y comprueba GET, HEAD y SHA-256 idénticos.
6. La credencial documental A **debe** ser inválida en B (403). La nueva credencial B debe operar solo en el bucket documental; GET/PUT/DELETE al bucket ajeno deben devolver 403 y acceso a `mcli admin info` debe fallar.
7. Destruye siempre ambos contenedores, network y archivo de respaldo al salir; no hay credenciales ni datos personales institucionales ni puertos externos. Falla CI si no hay Docker, no descarga imagen o falla cualquier paso de permisos/integridad.

La ejecución de la prueba y su resultado se acreditan **solo** cuando el gate de CI exact-HEAD indica SUCCESS; jamás sustituir con S3Mock ni modificar las aserciones para obviar un fallo. La comprobación no exige alterar ASP.NET, MinIO, Compose o Caddy.

## Condiciones de aceptación por Sponsor para infraestructura real

- Elegir formalmente proveedor S3 y revisar licenciamiento AGPLv3/condiciones institucionales, SBOM/attestation y avisos de seguridad (incluidas SN-2026-015/016).
- Validar con la aplicación .NET y credenciales mínimas, TLS/OIDC/antivirus; diseñar respaldos de volúmenes, metadata, configuración IAM y claves con almacenamiento fuera de sitio, cifrado y pruebas de restauración.
- Definir con Gran Secretaría/administración políticas de retención, inmutabilidad, RPO/RTO, destrucción de documentos, evidencias de Ley 21.719 y autorización explícita para entorno QA real.
- No modificar `main` ni desplegar `srv01` en pausa #97. PR #479 bloqueada hasta estos controles, aunque la recuperación sintética pase.

**Fuentes de continuidad:** Issue #470, PR #479, PR #491, nuevo PR de esta prueba y Línea Base Maestra Drive.