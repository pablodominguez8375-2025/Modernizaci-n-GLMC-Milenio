# QA525 — verificadores aislados para preparación firmada del corte vigente

**Estado:** PR #787 de alcance estrictamente seguro, extraído del borrador PR #771. Fecha 28-10-2026. Solo se integra a `dev` después de controles CI exactos. La fusión de código no instala ni activa operaciones en glmdch.

## Cadena de seguridad

1. `scripts/qa/operators/qa525-current-change-policy.py`: obtiene la release pública del HEAD actual usando el gate ya integrado en PR #783; comprueba que el commit de API que sirve como **referencia histórica** sea ancestro exacto y que la comparación GitHub no esté truncada. Si hubo cambios sensibles (identidad/cuentas, migraciones, `Program.cs`, capas Docker/Compose, configuración) devuelve `BLOCKED_REVIEW_REQUIRED`. Cualquier autorización posterior seguirá bloqueada hasta revisar la identidad y el runtime físico.
2. `scripts/qa/operators/qa525-current-host-preflight.py`: exige glmdch/root, ruta local confiable y SHA256 fijo del preflight previamente revisado. Solo tras superar la política de cambios consulta la línea base física con comprobaciones **sin escrituras**: API, WEB, Keycloak, PostgreSQL, MinIO, gateway y ClamAV, Compose, Master, SMTP, migraciones y salud. Vuelve a comprobar el corte de GitHub al finalizar; su estado máximo es `HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED`.
3. `scripts/qa/operators/qa525-current-host-staging.py`: únicamente si se aceptan ambos pasos, descarga la release sin token y verifica el ZIP mediante SHA256, `gh attestation verify` (identidad Sigstore del workflow y `source-digest` del commit), manifiesto y evidencia Silo usando el código existente de PR #785. Staging temporal privado, segundo preflight físico y solo entonces movimiento a una ruta nueva. Cualquier error deja el resultado en `BLOCKED` y descarta la carpeta temporal.

Los módulos se conservan **read-only o staging de código**: no hacen `docker build`, `docker compose up`, migraciones, reinicio de servicios, aplicaciones ni cambios de usuarios. No se habilitan timers. `may_build=false`, `may_apply=false` y `fresh_backup_created=false` hasta implementar y auditar una fase posterior. El staging no acredita UAT, licencia AGPL, recuperación de base ni despliegue físico.

## Evidencia concreta y bloqueo de seguridad

Al corte `dev@700d0e23f9cc98f83bc9d3cef4d55040a2dba446`, los cinco controles de publicación y el publicador del artefacto firmado terminaron `SUCCESS`. Su prerelease `qa-signed-700d0e23f9cc98f83bc9d3cef4d55040a2dba446` publica un ZIP cuyo SHA256 de metadata `0e60dee2951d6e5110fb69549bccb49a714b40a1524342ed2899d519b822a596` concuerda con el artefacto Silo del mismo corte. **No se descargó ni verificó aquí la firma criptográfica real.**

La comparación de la API instalada históricamente `6ff4ae0d092d4214df4ea31af484efbc969fde54` con ese `dev` muestra 99 archivos diferentes, incluidos `backend/src/PMGM.Api/Program.cs` y `backend/src/PMGM.Api/Modules/UserAccounts/`. Por lo tanto, ese corte exige revisión técnica, no staging físico desatendido. La referencia de instalación física debe validarse de nuevo en glmdch; un SHA en memoria o GitHub no acredita los contenedores efectivos.

## Pruebas CI y continuidad multi-IA

El workflow nuevo `.github/workflows/qa525-host-guards-contract.yml` compila el código y ejecuta 3 conjuntos herméticos: 14 casos de clasificación de cambios, 10 de preflight físico simulado y 13 de staging/limpieza/verificación de rutas. **37 pruebas sintéticas**, sin datos reales, SSH, Docker, GitHub API o contraseñas. `QA readonly contract tests` también puede comprobar el código por su ruta. Solo un workflow finalizado `SUCCESS` del HEAD exacto permite integrar este incremento.

La rama de trabajo original del **PR #771 permanece en borrador** para desarrollar más adelante el preparador/API+WEB y rollback. Contiene operadores fijos a versiones históricas `88fb` y `0bd927b` que NO deben ejecutarse ni fusionarse en bloque.

## Operación física separada

No copiar ni ejecutar estas herramientas en glmdch por el hecho de estar fusionadas a `dev`. Antes de su primer despliegue: auditoría de archivos locales, confianza root y rutas, comparación del host, aceptación de licencia/seguridad, respaldo real de ambas bases, restore probado, pruebas API+WEB, protección de Master/SMTP/Keycloak y autorización de la intervención. El sistema de actualizaciones automáticas solo se habilita después de completar esa etapa y UAT.

## Dependencia de línea base copiada y fijada

El verificador `scripts/qa/operators/qa525-host-baseline-6ff4-readonly.py` es una **copia byte a byte de solo lectura** del preflight histórico `qa525-update-readiness-0bd927b.py` que estaba exclusivamente dentro del PR #771. SHA-256 del contenido: `7867c8ae21f8be4ad2e60e6aa1491d90c35356e67d07954a2e80994bfb78f2ae`. Se incluye aquí por dependencia de los tests y para evitar integrar cualquier preparador o aplicador obsoleto. Verifica que la **línea base física** todavía corresponde a API 6ff4 y WEB 424153. **Nunca selecciona la versión a instalar**; ese rol corresponde al gate de release del HEAD vigente. Si la línea base física ya cambió, bloquea y requiere nueva revisión, en lugar de forzar la versión histórica.
