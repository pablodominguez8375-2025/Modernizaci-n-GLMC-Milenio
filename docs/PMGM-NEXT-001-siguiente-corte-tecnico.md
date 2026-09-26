## Hallazgo visual confirmado — «Mi calendario» activo se veía azul — 26-09-2026

El estado activo «Mi calendario» podía verse azul translúcido al tocarlo en móvil o al mantener el puntero encima en escritorio. La causa confirmada es la mayor especificidad de `.sidebar .nav-item:hover:not(.disabled)` (`0,4,0`) frente a `.sidebar .nav-item.active` (`0,3,0`) en `institutional-theme.css` y `member-portal.css`; en pantallas táctiles el pseudoestado `:hover` puede persistir tras tocar el botón.

La corrección añade `:not(.active)` a ambas reglas hover, de modo que el acceso seleccionado mantiene fondo dorado institucional (`#F3C609` / `#FBAE17`) y texto/icono azul oscuro (`#06148E`), sin cambiar rutas, permisos ni lógica. Una prueba de contrato falló con la implementación previa y valida ahora que los selectores hover excluyan `.active`. Pendiente CI exact-head, aprobación del Sponsor, merge a `dev`, publicación Pages y artefacto instalable. `srv01` permanece en pausa; esto no implica instalación ni aceptación de QA/UAT. Issue #97 sigue abierto y `main` no se modifica.

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
