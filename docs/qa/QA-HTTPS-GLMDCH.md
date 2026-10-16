# QA G3 — HTTPS canónico glmdch (#517 / #519)

Perfil explícito `PMGM_QA_HTTPS=true`, exclusivamente sintético. No instala ni opera glmdch desde CI. Silo exige el bundle firmado del mismo SHA; se mantienen sus firmas, guard y carga por ID local. No cambia main ni reservas #479.

## Primera instalación

Usar únicamente candidato completo firmado y verificado. Antes de activar: inspección G7, DNS del dominio aprobado, reachability 80/443 y certificados, BD2 QA/G4, cuentas G8, custodia de respaldo y decisión operativa pendientes. El perfil NO autoriza publicar por sí solo. El entorno fuera de Git debe fijar:

```
PMGM_QA_HTTPS=true
PMGM_QA_WEB_PUBLIC_URL=https://sistema.granlogiamixta.cl
PMGM_QA_OIDC_PUBLIC_URL=https://sistema.granlogiamixta.cl/identity
```

El instalador existente `INSTALAR.sh` selecciona el modelo público sólo después de verificar el bundle. Caddy publica únicamente 80/443, redirige HTTP a HTTPS y conserva certificados en volumen. Web, Keycloak, PostgreSQL, S3, consola y ClamAV no publican puertos. El gateway usa el patrón Caddy aprobado del piloto; no se modifica dicho piloto. Caddy obtiene el certificado público automáticamente cuando DNS/puertos lo permitan. No usar la CA interna del laboratorio en glmdch.

Keycloak usa `start`, proxy `xforwarded`, hostname estricto y `/identity`. La API obtiene metadata por red Docker, la SPA exige HTTPS y PKCE S256. El realm público deriva del realm QA vigente, preserva usuarios/roles y sólo admite callback `/auth/callback`, logout `/auth/logout` y webOrigin del dominio canónico; no localhost/IP/HTTP. Regeneración reproducible: `python3 scripts/qa/public-ingress.py`; tests bloquean deriva.

**Sólo volúmenes PostgreSQL vacíos en esta fase.** Keycloak omite imports de realms existentes: el instalador rechaza el volumen previo antes de pull/build/up. No borrar volúmenes para sortearlo. La migración de un QA existente, cambios de issuer y recuperación de sus identidades requieren entrega revisada; el perfil no la inventa ni la ejecuta. Backup/smoke pueden reconstruir el modelo ya instalado sin reimportar ni modificar roles. Restore público exige `--data-only`: detiene la aplicación y no la reabre automáticamente. Antes de reabrir se debe revisar el realm restaurado; la recuperación pública completa y rollback permanecen pendientes. El perfil privado conserva su flujo previo.

## Evidencia automatizada

Workflow `QA canonical HTTPS and OIDC`: exact-head, unitarios de configuración y laboratorio Caddy/Keycloak/PostgreSQL real en proyecto UUID efímero. Sólo el laboratorio añade `tls internal` y confía explícitamente en su CA para curl, sin desactivar verificación TLS. Comprueba discovery/issuer/endpoints canónicos, HSTS, CSP acotada, admin/master 404, PKCE login válido, callbacks inválidos rechazados y token sintético con issuer/audience. No levanta API ni UI de aplicación: la página nginx es sólo un upstream de control del proxy. No sustituye smoke autenticado, recorrido PKCE completo del navegador, BD2, G8, glmdch ni QA/UAT. Evidencia JSON sanitizada 7 días, sin tokens/claves.

G3 queda preparado en código únicamente tras gates SUCCESS; validación pública, despliegue y aceptación permanecen pendientes #97. G8/BD2 y tag remoto siguen abiertos. El dominio no es todavía un enlace entregado como QA operativo.
