# Handoff — QA525 #819: recuperación de preparado expirado

- Fecha: 2026-10-31 (America/Santiago).
- Responsable: ChatGPT (agente:chatgpt). Control de operación: Sponsor/PO; IA no accede a glmdch por SSH.
- Punto de partida: dev `a184936d78e6e9ee0fa85f7be060b13a5280710e`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios.
- Issue: [#819](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/819), continuidad [#525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525).
- Último QA físico validado: API/WEB `41d9c490d138419c7286445f37e7aeddf0d1b8d5`, ambos running y journal APPLIED. Paquete v0.189 `a184936d78e6e9ee0fa85f7be060b13a5280710e` preparado con Sigstore verificada, `prepared.json` presente, sin journal de target.
- Gate del operador: `VIGENCIA_RESTANTE_SEGUNDOS=-567`; `NO_APLICAR=VIGENCIA_INSUFICIENTE`. Abortó antes de ejecutar el aplicador.
- Prohibiciones: NO editar actas, timestamps, prepared ni journals en glmdch; NO repetir EF100; NO utilizar srv01, PR771/737/766, main, datos reales o producción.

## Plan de implementación

1. Incorporar un comando opt-in de recuperación de prepared ya construido, con nueva revisión técnica comprobada contra GitHub y CI/Sigstore actual.
2. Bajo locks, exigir que el prepared SHA/digest y esquema coincidan, journal target esté ausente, ningún nonce se haya consumido, baseline e imágenes de Docker sean idénticos, todo journal previo esté APPLIED y las actas antiguas estén realmente expiradas.
3. Archivar **conjuntamente** el `prepared.json` vencido y las tres actas en `review-archive-*`, preservando bytes íntegros, hashes, recibo root0600 y journals sin alteración. Tras el archivado exitoso ya no queda `prepared.json` activo: esto **no** es despliegue, aprobación, renovación ni borrado de evidencia.
4. Desde el HEAD de `dev` vigente tras integrar los cambios, generar release exacta **nueva**, revisión técnica y aprobación con TTL/nonce nuevos y G6 verdaderamente vigente; continuar por el aplicador supervisado existente con sus gates y rollback dual. Nunca reaplicar el preparado anterior por bypass.
5. Regresiones adversariales, CI exact-head, revisión multi-IA y release firmada antes de ejecutar nada en glmdch.

## Implementación en PR #820 (draft)

- `scripts/qa/operators/qa525-update.py`: nuevo subcomando explícito `--archive-expired-prepared`, con chequeo de permisos root0600 mediante el entrypoint existente, SHA/digest/scope/schema exactos, todas las actas expiradas, nonce no consumido, todos los journals anteriores APPLIED y ningún journal del candidato; verifica snapshot y estados running de API+WEB, imágenes OCI target correctas, cadena Compose/identidad revisada y salud del WEB vigente.
- El subcomando **no** despliega, reconstruye, migra, firma, consume nonce ni renueva timestamps; mueve el preparado y tres actas conservando bytes a `review-archive-*` y genera un recibo root0600 `EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT` con hashes y fsync. Ante excepción intenta restituir los originales; no ofrece flags para omitir validaciones.
- `tests/test_qa525_update_automation.py`: contratos adversariales para fuente/digest/estado/imagen/snapshot, todas las expiraciones, nonce, journal PENDING/ROLLED_BACK, y prueba hermética de archivo sin despliegue ni mutación de servicios.
- `docs/qa/QA525-FAST-REVIEWED-UPDATES.md`: procedimiento de recuperación y límites de instalación.

## Controles y pendientes para la próxima IA

- Rama `fix/qa525-expired-prepared-20261031-gpt`, PR [#820](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/820) **DRAFT**; no promover dev ni main desde un borrador sin revisión y CI exact HEAD.
- Verificaciones `QA525 supervised API WEB dual transaction (hermetic)` y `QA readonly contract tests` en SUCCESS para el último SHA consultado `fad0d28aae3c4c5ee4598787944251e6d94836cd`; restantes Actions en progreso al registrar y deben releerse.
- Antes de aceptar: revisión de seguridad independiente del nuevo operador, pruebas exhaustivas de filesystem root/symlinks/rollback y de no alteración de journal, CI/Showcase/instalable exact-head PASS. Evitar afirmaciones de ejecución física.
- **Nuevo SHA de merge implica nuevo release**, por lo que el preparado viejo a184 queda archivado, nunca se fuerza su aplicación. Actualizador instalado en glmdch permanece antiguo: desarrollar/habilitar procedimiento **firmado y supervisado** para sustituir launcher root-owned de modo controlado; `--install-launcher` actual es O_EXCL y NO reemplaza. No ejecutar raw ni editar el launcher manualmente.
- En el host, no iniciar archivo ni actualización antes de tener el operador firmado oficialmente instalado y autorización explícita. Luego se requiere nueva revisión técnica estructurada y G6 verdaderamente vigente; un solo `--review-and-run` del HEAD nuevo.
- La UAT sintética sigue pendiente: Ver como 415, Taller00, asignación de perfiles y crear usuario (#754); `main` sin cambios.

## Estado

Código en PR draft, tests herméticos iniciales PASS, **SIN autorización para uso físico**. Compilar/revisar nuevamente y dejar constancia en #819, #525 y Drive.


## Corte documental de continuidad — 2026-10-31 (GitHub API, antes de este commit)

- El commit previo de esta PR era `fd598b13a810e9dafd97708e33407d81f2e7c255`, con **7/7 workflow runs completed/success**. GitHub check-runs: **15 completados: 14 SUCCESS, 1 SKIPPED** (Deploy testing showcase). Son validaciones exact-head del **commit previo**, no se transfieren automáticamente al nuevo SHA de esta documentación; releer Actions tras el push.
- `dev=a184936d78e6e9ee0fa85f7be060b13a5280710e` y `main=6dfb9546a4873baff15955cf86abfd7d47e3d111` consultados en GitHub API antes del cambio.
- Seguimiento de intervención mínima, agentes multi-IA, espejo GitHub/Drive y esquema read-only en [Issue #821](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/821). Todo trabajo desde HEAD vivo de `dev` y PR; no implica habilitar automatismos de despliegue.
- **Diferencia aclarada**: en el plan anterior se decía no archivar `prepared.json`, mientras el código y su prueba hermética lo mueven al archivo histórico. Se corrigió la instrucción; sigue pendiente revisión adversarial de seguridad de la operación, instalación firmada del launcher y autorización física.
- Estado: **PR draft, NO despliegue**. No editar prepared/actas/journals a mano, no reaplicar SHA antiguo ni tocar `main`, producción, `srv01` o EF100.


## Seguridad ante corte abrupto del archivado de preparados vencidos — 31-10-2026

El operador del PR #820 crea el marcador privado `expired-prepared-archive-pending.json` en el directorio QA525 **antes** de mover `prepared.json` y las tres actas a `review-archive-*`. Lo escribe con `root:root 0600`, incluye SHA, digest, nombre del archivo e hashes de las evidencias, y sincroniza a disco. Al finalizar el archivo completo y su recibo, sincroniza los directorios y quita el marcador. Si ocurre una excepción Python normal durante los movimientos, revierte los archivos; una prueba hermética inyecta un error en el segundo renombrado y confirma integridad y limpieza. Si hay `SIGKILL`, caída de energía o rollback incompleto, debe quedar el marcador y el próximo intento `--review-and-run`, `--run-reviewed`, `--apply-prepared` o `--archive-expired-prepared` **se bloquea** con `RECOVERY_ARCHIVE_PARTIAL_REQUIRES_RECONCILIATION` mientras el marcador esté presente. `--status` expone `archive_reconciliation_required` sin cambiar la máquina.

**El marcador NO es una aprobación, un journal APPLIED ni un permiso para continuar.** Jamás borrar manualmente ese marcador, usar otro launcher o fingir un prepared exitoso: inspeccionar originales y archivo parcial, validar hashes y journals bajo el procedimiento de recuperación previamente revisado. El PR todavía está DRAFT. La prueba de excepción no equivale a simulación real de pérdida súbita de energía ni prueba el funcionamiento en glmdch; se exige revisión adversarial independiente y liberación firmada antes del uso físico. La nueva actualización de QA también requiere resolver el reemplazo firmado del launcher inmutable actualmente fijado (O_EXCL), verificación de G6 y nueva aprobación válida.
