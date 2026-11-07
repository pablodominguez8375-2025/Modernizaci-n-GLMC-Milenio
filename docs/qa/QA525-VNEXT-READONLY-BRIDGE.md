# QA525 vNext — puente de observación estricta (sin autoridad física)

**Estado:** biblioteca inactiva y pruebas sintéticas. No instala, no ejecuta Docker,
no autentica el host ni autoriza `APPLY` o `root`. El único QA físico vigente es
`glmdch`; no se crea un segundo servidor.

`qa525-vnext-readonly-bridge.py::inspect` conecta componentes ya integrados:

1. `qa525-vnext-host-adapters.read_only_docker` filtra los comandos exactos.
2. `qa525-vnext-runtime.build_reader` toma el inventario mediante
   `qa-lite-compose-adapter.metadata(run)` y la revisión OCI del mismo inventario.
3. `qa525-vnext-baseline-plan.validate_baseline` exige siete servicios running,
   tres oneshots `exited/0`, los 10 nombres exactos, capas válidas y Compose base
   común. API=6 / WEB=5 es el **mínimo histórico**, no un techo: un
   corte posterior puede tener API=7 / WEB=6 (u otras capas adicionales) si
   una revisión autenticada fija `expected_layers` con las **listas completas
   y ordenadas**, idénticas a la observación. Un conteo coincidente no basta.
4. `qa525-vnext-verify.verify(..., 'ROLLED_BACK', samples=3)` compara nuevas
   observaciones independientes contra la baseline exacta, incluida la salud
   inyectada y la versión pública WEB; todo cambio en capas, imagen, dependencia,
   oneshot o salud detiene en cerrado.

## Evidencia y limitaciones

La salida contiene únicamente hashes **públicos no firmados**, conteos y
`may_apply=false`, `may_install=false`, `sourced_from_authenticated_host=false`.
No imprime IDs, rutas, variables de entorno ni resultados HTTP. Un proveedor
inyectado puede mentir; el futuro punto de entrada root firmado deberá
verificar auténticamente sus puertos y los SHA de cada capa Compose. Este
puente no llama al antiguo adaptador WEB-only mutante `reverify` y no permite
recrear contenedores.

Los tres oneshots (`db-grants`, `migrator`, `minio-init`) todavía necesitan
prueba **real** desde `glmdch` por operador autorizado. El inventario de siete
servicios del 06-11 no los cubrió. Los tests sintéticos no sustituyen esa prueba.

## Qué NO resuelve

OPS Ed25519 fuera del repo y APP Sigstore PRE-root, custodia G6 y renovación
<48 h, launcher CLI con bloqueo POSIX/lease y cgroup, timeout externo del DNS,
replay/journal durable, rollback físico, y autorización separada launcher/API+WEB
siguen bajo Issue #822. Un éxito en CI o en este módulo no es un GO físico.

Coordinación: ChatGPT gestiona este puente y futura composición B7; Claude
conserva firma OPS/APP, documentación G6 y envoltorio de deadline DNS (véase
Issue #525). Ningún agente ejecuta root ni toca main/producción por este PR.
