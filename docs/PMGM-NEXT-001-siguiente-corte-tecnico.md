# PMGM-NEXT-001 — Siguiente corte técnico

## 1. Hito operacional bloqueante vigente: QA srv01

Issue #97 continúa abierto. El despliegue físico en `srv01` sigue siendo obligatorio antes de UAT/promoción a `main`.

El corte operativo debe usar siempre el HEAD vivo de `dev` y verificar paridad:

`HEAD dev = Pages = qa-current.json = BUILD-INFO del ZIP QA`.

Orden operacional:

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

La Línea Base Maestra LB-PC-2026-09-17 establece que no se inicia un nuevo incremento funcional mientras Issue #97 mantenga bloqueada la QA física, salvo decisión expresa del Sponsor / Product Owner para un alcance concreto. La decisión vigente mantiene diferido el despliegue físico en `srv01`; por tanto, este corte se limita a continuidad, documentación, preparación/verificación de QA y correcciones necesarias para el gate. Un workflow o artefacto generado no cierra Issue #97 ni sustituye pruebas en el servidor.

## 3. Funciones integradas que no son trabajo futuro

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

Se amplía el script `.github/scripts/capture-showcase-views.mjs` para comprobar en navegador que el perfil Tesorero conserva sus accesos transversales y las cuatro funciones propias de Tesorería, y no recibe accesos de Secretaría/Gestión Logial. En el recorrido del Venerable se comprueba que sólo figure **Egresos por autorizar**. La validación corre antes de guardar las capturas de escritorio y móvil y falla Showcase ante cualquier divergencia.

Este ajuste automatiza un control del alcance QA-031/v0.59; no añade ni modifica capacidades funcionales. El despliegue físico en `srv01` y la aceptación QA/UAT siguen pendientes según Issue #97.

## 14. Incremento autorizado — logotipo oficial en la plataforma

El Product Owner solicita integrar el logotipo oficial de la Gran Logia Mixta de Chile junto a la identidad corporativa de Proyecto Centenario. El recurso fuente es `Logo Gran Logia Mixta de Chile.svg` en Drive, ID `1_BLXseShbQX-ioGMNLd5xKPYGtLMEvFm`; la guía institucional exige fondo blanco, proporciones originales, área de protección y no recortar ni recolorear la marca.

El incremento v0.62 sustituye la “C” provisional de la cabecera por el SVG oficial y refuerza Showcase para comprobar carga, proporción, fondo y encuadre no superpuesto en móvil/escritorio. La paleta v0.61 se conserva. El PR #137 quedó integrado por squash en `e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`.

Gates exact-head sobre el merge SHA: CI #1436, Showcase/Pages #676, QA Installable #314 y Pre-UAT #329 SUCCESS. Demo y ZIP se generaron desde el mismo SHA. QA-062 registra la verificación live y digests. `main` sigue intacta (`6dfb9546a4873baff15955cf86abfd7d47e3d111`). La excepción autorizada cubre sólo la incorporación del logo; no cambia la decisión de mantener pendiente la QA física ni autoriza promoción a `main`.

Artefacto QA #10723132819: `proyecto-centenario-qa-srv01-e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`, digest `sha256:a42964269eb9af176feacfd3f0f546b51072647fb828b8af062b2bded180497b`, vence el 22-10-2026 22:55:08 UTC. Pages artifact #10723212264, digest `sha256:1be0343b760f6f65d5acfcf066582d56a6e4c76638955bf0eefb73cf44df375c`, vence el 23-09-2026 22:56:18 UTC.

## 15. Próximo punto de continuidad

Issue #97 sigue abierto: despliegue físico diferido en `srv01`, verificación `SOURCE_SHA`/`MANIFEST`, smoke autenticado, QA-001..QA-034, UAT y evidencia. No iniciar otro incremento funcional mientras siga el bloqueo, salvo decisión expresa del Product Owner con alcance concreto. Al retomar, consultar HEAD de `dev` y `main`, START-HERE, Drive y esta sección; no reutilizar artefactos expirados.


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
