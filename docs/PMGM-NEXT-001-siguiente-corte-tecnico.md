## Handoff vivo — revisión de aprobaciones — 28-09-2026 UTC

- SHA observado al abrir este handoff: `dev@26deb078a25b62fa863bf4e709e6820a337d67a2`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin promoción. PR #211 documental integrada en dev@26deb078. Pages run 36455750108 SUCCESS; `qa-current.json.sourceSha` coincide con 26deb078. QA artifact #10985078472 empaquetado; no instalado. Issue #97 mantiene la pausa de srv01.
- Revisión explícita pendiente: PR #154 solicita al PO 3 decisiones para implementar Perú en USD: (1) sublibros CLP/USD separados y sin conversión, (2) cuota local peruana en USD con mínimo oficial USD 6, (3) circuito de recepción/conciliación USD con Gran Tesorería. Es PR documental/propuesta, base antigua dev@8522b0d, head 698908e, mergeable=false y sin revisiones registradas. No aprobar ni fusionar esa base obsoleta sin comparar/rebasar primero.
- Fuente oficial Drive revisada: Decreto N.º 1.759, emitido 15-12-2025 y vigente desde 01-01-2026, Drive ID `1qsXM3CPAJw9jW1EHk3YjbNinHnexRub2` (archivo actualizado 22-09-2026): Santiago CLP 21.000, otros Orientes CLP 15.000 y Perú USD 6 para cuota ordinaria. No define importe USD para cónyuge/estudiante/tercera edad peruana ni tipo de cambio; no extrapolar. Cuadro Pago Gran Tesorería (11-09-2026) es referencia operativa. Manual Módulo Tesorería (versión 2026, actualizado 21-09-2026) describe recepción real e imputación a cuotas pasadas por mes; no resuelve presentación financiera de anticipos futuros ni política completa de reversos.
- Issue #190 ya registra la regla aprobada de dos componentes (aporte oficial + local) y ubicación en ficha de Taller; el dato oficial USD 6 existe. Antes de implementar su extensión monetaria, resolver las tres decisiones de PR #154 y no inferir categoría/tipo de cambio.
- Issue #191: regla operativa confirmada por PO de separar año de recepción del dinero del período de cuota aplicado. Sigue pendiente definición contable institucional para anticipos/recuperaciones, pagos multiperíodo y reversos/correcciones. No inventar cuentas contables; requiere fuente/decisión antes de cerrar diseño.
- Issue #63 sigue abierto para aceptación visual institucional global. En sus comentarios sólo consta aprobación parcial y ligada a SHA antiguo para Mi ficha → Biblioteca Virtual; la auditoría responsive automatizada no sustituye la revisión/aceptación del PO.
- Issue #46 ya tiene el alcance principal implementado por PR #206. Sigue una discrepancia puntual entre el criterio «justificada» y la línea base/código «presente/ausente»; no modificar ni cerrar hasta reconciliar la regla.
- Operación y releases: Issue #97 continúa pausado por instrucción vigente; levantarlo requiere instrucción explícita del Sponsor. PR #1 main ← dev permanece draft/bloqueada: su candidato UAT 739ba0b es anterior al dev actual; no solicitar aprobación de promoción antes de nuevo candidato y UAT. PR #116 está abierta en base histórica dev@5704400/head 17e1bd7; comentario PO exige autorización de merge separada, pero primero comparar el candidato con dev actual. PR #182 está cerrada sin merge y sustituida por #204.
- No hay cambios funcionales en este corte. No se instaló, no hubo QA física/UAT y main sigue intacta. Los checks/Pages/paquete prueban sólo integración/publicación/empaquetado. Revisión registrada también en Drive Línea Base Maestra.

## Handoff vivo — post-merge PR #210 — 28-09-2026

- Estado verificado: `dev@4087f47a575ba6bf48fead93838de5425a62bb10`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. No había clon Git local; lectura remota de ramas, árbol, fuentes y estado completada.
- PR #210 documental quedó integrada por squash en el SHA vigente. Exact-head del PR y post-merge PMGM CI, Showcase/Pages, QA Installable y Pre-UAT: SUCCESS.
- Pages artifact #10984181780 digest `sha256:4cff4268bee16ca3e48cffc9d0f0ddfff0bb1fa107571c741867a54f8b1dd566`. `qa-current.json` confirma `sourceSha=4087f47a575ba6bf48fead93838de5425a62bb10`; ZIP publicado SHA-256 `a43f2b7490b6f3b5e7b44d96c9137ae655ca5aa0aaa2f1f1686b133d7c0d9a6c`.
- QA artifact #10983728727 digest `sha256:353e3808fd25424fe1837e217303cbdc79a3c692ae62331cf04036a9ae456b92`; ZIP instalable interior SHA-256 `cbe8e16c66b4cd0d7d470160fe41102fc4212d7c35bc1f0e9acd85478fac2ceb`; BUILD-INFO verifica el SHA de origen. Paquete generado, no instalado.
- Issue #97 sigue abierto. `srv01` pausado por instrucción del Sponsor: no instalar, desplegar ni ejecutar smoke/regresión física/UAT. No promover `main`.
- Issue #46 / PMGM-BLG-078 sigue abierto. El código vigente de Mi ficha presenta historial propio de instrucciones con fecha, grado, tema, asistencia y encargado, en modo sólo consulta; la demo usa registros ficticios y la prueba frontend actual impide agregar un campo `progress` a los datos demo. La prueba no sustituye aceptación visual institucional. Antes de tocar código, identificar una brecha concreta frente a sus criterios; no duplicar lo integrado por PR #206.
- No se inició un incremento funcional. #190 mantiene pendientes tarifas/monedas; #191 requiere cerrar presentación contable. PR #182 fue cerrada sin merge y sustituida por #204.
- La continuidad se rige por estado persistente: cualquier chat o IA puede continuar leyendo START-HERE, GitHub `dev` y la Línea Base/documentos oficiales de Drive. Ningún chat o memoria conversacional es requisito ni fuente de verdad. GitHub gobierna código/backlog/decisiones técnicas; Drive conserva Línea Base y fuentes oficiales. Esta regla armoniza las instrucciones vigentes y no altera gobierno institucional.

## Verificación viva y handoff — 28-09-2026

- HEAD comprobado: `dev@57412f9afd90586899edd4885f371b96cceb36ac`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. No hay clone local en el workspace; inspección remota de raíz/árbol completada, sin modificaciones de código.
- PR #209 integrada por squash (head exacto `bee0f62d0da9a56eee4de3e5cb6d58b4f875a21d`). Sólo modificó la auditoría responsive y documentación para incluir SVG visibles.
- CI exact-head de #209: PMGM CI #1659, Showcase #965, QA Installable #603 SUCCESS. Auditoría Chromium: 20 perfiles, 483 estados a 360×800, 563 capturas, sin overflow detectado. Esto es evidencia automatizada; no equivale a revisión institucional, dispositivos reales ni UAT.
- Post-merge del SHA vigente: PMGM CI run 36430246829 SUCCESS; Showcase/Pages run 36430246786 SUCCESS y Deploy testing showcase SUCCESS; QA Installable run 36430246937 SUCCESS; Pre-UAT run 36430247019 SUCCESS.
- Pages artifact #10973542145, digest `sha256:f107ec8feaff05c588b3a2611e7dc9a259ebf0d1e3377e661474b61e8c469555`. Se leyó `downloads/qa-current.json`: `sourceSha=57412f9afd90586899edd4885f371b96cceb36ac`. ZIP publicado `Proyecto-Centenario-QA-srv01-57412f9afd90.zip`, SHA-256 `0e8704690a0b067ceca5734169f48cf3b9b7031bf16e0d7c3e9546b85b52f293`.
- QA artifact #10971934945, digest/SHA-256 del artefacto `8247265d7241579e9e6374356f8794e4ccfea0458239cf204481df961c2bb5b6`. ZIP interior `Proyecto-Centenario-QA-srv01-57412f9afd90.zip`, SHA-256 `b223222c8cf895f668e8489ed9ee2820938a781bd023caa67e882962a2e8ef89`; `BUILD-INFO` confirma SOURCE_SHA y run 36430246937. Paquetes de Pages y QA se construyeron separadamente y sus checksums difieren.
- Estados: funcionalidad de docencia por grado integrada en dev; auditoría SVG integrada; Pages publicado en el SHA actual; artefactos QA/Pre-UAT generados. Nada instalado, sin QA física/UAT aceptada. Issue #97 mantiene `srv01` pausado por el Sponsor; no desplegar/instalar/probar físicamente ni promover `main`.
- No iniciar otro incremento funcional sin alcance aprobado por el Product Owner. Issue #190 conserva decisiones de tarifas/monedas pendientes; #191 requiere definición de presentación contable; PR #182 está cerrada sin merge y sustituida por #204. El filtro de fecha/grado expuesto por API pero no por vista, señalado en el cruce documental, requiere alcance aprobado antes de ampliarlo.
- Próximo paso: esperar un alcance aprobado o instrucción del Sponsor para reanudar QA física/UAT. Revisar la discrepancia documental: GOV-001 conserva una frase histórica de “hilo maestro único”; START-HERE/GOV-002 y la Línea Base vigente establecen continuidad desde GitHub + Drive y verificable por cualquier IA. No cambiar reglas institucionales a partir de esa diferencia.

## Handoff vigente — Docencia por grado y consulta institucional — 28-09-2026

PR #206 quedó integrado por squash en `dev@7f2079b3c1d89564cc181f6efad5f231de6db081`, desde `feature/instruction-instructors-by-grade-20260928` y SHA exacto revisado `8dc936301b597b1c2aeaf0b48ec309c8456c59bc`. `dev` inicial `1baf752c8be94e4d7b667d03ac0cad9f4b05fb31`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta. PR #207 registra el cierre documental posterior.

El Segundo Vigilante puede registrar instrucciones/asistencia de Aprendices; el Primer Vigilante, Compañeros; el Inmediato Ex-Venerable Maestro, Maestros. El historial personal se refleja en Mi ficha. En Orden, Gran Segundo Vigilante consulta Aprendices, Gran Primer Vigilante Compañeros, Inmediato Ex Gran Maestro Maestros, y Jefatura de Docencia los tres grados. Consulta por Taller/consolidada, período y grado autorizado. Ninguno recibe mutaciones; se reportan sesiones realizadas y estadísticas agregadas sin identificar hermanos. Definición, permisos y QA están en GOV-001 §10.1, `PMGM-ARCH-018` y `docs/qa/PMGM-QA-V072-DOCENCIA-ORDEN.md`.

PR exact-head: PMGM CI #1651, Showcase #954 y QA Installable #592 — SUCCESS. Post-merge `dev@7f2079b…`: PMGM CI #1652, Showcase/Pages #955 con Deploy SUCCESS, QA Installable #593 y Pre-UAT #393 — SUCCESS. Pages artifact #10967675504 SHA-256 `f7ae60ac8f7af3c8a8aa051b8c2b19820626e571795f7a4c0f221395a0054053`; qa-current confirma `sourceSha=7f2079b3c1d89564cc181f6efad5f231de6db081`. ZIP de QA en Pages checksum `567b15253b585c7919ae90afe05dda43d5827112885b783b7f7172c874e14cc3`. QA artifact #10966054591 digest `19cac46d170b79fe09d2f8be0814c78d2be0f14540e51e6eb63552feab965414`; paquete incluido checksum `01eea675c476ec4e55c690c26101f27ec8aa5eee6c231397ee9c00fe0084b279`, `SOURCE_SHA` confirmado y MANIFEST 780/780. Pre-UAT artifact #10966654846 digest `1a291bb7d38c7908c959fcf7f94b8629996895b7c7fde275960340420e5d1a7f`. Los paquetes se generaron en pipelines distintos; sus checksums no son iguales, ambos trazan al mismo SHA de código.

Frontend local lint/build y 216 pruebas SUCCESS; privacy/classification/migration gates SUCCESS. .NET SDK no disponible localmente; backend, suites y smokes verificados por CI. Pages publicado. Issue #97 abierto y `srv01` pausado: no instalación, despliegue físico, smoke/regresión física ni UAT. Nada promovido a `main`. En continuidad futura, consultar HEAD, PRs/issues, checks, Pages y Drive en vivo; no usar este handoff como reemplazo de esas verificaciones.

## Handoff vigente — cierre de revisión referencial y acceso a Ficha del Taller — 28-09-2026

**SHA integrado:** `dev@4f1396820846342f3bd5091b51c6a62857d3a39c`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #204 se fusionó por squash desde `docs/refresh-logial-crosswalk-20260928`, HEAD exacto `7b5fffa067154c5453ecfed20e84c7687780a29f`. SHA de inicio de dev: `c05fa74e4e481a481a4e5054512675b006c4c57a`. Árbol local limpio, detached en `origin/dev`.

Archivos PR #204: `docs/reviews/PMGM-REV-LOGIAL-MENUS-2026-09-28.md` (nuevo) y `docs/PMGM-NEXT-001-siguiente-corte-tecnico.md`. Actualiza el cruce con la referencia Drive antigua y explica acceso a **Taller → Ficha del Taller** condicionado por capacidades. No hubo cambios de código, permisos, migraciones ni funciones. Mis planchas y Resumen del Taller constan ya integrados; se separa historial individual de cargos de una Oficialidad histórica por períodos. La API de instrucciones acepta filtros de fecha/grado; falta exponerlos en cliente/vista y requiere definición de alcance del Product Owner.

Checks exact-head del PR #204: PMGM CI #1645, Showcase #946 y QA Installable #584 — SUCCESS; despliegue Pages omitido en el evento PR. Post-merge sobre el SHA integrado: PMGM CI #1646, Showcase/Pages #947 (incluido Deploy showcase SUCCESS), QA Installable #585 y Pre-UAT #391 — SUCCESS. Artefactos: Pages #10947342572 digest `sha256:c657c49f942ed48775cde2e9260cc1cb62d2027466241bc95f7966fc28d7d5b1`; evidencia visual #10947551850 digest `sha256:d9ee224ea735874c5590ad078b36517c16b924255e2bf06a7614df19f0ee6aeb`; QA Actions #10947382198 digest `sha256:78056dbe4c062b23f66718dbca0feafddc73c50479c001a0fdcc05a39fddbec0`; Pre-UAT #10947138143 digest `sha256:1979ab2b6d5bf4827a78e8f92fc4b02f6419312a47de5d359187104eb22f88b3`.

Pages está publicado. El artefacto Pages contiene `qa-current.json` con `sourceSha=4f1396820846342f3bd5091b51c6a62857d3a39c`; el ZIP publicado `Proyecto-Centenario-QA-srv01-4f1396820846.zip` tiene SHA-256 verificado `c65cd218e7ca5b99943f31e9c364345bc99050c5a506a27b066d24ea6e83cf47`. El ZIP del artefacto QA, del mismo SOURCE_SHA, tiene SHA-256 `3b29c38beae40358b07d5c296bf2cded8ccbf9c304547aedaf9fc784f4c14116`; BUILD-INFO coincide y MANIFEST valida 776/776 archivos. Estos resultados representan publicación/empaquetado, no instalación ni aceptación.

PR #182 fue cerrada sin merge y con enlace a PR #204 como sustitución. Issue #97 sigue abierto; `srv01` continúa pausado: no hubo instalación, smoke/regresión física ni UAT. No promover a `main`. Issues #190/#191 mantienen pendientes sus definiciones monetarias/contables. Antes de cualquier función nueva, esperar alcance aprobado del Product Owner. En un nuevo chat, leer START-HERE y volver a consultar HEAD, árbol, PRs/issues, Actions y Drive en vivo.

Esta entrada supersede para el estado de PR #182 los bloques históricos de abajo.

## PR en curso — origen y pertenencia de Ficha del Taller — 27-09-2026

El Product Owner confirmó que **Secretaría del Taller**, **Gran Secretaría** y **Régimen Interior** pueden editar los campos de la ficha: fecha histórica de creación/fundación, ciudad/Oriente y país. La Secretaría local queda limitada a su Taller; los dos perfiles centrales actúan con alcance institucional de Orden. No se concede esta edición a Tesorería, Venerable ni administrador técnico.

PR #189, rama `feature/workshop-origin-metadata-20260927`, se abrió contra `dev@80dd71222aa391984709847436a1dc507b679d84`, con HEAD inicial `28fdadc6b00f92aad4cbb3ffaea35c801d29c7c3`. Issue #188 registra el requisito. `Organization.CreatedAtUtc` conserva la fecha técnica del registro, diferenciada de la fecha histórica `EstablishedOn`. `City` y `Country` quedan separados de `TreasuryTerritory`: no se infiere el tramo tarifario desde ubicación porque el Decreto 1759 no da una asignación general.

El PR incluye migración nullable, lectura/escritura auditada, control de perfil y organización en backend, formulario y datos sintéticos de Demo, pruebas de permisos y contrato. Especificación: `docs/PMGM-ARCH-017-ficha-taller-origen-y-permisos.md`. Frontend local: 212/212 pruebas, lint y build SUCCESS. Gates de migración (54), privacidad y clasificación de datos SUCCESS. .NET SDK no está disponible localmente; backend/PostgreSQL dependen de CI exact-head.

Para el HEAD inicial `28fdadc6b00f92aad4cbb3ffaea35c801d29c7c3`, PMGM CI #1610, Showcase #898 y QA Installable #536 terminaron SUCCESS. Showcase fue validación de PR; no hubo publicación de Pages. Se añadió un commit documental posterior, por lo que hay que repetir/confirmar CI exact-head para el HEAD final antes de considerar el PR listo. No hay instalación, despliegue QA ni UAT; `srv01` sigue pausado e Issue #97 abierto. El paquete QA no equivale a instalación. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sigue intacta.

## CIERRE POST-MERGE — MIS PLANCHAS — 27-09-2026

HEAD verificado de `dev`: `49744d10472e739580f58e0752e211ce25f22655`; PR #185 integrada. `main` permanece intacta en `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Gates exact-head SUCCESS: PMGM CI #1604 (run `36288091657`), Showcase/Pages #890 (run `36288091706`), QA Installable #528 (run `36288091679`) y Pre-UAT #376 (run `36288091698`). El despliegue de Pages pasó.

Se verificó la URL pública `/downloads/qa-current.json` y se descargó el paquete desde Pages. `sourceSha=49744d10472e739580f58e0752e211ce25f22655`; SHA-256 esperado y calculado `d4f2162cf183120efc1491654b594966ba2f76c1d2134b45000d27f323f9f931`; BUILD-INFO identifica el mismo SHA y MANIFEST valida 768/768. Artifact Pages #10921566427, digest `sha256:42efde6c2eec1f488a9a67a1a5778d56549f3446eb1d9abb895175419736e1a1`. El build se publicó por el flujo post-merge; `SOURCE_REF` conserva el nombre de la rama del PR y el workflow usa el merge SHA exacto.

Artifact QA #10921187044, digest `sha256:b32be48b4c7fc926d426331aecdef7dd68c1116aaac8547353dc5d81c85058db`; BUILD-INFO del mismo SHA y MANIFEST 768/768. Artifact Pre-UAT #10921176157, digest `sha256:cb54e8d1472d37b1729591230ab91a97e700f33ee19c92b0051565e4db1da58a`.

**Límite vigente:** `srv01` sigue en pausa. No hubo instalación, smoke autenticado en servidor, regresión física ni UAT; CI y artefactos no son aceptación QA/UAT. Issue #97 continúa abierto. Esta sección supersede los estados de pre-merge de abajo.

## Conformidad visual del menú activo — 26-09-2026

El Sponsor / Product Owner confirmó «Quedó perfecto» en la demo publicada, respecto del menú seleccionado en dorado al tocar «Mi calendario» y la conservación de los colores institucionales en la navegación. Se registra como conformidad visual de la demo. No equivale a instalación real, QA ni UAT; `srv01` continúa en pausa e Issue #97 sigue abierto. No se inicia otro incremento funcional. 
## Estado integrado — menú activo dorado al tocarlo — 26-09-2026

PR #179 quedó integrada por squash en `dev@9eef03623fc5e441bb254d057ed533dfce443413`. La causa era el mayor peso CSS de `:hover` sobre `.active` en `institutional-theme.css` y `member-portal.css`; excluir `.active` de ambas reglas mantiene el dorado institucional al tocar «Mi calendario» en móvil o dejar el puntero encima en escritorio. No cambia rutas, permisos ni lógica.

Prueba nueva: falla antes de la corrección y pasa después; frontend local 205/205, lint y build SUCCESS. Gates post-merge del SHA exacto: PMGM CI #1589, Showcase/Pages #870, QA Installable #508 y Pre-UAT #371 — SUCCESS. Pages `qa-current.json`, BUILD-INFO y ZIP identifican el mismo SOURCE_SHA; SHA-256 del ZIP Pages `484678413c8cbe69eff430f0cb2181cf0ddff3d96ade998e54884f5e7abc89d1`. MANIFEST valida 757/757 en Pages y QA Actions. Los ZIP de Pages y Actions se generan de forma independiente y tienen hashes diferentes, pero comparten SOURCE_SHA.

La demo está publicada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/. `srv01` continúa en pausa; no hubo instalación, smoke autenticado en el servidor, regresión física ni UAT. CI, la demo y los paquetes no equivalen a QA/UAT aceptadas. Issue #97 sigue abierto; `main` no se modifica ni promueve.

## Estado vigente — icono de Biblioteca Virtual en Mi ficha

PR #177 quedó integrada en `dev@79401d05faf7cf25ab2523f283471cb10e7221a5`. El mosaico azul «Biblioteca Virtual · acceso por grado» ahora muestra el libro dorado; la causa era que su trazo heredaba el mismo azul del fondo. No cambian rutas, permisos ni acceso por grado. Los gates post-merge de CI, Showcase/Pages, QA instalable y paquete pre-UAT terminaron SUCCESS; `qa-current.json`, BUILD-INFO y MANIFEST se verificaron contra el mismo SHA. `srv01` sigue pausado y Issue #97 permanece abierto. La evidencia automática no equivale a instalación ni aceptación de QA/UAT.

## Hallazgo visual reportado — atajo de Biblioteca Virtual — 26-09-2026

El mosaico «Biblioteca Virtual · acceso por grado» de `Mi ficha` tenía un cuadro azul sin el icono del libro. `MemberPortalPage` sí renderizaba el SVG, pero `memberLibraryShortcut.css` le daba el mismo azul marino al fondo y al trazo. PR #177 cambia sólo el trazo a dorado institucional y agrega cobertura de regresión; la tarjeta conserva su fondo azul y su comportamiento de acceso por grado. El resultado se anotará al completar los gates del PR. No involucra instalación ni QA/UAT en `srv01`.

## Estado vigente — PR #175 integrado — 26-09-2026

El resaltado del menú activo del Portal del Hermano quedó integrado en `dev@d13428684b13f649d6732e9be4e555e90a94248d`. `member-portal.css` era la causa: sustituía el estado activo institucional por blanco translúcido y texto claro. El estilo corregido marca la selección con fondo dorado institucional y texto/iconos azul oscuro, manteniendo la navegación azul.

CI exact-head del PR y gates post-merge, incluida publicación de Showcase y generación del instalable, finalizaron SUCCESS. Se verificaron `qa-current.json`, `BUILD-INFO`, checksum y MANIFEST contra el SHA integrado. Esto no implica instalación real, aceptación de QA/UAT ni levantamiento de la pausa de `srv01`. Issue #97 permanece abierto. No se inicia otro incremento funcional sin un alcance autorizado.

## Alcance autorizado — resaltado del menú activo del Portal del Hermano — 26-09-2026

La captura móvil reportó que «Mi calendario» pierde el resaltado dorado. La causa confirmada es que `member-portal.css` define los accesos activos con fondo blanco translúcido, sobreescribiendo la señal visual institucional. El alcance autorizado se limita al estado activo de la navegación: fondo dorado institucional, texto e icono azul oscuro, manteniendo el fondo azul de la barra y los colores de los demás accesos. No cambia rutas, permisos ni lógica de navegación.

## Verificación de continuidad — 26-09-2026

HEAD vivo verificado: `dev@b68338026f4741360392a5f448a8999060e020d3`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece intacta. PR #172 quedó integrada y PR #173 documentó la regla visual de iconos dorados sobre fondos azules.

Los gates post-merge del SHA exacto `b683380` finalizaron SUCCESS: PMGM CI #1575, Showcase/Pages #850, QA Installable #488 y Pre-UAT #365. La Línea Base Maestra de Drive y el último comentario de Issue #97 contienen la referencia de Pages y artefactos asociados a este corte.

`srv01` continúa pausado hasta nuevo aviso. No se ejecuta instalación, despliegue, smoke autenticado, regresión física ni UAT. Los resultados CI/artefactos no constituyen aceptación de QA/UAT. No iniciar incremento funcional sin autorización expresa para un alcance concreto; no modificar ni promover `main`.

# PMGM-NEXT-001 — Siguiente corte técnico

## CIERRE — menú Ficha del Taller, identidad y logo — 27-09-2026

El Product Owner reportó que no encontraba la ficha del Taller, y confirmó incluir logo personalizado opcional. Issue #199 formaliza el alcance: enlace directo bajo Taller; nombre, fecha de iniciación, ciudad/Oriente, país y logo. Se mantiene el permiso vigente de lectura/edición; Secretaría del Taller, Gran Secretaría y Régimen Interior editan dentro de su alcance; Venerable consulta/supervisa según la matriz. El logo es PNG/JPEG ≤2 MiB, firma validada, escáner antimalware y almacenamiento privado. No se otorgan permisos nuevos ni se modifica `TreasuryTerritory`.

PR #200 se integró por squash en `dev@d455d454dff0e8cb135ca8d21c04bbd2242be38b`, desde `dev@c6da21c92beac84a70fca0aadb6b26a984842242`; SHA exacto validado del head del PR: `48809f9f0d78e623593a774c1b46f48e7a9bd62a`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no cambió. Issue #199 queda como referencia del alcance.

Checks exact-head: PMGM CI #1634 (run 36357065482), Showcase #931 (run 36357065425) y QA Installable #569 (run 36357065389), todos SUCCESS. Tras integración, Showcase/Pages #932 (run 36357277072) y QA Installable #570 (run 36357277061) SUCCESS. Frontend local: 215/215 pruebas, lint y build SUCCESS; backend build y suites (incluye PostgreSQL, S3 y ClamAV) pasan en CI.

Pages artifact #10943798335, digest `sha256:56674bd800e26ebd53a77d621a67a03eefc44108e126146d2dfcf76040497023`; evidencia visual #10943753398, digest `sha256:528efbe90622f557ef90fab61b11d25978ecb9e67227f4542060fa614a92511a`. `qa-current.json` y ZIP descargado de Pages identifican `sourceSha=d455d454dff0e8cb135ca8d21c04bbd2242be38b`; archivo `Proyecto-Centenario-QA-srv01-d455d454dff0.zip`, SHA-256 verificado `b189f06728348a78df999d7f48f1bb08898d58e9a046f5d12da3e85656c08080`. QA Installable artifact #10943758277, digest `sha256:fcc17f55691aa078ed3a8d32fbb056275b63f6c7a8be55e9a763491eaa734954`; su workflow valida SHA/manifiesto para el mismo SHA integrado.

**Límites:** publicación Pages y artefactos no significan instalación ni UAT. `srv01` continúa pausado; no hubo instalación, smoke autenticado en servidor, regresión física ni UAT. Issue #97 conserva esos pendientes. Issues #190/#191 y PR #182 no fueron parte de este alcance. Especificación y criterios: `docs/PMGM-ARCH-017-ficha-taller-origen-y-permisos.md`, `docs/qa/PMGM-QA-V071-FICHA-TALLER-LOGO.md`.

## Mejora autorizada en implementación — Mis planchas y Biblioteca Virtual — 27-09-2026

El Hermano consulta sólo sus planchas, puede cargar y reemplazar su propio trabajo aunque la carga anterior la haya hecho Secretaría. Secretaría puede subir a nombre de un autor activo del Taller. Cada carga exige descripción breve y conserva versiones. Después de validación de integridad y escaneo antimalware limpio se publica automáticamente la nueva versión en Biblioteca Virtual → «Planchas de Trabajo», clasificada por el grado efectivo del autor. Si el proceso falla, sigue visible la última versión válida. El grado también se controla en listado, detalle y descarga directa, y las rutas genéricas no pueden eludir el flujo seguro. Se reutilizan el gestor documental y el catálogo por grado; no se alteran permisos generales, actas ni planchas oficiales.

Estado: integrada en `dev@49744d10472e739580f58e0752e211ce25f22655` por PR #185. Demo Pages y QA/Pre-UAT generados para el mismo SHA; no equivalen a instalación física ni aceptación QA/UAT. `srv01` sigue en pausa. Especificación: `docs/PMGM-ARCH-016-planchas-trabajo-personales-y-biblioteca.md`. Especificación: `docs/PMGM-ARCH-016-planchas-trabajo-personales-y-biblioteca.md`.

## Mejora autorizada — Resumen del Taller y acceso de consulta delegado — 27-09-2026

Se reemplaza la propuesta referencial «Libro de Oro» por el menú **Resumen del Taller**, reutilizando la vista existente de Ficha de Taller y sus proyecciones actuales. La vista corresponde a los ocho cargos que integran constitucionalmente el Consejo de Administración, sólo dentro de su Taller. El Venerable Maestro puede otorgar o revocar manualmente permiso de consulta a otro Maestro con membresía activa en ese mismo Taller.

La delegación requiere fundamento y auditoría; no incorpora al destinatario al Consejo ni otorga edición, firma, autorización o acceso a otros módulos. En cada lectura se comprueban nuevamente el grado de Maestro, la membresía activa y el Taller del permiso. Un cambio de grado o baja suspende su efecto y la delegación se conserva para revocación e historial. `lodge_admin` y el administrador de plataforma no sustituyen al Venerable en esta gestión. Esta delegación es una decisión funcional del Sponsor y no se atribuye como facultad textual de la Constitución.

## Corte integrado — PR #183 / Resumen del Taller — 26-09-2026

PR #183 se fusionó por squash en `dev@f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`, con aprobación del Sponsor. La implementación reutiliza Ficha de Taller para el menú Resumen del Taller, da lectura acotada al Consejo y permite al Venerable del Taller delegar/revocar consulta a un Maestro activo de su Taller. No cambia permisos de otros módulos ni otorga voz o voto al delegado.

Gates post-merge sobre el SHA exacto: PMGM CI #1598 (run 36283920282), Showcase/Pages #882 (run 36283920245), QA Installable #520 (run 36283920204) y Pre-UAT #374 (run 36283920260): SUCCESS. `qa-current.json`, BUILD-INFO y checksum del paquete publicado señalan el mismo SOURCE_SHA `f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`; MANIFEST valida 762/762 entradas en Pages y QA Actions. SHA-256 del ZIP indicado por `qa-current.json`: `2b0278074b0d36046c74eb7a0437b2325e3154635fab2dd3c160e235cf0044bf`. Los artefactos Pages/QA/Pre-UAT y sus digests quedan en PMGM-BASE-001 y Issue #97.

Demo Pages publicada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/. `srv01` sigue en pausa; no hubo instalación, smoke en servidor, regresión física ni UAT. CI/Pages/artefactos no significan QA/UAT aceptadas. Issue #97 permanece abierto; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no se modifica.

## Decisión vigente del Sponsor — 25-09-2026

El Sponsor / Product Owner indicó que `srv01` **queda pendiente y no se montará hasta nuevo aviso**. Issue #97 continúa abierto; la pausa no constituye aceptación de QA/UAT ni autorización de promoción a `main`. Mientras siga vigente, no se inicia instalación, despliegue, smoke autenticado, regresión física ni UAT en `srv01`.

Esta instrucción reemplaza cualquier paso operativo más antiguo de este documento que indique montar o desplegar ahora. Cuando el Sponsor levante la pausa, retomar desde el HEAD vivo de `dev` y comprobar Pages, `qa-current.json`, `BUILD-INFO`, checksum y `MANIFEST` antes de planificar el despliegue. Mientras Issue #97 siga abierto, no iniciar otro incremento funcional salvo autorización expresa del Product Owner para un alcance concreto.

## Verificación de continuidad — 25-09-2026

Último corte funcional: `7aba5c9f43df49d2d6d47bf2f522913dfa8863d3`; HEAD actual de `dev`: `bdcfa6efa12dd7ab6cf167302506121082a40dcb` (PR #164 sólo actualizó documentación); `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sigue intacta.

PR #162 dejó sincronizados los cargos y abreviaciones en la Línea Base Maestra de Drive y perfiles. PR #163 incorporó en Secretaría → Insinuados el alta de postulante y apertura de expediente privado en borrador, con continuidad en la ficha vigente, permisos acotados al Taller, control de duplicidad y bloqueo de reingreso antes del año reglamentario cuando existe rechazo pertinente.

PR #163 se integró por squash. En el SHA integrado pasaron los gates post-merge: PMGM CI #1541, Showcase/Pages #805, QA Installable #443 y Pre-UAT Installable #355. Pages y los paquetes corresponden al mismo SHA; no acreditan despliegue físico ni aceptación UAT.

Issue #97 continúa abierto: faltan instalación del SHA exacto en `srv01`, verificación de checksum/manifiesto, smoke autenticado, regresión institucional y UAT. `main` no debe promoverse hasta que el Product Owner acepte formalmente el corte.

**Siguiente paso operativo:** mantener la pausa de `srv01` hasta nuevo aviso del Sponsor. No instalar ni desplegar por ahora. Cuando se reactive, verificar el HEAD vivo de `dev` y la paridad Pages / `qa-current.json` / BUILD-INFO / checksum-Manifiesto, y continuar Issue #97 con evidencia; no iniciar otro incremento funcional sin autorización expresa para un alcance concreto.

## Mejora autorizada — Reportes de control y auditoría contable de Tesorería del Taller — 25-09-2026

El Product Owner autorizó ampliar los reportes del módulo existente. Se mantiene el libro único, la separación de pagos de cuotas, y la regla de que sólo egresos aprobados afectan caja. La implementación suma trazabilidad por movimiento (ID, sujeto y marca temporal UTC de registro y, cuando exista, de autorización), presentación explícita del resultado de cuadratura y pendientes, y exportación CSV con escape de entradas tipo fórmula. No se agregan monedas ni atribuciones institucionales nuevas. La revisión de aprobación conserva el permiso actual del Venerable Maestro y los reportes siguen restringidos al Tesorero del Taller.

La mejora quedó registrada en el PR #167 contra `dev@12b87c2e56ee99d7f96fdfd491b85b7dff67ea17`, con QA-040 y el gate de regresión actualizado a 40 controles. El PR registra los resultados de frontend y los gates automáticos sobre el HEAD exacto vigente; la integración en `dev` requiere completar el flujo del PR. `srv01` sigue pausado por instrucción del Sponsor. Issue #97 y UAT no se consideran aceptados.

## 1. Hito operacional bloqueante vigente: QA srv01

Issue #97 continúa abierto y la QA física en `srv01` está expresamente diferida por decisión del Sponsor hasta nuevo aviso. La pausa deja pendiente el gate de QA/UAT; no se monta el servidor ni se promueve `main` mientras siga vigente.

El corte operativo debe usar siempre el HEAD vivo de `dev` y verificar paridad:

`HEAD dev = Pages = qa-current.json = BUILD-INFO del ZIP QA`.

Orden de reanudación, sólo cuando el Sponsor levante la pausa:

1. tomar HEAD vivo de `dev`;
2. verificar Pages + `qa-current.json` + instalable;
3. desplegar el SHA exacto en `srv01`;
4. validar SHA-256, BUILD-INFO y MANIFEST;
5. ejecutar smoke autenticado;
6. ejecutar regresión QA completa;
7. corregir P0/P1;
8. congelar candidato UAT;
9. ejecutar UAT institucional;
10. promover a `main` sólo con aprobación expresa.

## 2. Gate vigente — cierre de QA antes de un nuevo incremento funcional

La Línea Base Maestra LB-PC-2026-09-17 establece que no se inicia un nuevo incremento funcional mientras Issue #97 mantenga bloqueada la QA física, salvo decisión expresa del Sponsor / Product Owner para un alcance concreto. Para este corte, el Product Owner autorizó expresamente ampliar el menú y los flujos de Tesorería del Taller; el alcance autorizado queda documentado en `PMGM-ARCH-012`. El despliegue físico en `srv01` sigue pendiente: un workflow o artefacto generado no cierra Issue #97 ni sustituye pruebas en el servidor.

## 3. Funciones integradas que no son trabajo futuro

El alcance autorizado de Tesorería del Taller añade los menús Ingresos y Egresos, Configuraciones y Reportes, expande Resumen y Cuotas/Cobranzas, y conserva Cuadro mensual. Revisa `PMGM-QA-V066` para caja de apertura, recibos de cuota contabilizados una sola vez, autorización obligatoria de egresos y cuadratura. La aceptación física se incorpora como QA-037 y continúa pendiente de Issue #97.

- Secretaría integral: PR #105.
- Insinuaciones: PR #106.
- Cierre documental de Tenidas: PR #110.
- Cuadro Mensual de Tesorería: PR #112.
- Continuidad Tesorería: PR #113.
- Hospitalaria integral Taller + Gran Hospitalaria: PR #114.

## 4. Último incremento integrado — PR #114

PR #114 `feat(hospitalaria): flujo integral Taller y Gran Hospitalaria` quedó integrado.

Corte funcional:

- merge SHA: `44b6cc90a56e508c8c45e85d040e928738e1b5d8`;
- CI post-merge: `success`;
- Showcase/Pages: `success`;
- QA Installable: `success`;
- Pre-UAT: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-44b6cc90a56e.zip`;
- SHA-256: `c5474a135c879e7df99768bc19130f7774e0583b2e8f06560e99f11bf2d2a6e6`;
- matriz srv01: **QA-001..QA-025**.

Reglas vigentes:

1. Tronco de Beneficencia independiente de Tesorería;
2. Hospitalario gestiona caja/aportes/socorros/rendición de su Taller;
3. Secretaría no administra Hospitalaria;
4. Venerable lee/inspecciona y aprueba egresos sin editar movimientos;
5. Consejo autoriza socorros sólo mediante acuerdo real `benevolence_aid_proposal`, aprobado, mismo Taller y monto exacto;
6. estado mensual exige revisión Hospitalaria del Consejo para el mismo Taller y período;
7. rendición bloqueada con egresos pendientes;
8. reposición pagada requiere referencia/comprobante;
9. Gran Hospitalaria recibe sólo agregados y referencias institucionales;
10. proyección superior excluye beneficiario, `memberReference`, destino y observaciones privadas;
11. Gran Hospitalaria observa o concilia;
12. conciliación genera regularidad institucional consumida por Ceremonias;
13. demo reproduce Hospitalario/Venerable/Gran Hospitalaria;
14. QA-025 protege flujo y privacidad.

Fuente: `docs/PMGM-ARCH-011-hospitalaria-flujo-integral.md`.

## 5. Siguiente incremento después del cierre de Issue #97

La Línea Base Maestra señala como siguiente foco funcional el expediente de insinuación y sus transiciones reglamentarias. Este bloque sólo se inicia después de cerrar QA/UAT de Issue #97 o recibir una decisión expresa del Sponsor / Product Owner que autorice un alcance concreto antes de ese cierre.

Antes de programarlo:

1. consultar el HEAD vivo de `dev` y `main`;
2. leer Estado Maestro, START-HERE, AGENTS, ADRs, pruebas y PRs recientes;
3. revisar la normativa vigente y la Línea Base Maestra en Drive;
4. auditar el código integrado de Insinuaciones y el estado de PR #116 para separar funciones implementadas, pendientes y candidatas a sincronización;
5. no duplicar flujos existentes ni fusionar PR #116 por sus gates automáticos únicamente;
6. documentar el alcance residual y sus criterios QA/UAT antes de crear la rama.

La revisión actual confirma Issue #97 abierto y PR #116 en draft, con base histórica anterior a `dev`. No se inicia aquí trabajo funcional de esa rama.

## 6. QA vigente

La línea integrada de PR #114 deja **QA-001..QA-025**.

QA-025 valida:

- independencia del Tronco;
- autorización Venerable/Consejo;
- revisión mensual del Consejo;
- privacidad Taller/Gran Hospitalaria;
- rendición agregada;
- reposición;
- conciliación;
- regularidad institucional.

Issue #97 y Línea Base Maestra registran el corte funcional. Cualquier commit documental posterior cambia el HEAD vivo y obliga a regenerar/verificar Pages y el instalable del mismo SHA antes del despliegue físico.

## 7. Incremento referencial autorizado — Tesorería y Mi ficha

El 21-09-2026 el Product Owner autorizó aplicar las mejoras compatibles identificadas en el sistema logial de referencia. El primer incremento extiende el candidato de Tesorería PR #121 con:

- cartola personal de cuotas y comprobantes en Mi ficha;
- separación explícita entre período de la obligación y fecha efectiva de pago;
- totales cargado, pagado y saldo propios;
- control de reintento duplicado cuando coinciden cargo, fecha, monto, medio y referencia;
- datos ficticios equivalentes en GitHub Pages y prueba QA-027.

No se incorporan cambios de atribuciones en Secretaría: el circuito de insinuaciones, ceremonias, documentos PDF firmados y cierre documental aprobado permanece vigente. Las mejoras posteriores de cierre/apertura anual, proyección y recordatorios se mantienen como trabajo incremental, no como autorización para cambiar la normativa ni generar documentos oficiales dentro del sistema.

El mismo bloque incorpora QA-028 para el **Cuadro Logial Mensual**: Gran Tesorería ve inicialmente el consolidado por cuota normal, tercera edad, estudiante, cónyuge y Past Activo, con cantidad y monto por línea y total del mes. Los datos individuales permanecen minimizados y sólo se consultan expresamente para resolver diferencias. El Taller queda al día únicamente después de conciliar el pago íntegro del total exigible.

El 22-09-2026 se añade QA-029 y PMGM-ARCH-012 para la navegación por cargo. Tesorería del Taller y Gran Tesorería conservan un solo acceso principal cada una y agrupan internamente sus tareas. El Venerable sólo recibe la vista de egresos por autorizar. Este cambio reorganiza la experiencia sin ampliar permisos ni alterar las reglas financieras.

La inspección de la evidencia visual de QA-029 detectó y corrigió un recorte horizontal en la vista móvil de autorización del Venerable. El cierre del candidato exige volver a generar CI, Showcase e instalable desde el mismo SHA y comprobar la captura `tesoreria-autorizacion-venerable-390x844.png` antes de solicitar publicación temporal en Pages.

## 8. Incremento activo — navegación de Secretaría por cargo

Desde `dev` `822fd25b88caafdf5a01d89c27c53b6cd000f183` se inicia el incremento PMGM-ARCH-013 / QA-030:

- una entrada lateral **Secretaría** para Secretaría del Taller;
- una entrada lateral **Gran Secretaría** para Gran Secretaría;
- navegación interna hacia todas las funciones vigentes del cargo;
- eliminación visual de accesos duplicados, sin eliminar pantallas ni contratos;
- preservación de permisos, privacidad y terminología institucional;
- documentos oficiales registrados mediante descripción y PDF firmado físicamente, nunca generados o firmados por el sistema.

Estado inicial de rama: 153/153 pruebas frontend y build productivo aprobados. Falta commit, PR a `dev`, CI exact-head, Showcase, instalable QA y revisión funcional en Pages. La QA física de `srv01` continúa diferida.

## 9. Corrección P0 de permisos visibles — Tesorero / Secretaría

QA-031 fija que administrar Tesorería no habilita Gestión Logial ni funciones de Secretaría. El Tesorero del Taller mantiene exclusivamente su espacio financiero y las vistas transversales comunes; no ve ni abre Tenidas, actas, correspondencia, pendientes, expedientes de insinuación, Circuito de Iniciación, Cuadro del Taller, Ficha del Taller o Gestor Documental general.

El backend ya aplicaba esta separación; la corrección alinea el perfil demostrativo y la navegación del frontend con la autoridad real de la API. Debe validarse en Pages con el perfil **Tesorero del Taller** y conservarse mediante QA-031.

## 10. Incremento activo — Insinuados publicados con foto protegida

Desde `dev` `fc019fb70e2f2df610ae3c27e6d8194ddf801864` se inicia QA-032 para la vista general de insinuados publicados:

- conservar la lista transversal de publicaciones vigentes para hermanos autenticados;
- mantener la proyección minimizada: nombre, Taller, fechas y regla de publicación;
- entregar `photoUrl` sólo si existe fotografía vinculada a la ficha privada;
- mantener la fotografía detrás de la ruta protegida `/api/candidate-publications/{publicationId}/photo`;
- evitar exponer rutas internas, versión documental o datos privados del expediente.

El incremento no modifica atribuciones de Secretaría, Gran Secretaría ni Tesorería, y no cambia la regla de que el expediente completo queda restringido por rol.

## 11. Incremento activo — identidad institucional del Sistema

Se alinea la configuración de identidad visual de Sistema con la Línea Base Maestra LB-PC-2026-09-17:

- azul institucional `#06148E`;
- azul complementario `#004AD4`;
- dorado `#F3C609`;
- dorado fuerte `#FBAE17`.

Integrado en `dev` mediante PR #132, merge SHA `e1eff39fa380144ef69e200010ced86c787e23a4`. CI #1423 y CI post-merge #1424 SUCCESS; Showcase #658 y publicación Pages #659 SUCCESS; QA Installable #296 (PR) y #297 (dev) SUCCESS; Pre-UAT #320 SUCCESS. Artefacto QA del SHA integrado: `proyecto-centenario-qa-srv01-e1eff39fa380144ef69e200010ced86c787e23a4`, SHA-256 `b8101f28c16af42da24d68d4d20f6a4575b3d0d921742fe5f041c3c8c3c771ae`, expira el 22-10-2026 21:30 UTC. Pages fue desplegado desde el mismo SHA. La guía oficial del logotipo exige no deformar, recolorear ni recortar el archivo; se conserva su referencia configurable. Issue #97 y despliegue físico en `srv01` continúan pendientes.

## 12. Resolución de continuidad — prioridad del Issue #97

El 22-09-2026 se reconcilia este documento con la Línea Base Maestra: queda sin efecto la autorización genérica anterior para abrir incrementos funcionales mientras Issue #97 siga bloqueando QA. Sólo una decisión expresa del Sponsor / Product Owner que identifique el alcance habilita una excepción. La prioridad vigente es completar QA/UAT física cuando se reactive `srv01`; después se revisa el flujo de insinuaciones contra el código actual antes de proponer trabajo residual.

## 13. Fortalecimiento del gate Demo para QA-031

Se amplía el script `.github/scripts/capture-showcase-views.mjs` para comprobar en navegador que el perfil Tesorero conserva sus accesos transversales y las seis funciones propias de Tesorería, y no recibe accesos de Secretaría/Gestión Logial. En el recorrido del Venerable se comprueba que sólo figure **Egresos por autorizar**. La validación corre antes de guardar las capturas de escritorio y móvil y falla Showcase ante cualquier divergencia.

Este ajuste automatiza un control del alcance QA-031/v0.59; no añade ni modifica capacidades funcionales. El despliegue físico en `srv01` y la aceptación QA/UAT siguen pendientes según Issue #97.

## 14. Incremento autorizado — logotipo oficial en la plataforma

El Product Owner solicita integrar el logotipo oficial de la Gran Logia Mixta de Chile junto a la identidad corporativa de Proyecto Centenario. El recurso fuente es `Logo Gran Logia Mixta de Chile.svg` en Drive, ID `1_BLXseShbQX-ioGMNLd5xKPYGtLMEvFm`; la guía institucional exige fondo blanco, proporciones originales, área de protección y no recortar ni recolorear la marca.

El incremento v0.62 sustituye la “C” provisional de la cabecera por el SVG oficial y refuerza Showcase para comprobar carga, proporción, fondo y encuadre no superpuesto en móvil/escritorio. La paleta v0.61 se conserva. El PR #137 quedó integrado por squash en `e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`.

Gates exact-head sobre el merge SHA: CI #1436, Showcase/Pages #676, QA Installable #314 y Pre-UAT #329 SUCCESS. Demo y ZIP se generaron desde el mismo SHA. QA-062 registra la verificación live y digests. `main` sigue intacta (`6dfb9546a4873baff15955cf86abfd7d47e3d111`). La excepción autorizada cubre sólo la incorporación del logo; no cambia la decisión de mantener pendiente la QA física ni autoriza promoción a `main`.

Artefacto QA #10723132819: `proyecto-centenario-qa-srv01-e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`, digest `sha256:a42964269eb9af176feacfd3f0f546b51072647fb828b8af062b2bded180497b`, vence el 22-10-2026 22:55:08 UTC. Pages artifact #10723212264, digest `sha256:1be0343b760f6f65d5acfcf066582d56a6e4c76638955bf0eefb73cf44df375c`, vence el 23-09-2026 22:56:18 UTC.

## 15. Próximo punto de continuidad

Issue #97 sigue abierto: despliegue físico diferido en `srv01`, verificación `SOURCE_SHA`/`MANIFEST`, smoke autenticado, QA-001..QA-034, UAT y evidencia. No iniciar otro incremento funcional mientras siga el bloqueo, salvo decisión expresa del Product Owner con alcance concreto. Al retomar, consultar HEAD de `dev` y `main`, START-HERE, Drive y esta sección; no reutilizar artefactos expirados.

### Continuación autorizada por el Product Owner — 24-09-2026

El usuario indicó continuar, con autorización previa permanente para desarrollar mejoras y el siguiente punto recomendado por la Línea Base Maestra de Drive. Se abrió `feature/ceremony-rights-ledger` desde `dev@d7b931b323c191733a09f392c74759f7994e844b`. Alcance: pago/conciliación del derecho ceremonial por expediente y efecto bloqueante en elegibilidad. Ver `docs/qa/PMGM-QA-V070-DERECHOS-CEREMONIALES.md`; QA-039 amplía el kit a 39 controles. Mantener `main` y srv01 sin cambios.


## 16. Incremento funcional expresamente autorizado — Hospitalaria / reposiciones

Aunque Issue #97 mantiene pendiente QA física, el Product Owner autoriza este alcance concreto: reposición de $1.500 por hermano activo del Cuadro ante defunción; tarifa por vigencia; cobro individual trazable del Hospitalario; transferencia total a Gran Hospitalaria; conciliación/visto bueno y estado de regularidad requerido en Ceremonias; cuota de cónyuge referencial de $15.000 mensuales, configurable en Tesorería. El monto de aporte a Gran Tesorería se ingresa por separado según cuadro oficial vigente.

Rama en trabajo: feature/hospitalaria-death-replenishment-spouse-fee, desde dev@f90e6af186a1fe81f39ac4a77b6f26fd8721a017; main sigue en 6dfb9546a4873baff15955cf86abfd7d47e3d111. Cambios iniciados incluyen persistencia/migración, API, demo y UI; las transferencias observadas admiten reenvío numerado con historial íntegro. QA-033 se define en docs/qa/PMGM-QA-V063-HOSPITALARIA-REPOSICION-CONYUGE.md.

Antes de abrir PR: terminar pruebas, revisar migración, registrar resultados y conservar main intacta. El entorno no tiene .NET SDK; el CI exact-head debe compilar backend y ejecutar integración PostgreSQL. Después: PR a dev, CI exact-head, Showcase/Pages, instalable QA y Pre-UAT del mismo SHA. Despliegue físico srv01, smoke, regresión y UAT permanecen sujetos a Issue #97; no promover a main.


### Seguimiento de reparación PR #139 — 23-09-2026

- Restaurar íntegramente `frontend/src/api/pmgmApi.ts` desde `dev` y reaplicar sólo tipos, métodos, comportamiento sintético y agrupación de reposiciones.
- Corregir las nueve declaraciones FK posicionales de la migración a argumentos explícitos y eliminar `SubmissionNumber` del mapeo de `TreasuryPayment`; conservarlo en `DeathReplenishmentTransfer`.
- Resultado local: 185/185 pruebas frontend, lint, build, diff-check y migration gate 44 migraciones aprobados.
- Pendiente: crear commit reparador sobre PR #139, esperar CI de backend y gates Showcase/Pages, QA Installable y Pre-UAT sobre el mismo SHA; actualizar este checkpoint con los resultados reales. `main` intacta; Issue #97/srv01 y UAT siguen pendientes.

- Hallazgo CI #1440 posterior al primer commit reparador: error de compilación C# CS0118 para `Membership` y `SubmissionNumber` en entidad Obligation en vez de Transfer. Corregir, repetir CI #1440 en nuevo HEAD y revisar gates restantes. El primer intento Showcase y QA Installable están corriendo sobre el SHA `05049a68a015dac2de584aef6b6f8d101fa81d1f`; no usarlo para publicación hasta que backend pase.

- CI #1441: compilación API SUCCESS; Privacy/Data classification/Migration safety SUCCESS; PostgreSQL integration falla al generar SQL para `InsertData` de tarifa por no existir `TargetModel` en las migraciones manuales. Se reemplaza por SQL explícito `INSERT/DELETE`; repetir integración completa y gates del nuevo SHA.


### Checkpoint corregido — PR #139 (23-09-2026)

Último SHA observado de la rama: `b1bf40c308b2c2ce24dbcd8669d4aca8344b8a22`. CI #1442, Showcase #684 y QA Installable #322 SUCCESS sobre el SHA exacto. Local: 185/185 frontend, lint, build y migration gate 44 migraciones aprobados. CI encontró y se corrigió la carga inicial de tarifa mediante SQL explícito; CI #1442 confirmó pruebas backend/PostgreSQL, smoke HTTPS/OIDC y recuperación. PR #139 sigue abierta en borrador. Pages no se desplegó desde el evento PR; Pre-UAT se ejecuta al integrar en `dev`. Antes de mergear, registrar este checkpoint y repetir gates exact-head; luego verificar Pages, QA instalable y Pre-UAT del SHA integrado. `main` intacta; `srv01`/QA/UAT pendientes por Issue #97.


## 17. Nuevo checkpoint — corrección Tesorería Decreto 1.759 (23-09-2026)

El Product Owner prioriza cerrar Tesorería antes de seguir con los demás módulos. Rama local/remota: `feature/treasury-decree-1759`, basada en `dev@dcb6b169f59da7649012f3d2e8fd20e5313ad3f8`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece intacta. La autoridad institucional clasifica el Oriente del Taller. Gran Tesorería fija el componente institucional según Decreto 1.759 (vigente 01-01-2026); Tesorería del Taller parametriza su total local por tipo/vigencia y conserva la diferencia. No aceptar montos institucionales editados ni cuota local menor al aporte oficial. Past Activos no generan aporte Gran Tesorería; cualquier cuota local para ellos es separada.

Tarifas: ordinario Santiago/otros Orientes $21.000/$15.000; cónyuge $13.000/$10.000; tercera edad $10.000/$8.000; estudiante $8.000/$8.000. Perú ordinario USD 6; el sistema aún es CLP y debe bloquear sin conversión automática. Derechos ceremoniales listados en el catálogo; integración pago/conciliación por solicitud sigue pendiente. Cuotas de cesantía existen en el decreto y no se asignan automáticamente en este incremento.

Implementación local incorpora API, UI/demo, migración de Oriente, reglas de cálculo, exclusión de Past Activos y aceptación QA-035. Completar validaciones y revisión de exact-head, publicar commits en la rama GitHub, abrir PR contra `dev`; ejecutar CI, Showcase/Pages, instalable y Pre-UAT. Falta .NET SDK local; backend y PostgreSQL dependen de CI. Actualizar este checkpoint con SHA/PR/gates exactos. No tocar/promover a `main`; QA física `srv01` y UAT pendientes por Issue #97.

PR #140 ya abierta. El primer SHA `bdfe1e2fba64d5d5529b62abca805ffc2aea337f` tuvo un archivo `pmgmApi.ts` truncado durante la publicación por exceder el límite de salida de una sola lectura. Se reconstruyó por bloques y se comprobó hash local/remoto idéntico. SHA reparado `83379537f508302855b7b6a797b79b4a2a49b2b7`; nuevos gates CI #1446, Showcase #689 e instalable #327 pendientes. No considerar los resultados rojos del SHA truncado como validación del código reparado, ni fusionar antes del exact-head.

Luego CI #1448 sobre `5fcd5aec…` falló una prueba de minimización del DTO general de organizaciones (296/297 backend tests pasaron); Showcase #691 e instalable #329 SUCCESS, primer smoke autenticado y piloto SUCCESS. Se corrigió trasladando las lecturas territoriales a endpoints de Tesorería con permisos separados y devolviendo el DTO general a sus cuatro propiedades aprobadas. Local tras este cambio: tests frontend 186/186, lint/build y gates JSON/migration SUCCESS. Publicar el ajuste y esperar gates exact-head frescos antes de integrar.

CI #1449 repitió la misma aserción: el archivo ya corregido no se transfirió porque coincidía con `HEAD` local y quedó fuera del conjunto de archivos modificados, aunque la rama remota conservaba el campo territorial. Forzar la restauración de `OrganizationEndpoints.cs`, verificar el blob local/remoto y ejecutar CI #1450 o el siguiente número, Showcase e instalable nuevamente sobre un solo SHA.


## 18. Tesorería cerrada en dev — PR #140 (23-09-2026)

SHA integrado: `7747eab76f339395efa3356e92017c19f5abb0f7`; PR #140 fusionada por squash. Exact-head gates post-merge: CI #1451, Showcase/Demo/Pages #695, QA Installable #333 y Pre-UAT #332, todos SUCCESS. `main` permanece intacta en `6dfb9546a4873baff15955cf86abfd7d47e3d111`. QA srv01 física y UAT siguen pendientes por Issue #97.

Quedó corregido: tarifas Decreto 1.759 desde 01-01-2026, clasificación institucional de Oriente con rutas RBAC, minimización intacta del DTO general de organizaciones, aporte Gran Tesorería separado de cuota local, margen del Taller y cónyuge a valor de referencia local Santiago $15.000 (oficial GT $13.000). Past Activos no componen el aporte institucional. QA-035 queda listo para ejecución física (plantilla 35 controles). Perú USD 6 no se convierte ni se factura aún en el flujo CLP; cesantía automática y conciliación del pago ceremonial por expediente no están implementadas.

Continuación funcional sugerida: primero enlazar el pago/verificación del derecho ceremonial con el expediente y su elegibilidad; después diseñar moneda Perú y cuotas por cesantía conforme al decreto. Mantener `main` sin cambios y no afirmar regularidad UAT/srv01 hasta la verificación física.


## 18. Incremento en curso — alineación del Cuadro de Tesorería con Excel oficial

Base al inicio: `dev@8d06c8e684cd79047202971bd1c20244e7d1eecd`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, intacta. Rama: `feature/treasury-statement-excel-alignment`. Fuente Drive: **CUADRO PAGO GRAN TESORERÍA.xlsx**, ID `1nPEZsVr5QPNS-Z_SBmjEs33wjfqsUNTN`.

La nómina se organiza en Maestros, Compañeros y Aprendices con RUT, nombre y apellidos, grado, cargos abreviados, tipo/valor de cuota y respaldo. Cónyuge, estudiante y tercera edad requieren referencia a Plancha de autorización; generación, ingreso manual y envío del Cuadro no deben aceptar estas categorías sin respaldo. El detalle personal se entrega a Tesorería del Taller o por consulta autorizada; Gran Tesorería mantiene agregación inicial y minimización.

Implementación en rama: API detallada con RUT/nombres, snapshot de cargos activos al corte, columnas alineadas y datos ficticios para Pages. QA-036 registra aceptación física futura. Local: frontend 188/188, lint/build SUCCESS; JSON y gate QA de 36 controles SUCCESS. PR #142 abierta hacia `dev`; primer SHA publicado `808687b9146fcc5628c35b6feeedd6c2c9f0dffe`. CI #1454, Showcase #699 e instalable QA #337 SUCCESS sobre ese SHA. El despliegue de Pages se omite en PR y Pre-UAT no tiene disparador pull_request; confirmar ambos flujos para el SHA integrado. No hay .NET SDK local; backend/PostgreSQL depende de CI. QA física/UAT continúan pendientes por Issue #97. `main` intacta.


El incremento funcional autorizado añade cierres anuales inmutables, arrastre de saldo, reporte Debe/Haber/Neto y listado filtrable/exportable; revisar `PMGM-QA-V067` y QA-038. Se mantiene pendiente la validación física de QA por Issue #97.

## 20. Ajuste responsive — tabla de Cuotas y Cobranzas

El Showcase de `dev@8522b0dfd3e9db6eb964f6fdce5e8479c2a3525c` mostró un defecto que las comprobaciones anteriores no detectaban: los rótulos auxiliares de tarjeta aparecían junto a cada valor en escritorio y comprimían la tabla. La intención aprobada se conserva por tamaño de pantalla:

- Hasta 900 px: seis rótulos y seis valores visibles dentro de tarjetas; acción de pago táctil y sin desbordamiento.
- Sobre 900 px: los seis encabezados nativos de tabla y sus valores; rótulos auxiliares ocultos.

Se actualiza `PMGM-QA-V069` y la aserción de Showcase para validar la composición correcta a 390×844, 768×1024 y 1440×900. El cambio es únicamente visual; no modifica montos, permisos, cálculos ni persistencia. Pruebas frontend 196/196, lint y build pasan; CI #1517, Showcase/Pages #773, QA Installable #411 y Pre-UAT #347 SUCCESS sobre el merge SHA `eb9ca2f75523937b6871bcd24b23a7dd8a4b098a`. Pages `qa-current.json` identifica ese mismo SHA y el ZIP `Proyecto-Centenario-QA-srv01-eb9ca2f75523.zip` (SHA-256 `2ac85751b88218ef525bd602cd5f2526cfcdd49cc331e1d15023197173cec320`). PR #155 integrada. QA física/UAT continúan pendientes en Issue #97.


## Continuidad de PR #168 — 26-09-2026

PR #168 integró a `dev` el estado post-PR #167 como `85fc5a5816910ae9c477a0aecc1121aca21429b8`; CI/backend, lint/build, showcase build e instalable terminaron SUCCESS sobre el HEAD exacto del PR. El Pages del PR omitió publicación; el estado post-merge Pages/`qa-current.json` del HEAD integrado debe confirmarse aparte. `srv01` no se monta hasta nuevo aviso.

## Corte integrado PR #169 — evidencia histórica de cuadratura de caja

El Product Owner amplió el alcance de auditoría contable de Tesorería del Taller. Se revisó el Sistema Logial de referencia: se adoptan los reportes Debe/Haber/Neto, respaldos y controles compatibles; se descartan como fuente de cálculo el semáforo duplicado frente al Cuadro institucional y la proyección que supone cobranza íntegra. La conciliación mensual de Gran Tesorería continúa separada.

PR #169 se integró por squash como `2db812f37c2080d166b74692f0118f566879138c`, desde `dev@85fc5a5816910ae9c477a0aecc1121aca21429b8`. El servidor calcula cada conciliación por rango bajo una transacción RepeatableRead y guarda saldos, diferencia, recuento de movimientos, referencia/nota opcional, actor y UTC como nueva evidencia inmutable. QA-041 queda pendiente para ejecución física. No cambian permisos, aprobaciones, monedas ni libro de movimientos. Frontend 199/199, lint/build y gates locales SUCCESS. Post-merge PMGM CI #1559 (run 36205450888), Showcase/Pages #829 (run 36205450889), QA Installable #467 (run 36205450901) y Pre-UAT #361 (run 36205450882) SUCCESS. `BUILD-INFO` y `qa-current.json` apuntan al SHA integrado; los manifiestos del ZIP Actions y Pages verifican. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sigue intacta. El siguiente paso es retomar Issue #97 únicamente cuando el Sponsor autorice montar srv01; entonces verificar el paquete vigente, instalar el SHA exacto, ejecutar smoke autenticado, regresión institucional y UAT con evidencia. Hasta entonces no se monta srv01 ni se declara QA/UAT aceptada.

## Mejora autorizada — identidad visual institucional (25-09-2026)

El Product Owner autorizó aplicar en el frontend los criterios de la Guía de uso del logotipo GLMCh y de la plantilla oficial de papelería. El cambio actualiza tema global, módulos y pruebas de contrato; sustituye colores/fuentes anteriores, conserva la composición responsive y el logo SVG original, y mantiene contraste accesible en texto sobre acentos dorados. Referencia de implementación: `docs/ui/PMGM-UI-001-identidad-visual-responsive.md`.

Rama `feature/institutional-brand-system-20260926`, creada desde `dev@c2d49728179a10f163cd6fef8d9f8a07f81754de`. Tras pruebas y CI exact-head, integrar en `dev`, actualizar Pages y QA instalable y registrar el SHA. Esto no levanta la pausa de `srv01`: no instalar, desplegar, ejecutar smoke/regresión física ni UAT. Issue #97 sigue abierto, QA/UAT no están aceptadas y `main` permanece intacta.


## Corrección autorizada — iconos dorados en mosaicos azules — 26-09-2026

PR #172 corrigió el contraste de iconografía en el portal de miembros: se conservan los fondos azules institucionales y se fijan en dorado los símbolos de Tesorería y Hospitalaria y los iconos de llamados sobre azul. No cambia la numeración del calendario, los colores semánticos ni el texto azul marino sobre botones dorados. Una prueba de regresión verifica el contrato de color.

Integrado por squash en `dev` como `9931df6c8b2199f0c2b17edf1d31f6fa2f762886`, desde PR #172. Checks exact-head SUCCESS: PMGM CI #1573, Showcase/Pages #846, QA Installable #484 y Pre-UAT #364. El artefacto QA Actions #10896758099 tiene digest externo `sha256:6c604107882f69834a71873200aba95b53d7b390a01b3bfda614c0f47f914d31`; BUILD-INFO identifica el mismo SOURCE_SHA y MANIFEST valida 754/754 entradas. Pages #10896848056 contiene `qa-current.json` con el mismo SHA y el ZIP publicado con SHA-256 `b4b22f6bdd2301997d145aee7e6915284c762ec190e2f73c079b291cb78e5039`; BUILD-INFO coincide y MANIFEST valida 754/754 entradas. Las capturas responsivas del run #846 incluyen `mi-ficha-1440x900.png` y muestran el símbolo dorado sobre mosaico azul.

`main` sigue en `6dfb9546a4873baff15955cf86abfd7d47e3d111`. `srv01` permanece pausado: no hubo instalación, smoke autenticado en el servidor, regresión institucional ni UAT; QA/UAT no se aceptan.


## Ajuste en curso — cobranza e imputación de cuotas

Requisito confirmado por el Product Owner y alineado con `Manual_Modulo_Tesoreria.md` de Drive: para registrar una cobranza se selecciona hermano, fecha efectiva de recepción, monto/medio y el año y mes de cuota por separado. La fecha de recepción conserva el período de caja/contable del ingreso; el cargo de cuota seleccionado determina la obligación que se reduce en la cartola e historial. No se registra un ingreso duplicado desde el libro manual.

La interfaz se actualiza en `LodgeTreasuryPanel`, se agrega selector dependiente de año/mes, se mejora la lectura móvil del historial y se amplían las pruebas del contrato visible. El POST conserva `paymentDate` y apunta al `chargeId` del período elegido; no se alteran reglas, permisos ni esquema backend. Integrado por PR #195 a `dev` como `624bcdce1d545e27da763573f9eaaead16ed5333`. PMGM CI #1624, Showcase #917 (incluido Deploy showcase), QA Installable #555 y Pre-UAT #383 terminaron SUCCESS para ese SHA. El `qa-current.json` del artefacto Pages confirma el mismo SHA; ZIP instalable `Proyecto-Centenario-QA-srv01-624bcdce1d54.zip`, SHA-256 `b1821668c7691afdbf96dfdba81e19ec8692a7f2fe8d13d54fa1e1445fa4f5cb`. Demo: `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`. QA física/UAT no ejecutadas; `srv01` continúa pausado y `main` intacta.


## Handoff post-merge — 27-09-2026

La cuenta propietaria fusionó PR #187, #189 y #192. HEAD verificado: `dev@316db1bf65507a0af25f8f9a391bda2064818e00`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, intacta. PMGM CI #1618 / run 36346169507 falló dos veces en “First implementation authenticated smoke” al descargar `public.ecr.aws/aws-cli/aws-cli:2.32.25`, HTTP 429 “Data limit exceeded”. Revisión del job: backend/tests, frontend, piloto y configuración de infraestructura SUCCESS. Showcase #908 SUCCESS; QA Installable #546 y Pre-UAT #380 SUCCESS como artefactos únicamente. No hay despliegue Pages confirmado en esta consulta, no hubo instalación ni UAT; `srv01` permanece pausado (Issue #97). Issue #188/#191 abiertos.

Se abre PR de corrección para usar la imagen oficial AWS CLI desde Docker Hub manteniendo la versión fijada, y para aclarar que una autorización del Product Owner ya dada no requiere un segundo aprobador de proyecto. Pendiente verificar CI exact-head; no fusionar con gates fallidos. Consultar protección real de GitHub en cada merge; no inventar revisión externa. `main` y `srv01` permanecen fuera de alcance.


## CIERRE POST-MERGE — PR #193 — 27-09-2026

PR #193 se fusionó por squash: `435a0a6870a7db645d98256a7b8fbdac411d307d`. HEAD vigente verificado: `dev@435a0a6870a7db645d98256a7b8fbdac411d307d`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambio. PMGM CI #1620 SUCCESS, incluido backend/tests, frontend, “First implementation authenticated smoke”, piloto HTTPS/OIDC/recovery e infraestructura. Showcase #911 SUCCESS con artifact Pages y Deploy showcase SUCCESS. QA Installable #549 y Pre-UAT #381 SUCCESS como artefactos. No hubo instalación/UAT; `srv01` permanece pausado (Issue #97). Issues #188/#191 siguen abiertos para sus pendientes.

PR #193 reemplazó en CI `public.ecr.aws/aws-cli/aws-cli:2.32.25` por la imagen oficial `amazon/aws-cli:2.32.25`; el smoke exact-head y post-merge pasó. Formalizó que no se exige aprobación de tercero para PR a `dev` cuando el Product Owner ya autorizó el alcance e indicó integrarlo si está listo. No pedir confirmación redundante; validar gates exact-head y bloqueos técnicos reales de GitHub. Checks verdes por sí solos no dan autorización. `main` y srv01 conservan decisiones separadas.

## Cierre post-merge — PR #196 — 27-09-2026

PR #196 quedó integrada por squash como `807954f2d79a81d5f9ec496b08cc267a4c407317`; actualiza este handoff después del merge de Tesorería PR #195. HEAD de `dev`: `807954f2d79a81d5f9ec496b08cc267a4c407317`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece intacta.

Para el SHA integrado, PMGM CI #1626, Showcase #920 (incluye Deploy showcase), QA Installable #559 y Pre-UAT #384 terminaron SUCCESS. El artefacto Pages #10942745527 registra digest SHA-256 `758f8b288cdfd99cdd2d36fdcabe23531f28a7a7e731bfd8af7c146a70827d42`; `qa-current.json` apunta al mismo SHA. El ZIP publicado es `Proyecto-Centenario-QA-srv01-807954f2d79a.zip`, SHA-256 `b3ef27dae9e9e83c3047ea89a2a1c8afc691f940ab667737d2a0bec69e6612da`; el artefacto QA Actions #10942047517 informa digest `sha256:843c6d8becc767292547477964244b8edbbde5ea15f6689e8d1fac31ce3b8689`.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/. La cobranza permite separar la fecha real de recepción del año/mes de la cuota imputada, conserva el historial por cargo y está validada en la vista móvil. Estos checks confirman automatización y publicación; no hubo instalación ni UAT física. `srv01` sigue pausado por Issue #97 y la aceptación QA/UAT continúa pendiente.
