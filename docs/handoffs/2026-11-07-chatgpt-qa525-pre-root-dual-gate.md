# QA525 B4 — puente dual no autorizador PRE-root (Issue #927)

Fecha 07-11-2026, responsable ChatGPT; base de apertura dev@90521bc710f6f622b3481a3e459a609508e69907, main@6dfb9546a4873baff15955cf86abfd7d47e3d111. DRAFT y biblioteca inactiva para no solapar los PR #921/#925 ni reimplementar código de Clau #902.

Contrato técnico a implementar:
- Reusar el validador de OPS Ed25519 siete campos y su exactitud de bytes/firma `qa525-vnext-ops-signature.py` mediante adaptador/verificador INYECTADO y huella externamente pinneada del Sponsor. La huella desde Github/Drive por sí sola NO es un pin independiente.
- Exigir verificador APP Sigstore/OIDC independiente con `repo`, `issuer`, `workflow`, `source_sha`, `archive_sha256` y bundle exactos; conectar producción solamente al auditor rootless APP existente, NO aceptar un status del ZIP como firma.
- Rechazar OPS source = APP source y cualquier entrada/salida ambigua, fuente falsa, conexión reusada o callback truthy. No se autoriza instalación/ejecución por este módulo.
- Tests negativos sin OS/red ni exec; CI exact HEAD, cocontrol independiente Claude, un solo merge a dev después de compatibilidad. #822 NO-GO físico hasta autenticación por fuente independiente antes del primer byte root, G6 A restaurado y autorizaciones separadas.

Estado inicial: planificación y archivo de handoff publicados; implementación/pruebas pendientes. No se alteró `glmdch` ni `main`.

## Implementación entregada en PR #928 (diferenciar código de autoridad)
- Biblioteca creada `scripts/qa/qa525-vnext-pre-root-dual-gate.py`: exige pins y dos SHA source diferentes, fingerprint OPS y policy exactos, source/signer workflow/issuer APP, hash del bundle, llamada al contrato de verificación OPS de 7 campos del #902 (por puerto inyectado) y llamada APP con challenge completo sin mutación. Devuelve SIEMPRE `may_install=false`, `may_apply=false`, `ops_fingerprint_provenance_verified_here=false`, `app_attestation_provenance_verified_here=false`.
- Tests `tests/test_qa525_vnext_pre_root_dual_gate.py`: integran parser real OPS con verificador sintético y puerto APP sintético; rechazan huella falsa, bytes/firmas alterados, source igual, workflow/issuer ajenos, app truthy no-True, callback mutante, hash falso, resultados autodeclarados, no import subprocess/OS.
- **PENDIENTE PARA OPERAR:** adaptadores ROOTLESS concretos de `ssh-keygen -Y verify` y `gh attestation verify --bundle --repo --signer-workflow --source-digest --deny-self-hosted-runners`, pines independientes ingresados por Sponsor del gestor fuera de repo/Drive, hashes de intérprete/herramientas y bootstrap anterior al primer byte Python root. El resultado de este PR NO autentica por sí mismo la procedencia de esos callbacks, no genera recibo root ni autoriza fisico; Issue #822 sigue NO-GO.
- HEAD a revisar tras test `8fdc1201df94d96bf551cb045d53180cb0b17fa6`; exigir 7/7 exact HEAD y revisión cruzada antes de merge.

## Adaptador real OPS sin privilegios (nuevo, para revisión)
Implementación adicional reservada en PR #928 comentario 6713182910:
- scripts/qa/qa525-vnext-ops-rootless-adapter.py usa /usr/bin/ssh-keygen -Y verify con argv literal (namespace qa525-ops-v2@pmgm, principal qa525-ops), mensaje exacto en stdin, archivos temporales 0600 dentro de carpeta privada 0700, ningún shell ni PATH de usuario, stdout limitado y stderr suprimido. Refusa EUID 0 y binario no root-owned regular, detecta symlink y exige SHA256 externo del binario previo/post.
- tests/test_qa525_vnext_ops_rootless_adapter.py incluye firma Ed25519 REAL con clave temporal de TEST únicamente (no Sponsor), rechazo de bytes/sig/namespace/clave diferentes, pin binario incorrecto, ejecución root, runner hostil y modos de archivos, además de fake runner para inspeccionar argv exactos.
- La firma real se verifica sin privilegios, pero todavía falta autenticar independientemente PIN del binario/clave Sponsor desde el gestor y enlazar ese recibo al bootstrap host antes de elevar privilegios. APP debe utilizar auditor existente qa525-attested-installer-audit.py; todavía falta cierre de composición en ejecutor PRE-root supervisado, con test de doble firma real. NO declarar esta librería como instalador.
- HEAD de código/tests a verificar a2978315cb9ee89503cc85eb2848e04dd2d6c575; CI se renueva tras este documento. #822 NO-GO físico.

## 07-11 — revisión adversarial Claude #6713246779 / cierre de P1-B y bloqueo P1-A
Clau dictaminó 0 P0, 1 P1 para activación y varios P2. **P1-B corregido**: ops_source_sha es commit del CÓDIGO OPS del programa/readiness/staging; app_source_sha es commit del ARTEFACTO APP atestado por Sigstore. Es legal que sean iguales y se elimina la desigualdad forzada. La independencia es de FIRMANTES (clave humana Ed25519 vs identidad OIDC/workflow APP). Test explícito de SHA idéntico con GO siempre false, hash de recibo alterado, OPS source distinto a recibo.
**P1-A ABIERTO, HARD STOP físico:** recibo OPS v2 no firma hash del artefacto APP; un atacante puede combinar dos firmas válidas de cortes distintos. El resultado B4 ahora marca ops_app_signed_artifacts_bound_here=false siempre. Requiere evolución del recibo OPS v3 firmando app_source_sha/app_archive_sha256 o verificación del manifiesto de archivos atestados contra los tres hashes firmados. Clau ofreció carril nuevo exclusivo para contrato #902 v3; aceptación registrada en PR #928 comentario 6713360716. Hasta cerrar ese vínculo y autenticar puerto APP con pines externos, prohibido instalar aun con tests verdes.
P2: wrapper OPS debe declarar no vacíos los fingerprint APP; adaptador externo pinneado ssh-keygen depende del TCB root (auditar ancestros /usr/bin); stdout PIPE tiene tope post-hoc; terminar con salida acotada antes de uso físico. No reusar old GO ni claim de status como firma. 

## Endurecimiento P2 OPS rootless (07-11)
- Antes de inspeccionar/ejecutar /usr/bin/ssh-keygen, exige que /usr y /usr/bin sean directorios reales, root-owned y no escribibles por grupo/otros. Se conserva validación del propio binario, inode estable y SHA externo pre/post; esto reduce sustitución de ruta, no elimina el TCB del kernel/root del servidor.
- Se sustituyó el stdout=PIPE sin límite de subprocess por archivo temporal privado en carpeta 0700, lectura máxima 1024 bytes después de finalizar y rechazo de contenido superior. Los errores no incluyen stderr ni datos privados. El proceso directo usa timeout de 30 segundos; SIGKILL/cgroup por hijo y cierre físico siguen pendiente del ejecutor externo B4: este adaptador NO es un orquestador root.
- Tests adicionales de ancestros directorios no confiables y salida 1025 bytes; el mock escribe en stdout temporal y se valida mode 0600 del receipt/signature.
- HEAD código/test de esta ampliación 7cbd3598951e0cdbe72d2e9ace0fa274fa896d04; CI del nuevo HEAD obligatorio. **P1-A vínculo artefactos APP/OPS sigue abierto**, flags de origen/GO false y sin glmdch.
