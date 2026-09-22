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

## 2. Avance funcional en paralelo

El Sponsor / Product Owner autorizó continuar desarrollo, pruebas y documentación aunque Issue #97 continúe abierto.

Esto no reemplaza QA/UAT ni permite declarar operacional un SHA no desplegado físicamente.

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

## 5. Selección del siguiente incremento funcional

No fijar el próximo bloque usando conversaciones antiguas o memoria.

Antes de programar:

1. reconsultar HEAD vivo de `dev`;
2. leer Estado Maestro, START-HERE, AGENTS, ADRs y PRs recientes;
3. revisar Issue #97;
4. revisar Línea Base Maestra y documentación oficial vigente en Drive;
5. revisar backlog/issues vigentes de GitHub;
6. elegir un bloque no integrado y con fundamento documental actual.

Hasta completar esa reconsulta, el siguiente incremento funcional permanece **por seleccionar**.

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

El trabajo se desarrolla en `feature/system-brand-official-colors-20260922`, rebasada sobre el `dev` vivo `3ae5ecb514b5d9a525d0a271512c94dbe0f65f1b`. Incluye parámetros reales/API, mock Pages, controles y vista previa de los cuatro colores, pruebas y QA-033. La guía oficial del logotipo exige no deformar, recolorear ni recortar el archivo; se conserva su referencia configurable. Pendiente PR, CI exact-head, publicación Pages e instalable QA del mismo SHA. Issue #97 y despliegue físico en `srv01` continúan pendientes.
