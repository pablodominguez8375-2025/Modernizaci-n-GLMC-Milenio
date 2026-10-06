# Seguridad backend #332 — cierre documental #335

## Corte y fuentes

- Inicio: dev 5b2d131dcfd9da0104eb680e0a79b8760616fa77; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.
- Integración funcional: dev d414d2f786e7235630779937e250cd4fc4cd2c7d, squash #332/#331, sobre 8706d51 (incluye #327 y #334 de Claude).
- Head funcional validado: f015c9a8a4ef95274e88fd52c5a513a7aa54cb75. Dos rebases por avances concurrentes de dev; ningún archivo de Claude fue reemplazado.
- Instrucciones Drive: 1YCinOVBZIEG5pUaeYl2TEZqv3v6djAr1fSA4v5H_Jyk; ejecución parcial. Línea Base 1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM actualizada con avance; recibo final consolidado antes del aviso al PO.
- Este documento es una instantánea durante la publicación postmerge. El [recibo final de publicación, QA y cierre documental](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/332#issuecomment-6015571119) conserva los SHA efectivos y hashes públicos, y supersede los estados pendientes de esta instantánea.

## Implementación

API usa USER APP_UID en backend/Dockerfile. RequestRateLimiting se registra en Program.cs después de autorización: presupuestos por categoría y sujeto/issuer verificados, sin cambiar presupuesto por ruta/query/Taller/rol ni confiar en cabeceras de cliente. Alternativa de IP del par directo para peticiones sin sujeto. 120 escrituras, 20 cargas y 30 descargas de contenido/ICS por minuto; configuración positiva obligatoria, ventanas de 60 s, sin cola. Rechazo 429 en español con Retry-After/no-store antes de ejecutar el handler. Cubre barras finales; lecturas ordinarias y health siguen disponibles. Es local por proceso y se reinicia al reiniciar la API; no certifica capacidad ni sustituye una protección distribuida.

CI audita NuGet directo/transitivo de API y tests y npm de producción; alta/crítica y reportes NuGet inválidos bloquean. NuGet limpio puede omitir frameworks: se acepta sólo con versión, modo, fuentes y proyecto válidos. Informes auditables como artifact. Nuevos tests HTTP cubren identidad, presupuestos, concurrencia, lectura/health, 401, Retry-After y configuración; respetan cancelación de xUnit.

Archivos funcionales: backend/Dockerfile, backend/src/PMGM.Api/Program.cs, nueva Infrastructure/RequestRateLimiting.cs, nueva Integration/RequestRateLimitingHttpTests.cs, .github/workflows/ci.yml, scripts/check-dependency-vulnerabilities.py, tests/test_dependency_vulnerabilities.py, handoff de reserva y registro GOV-004. Sin migraciones, cambios de modelo o reglas institucionales. [Registro de transporte/configuración](../modelo-datos/2026-10-06-request-rate-limiting.md).

## Evidencia

- Gates exact-head finales: PMGM CI #1968 (run37457381146), Showcase #1359 (37457381058) y QA #997 (37457381374), todos SUCCESS.
- Backend 496/496, sin omisiones. Frontend actualizado conserva 426 pruebas; CI anterior sobre base #327 pasó 426/426. Parser local5/5, privacidad12, clasificación100 y migraciones62 PASS.
- No hay .NET/Docker locales; compilación, PostgreSQL/S3/ClamAV, no-root y HTTPS/OIDC con respaldo/recuperación se verifican en CI. No afirmar UAT manual.
- Postmerge funcional d414d2f: CI37458624482 y Showcase/Pages37458624625 en curso al preparar este handoff. QA37458624447 y Pre-UAT37458624505 SUCCESS. Resultado final en el recibo enlazado.
- QA artifact11410042317 (d414d2f), SHA256 exterior cc7a1b06e8a3810a95019aa269162fa3d974766edcabaca18aaea76f9a0d4445; ZIP interno Proyecto-Centenario-QA-srv01-d414d2f786e7.zip, SHA256 b30dbd84fcb650a33526ab3fcc0dbb4ceb7ff820305ee672d930753235153dcb. CRC y 1049/1049 hashes del manifiesto verificados, BUILD-INFO SOURCE_SHA exacto.
- Pages del corte funcional apunta a d414d2f; la publicación posterior del cierre documental cambia el SHA de fuente sin cambiar código. SHA realmente publicado y SHA256 de su ZIP se verifican en [qa-current.json](https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/downloads/qa-current.json) y quedan fijados en el recibo final. El ZIP publicado y el artifact de QA pueden diferir por metadatos de construcción.

## Pendientes y límites

- Despliegue QA pendiente; srv01 y UAT institucional pausados por #97. Main congelado; no promover.
- Frontend nginx sin root/8080 y CSP por entorno; Actions fijadas por SHA y Dependabot pendientes. No declarar toda la delegación ejecutada.
- Evaluado system/info: versión/runtime anónimos; el smoke piloto lo consulta sin token, frontend conserva SystemInfo. Autenticación requiere adaptar smoke; reducción del detalle es alternativa pendiente de decisión/implementación.
- Riesgo informado antes de #327: nginx12m frente a DocumentStorage MaxUploadBytes52428800 (50MiB) en los stacks. No reducir unilateralmente el contrato documental; resolver en siguiente corte con prueba de carga.
- No tocar ramas históricas #116/#58/#60 ni identidad, main o srv01. Cambios futuros nacen de dev vivo y reclaman Issue/PR conforme GOV-003.
