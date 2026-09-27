# PMGM-ARCH-016 — Planchas de trabajo personales y Biblioteca Virtual

**Estado:** implementación comprometida en rama `feature/member-work-papers-20260927`; alcance aprobado por el Product Owner. PR aún no abierto.
**Fecha:** 2026-09-27
**Áreas:** Mi ficha / Biblioteca Virtual / Secretaría del Taller
**Fuente:** regla aprobada de versionado de planchas de trabajo en la Línea Base Maestra.

## Decisión funcional

El hermano consulta desde **Mi ficha → Mis planchas** sólo los trabajos cuya autoría institucional le pertenece. Puede subir una plancha y reemplazar versiones de su propia autoría. Secretaría del Taller puede cargarla a nombre de un hermano activo de su Taller y actualizar la plancha ya vinculada a la Tenida, conservando el mismo historial. El autor puede reemplazarla después aunque la primera versión la haya cargado Secretaría.

Cada carga requiere título y descripción breve de referencia. Se aceptan PDF y DOCX. La clasificación se fija según el grado efectivo del autor en esa carga; el hermano no puede editar el grado. La versión nueva sólo pasa a ser vigente y visible en Biblioteca Virtual → **Planchas de Trabajo** después de validar tipo/contenido, integridad y antivirus. Si el procesamiento falla, se conserva la publicación anterior y sus metadatos vigentes.

La lectura en Biblioteca aplica el acceso acumulativo por grado y las demás restricciones institucionales existentes en backend. Mi ficha muestra el historial de versiones del propio autor. Una plancha de trabajo no es una Plancha de Autorización de Ceremonia ni un documento oficial, y nunca es elegible para Gran Archivero.

## Integración técnica

- Reutiliza `InstitutionalDocument`, `DocumentVersion`, almacenamiento privado de documentos, hash, ClamAV, catálogo y endpoint autenticado de descarga.
- No duplica binarios, API de Biblioteca ni proceso antivirus.
- La autoría se guarda como identidad institucional `MemberId`, separada del sujeto que cargó la versión.
- La carga personal deriva la identidad del token y vuelve a verificar membresía activa; Secretaría selecciona autor sólo dentro de su Taller y requiere que continúe activo.
- Sólo el autor puede consultar su listado personal y crear versiones por autoservicio. Secretaría mantiene la carga dentro de su responsabilidad. Las acciones de contenido y análisis revalidan autoría/ámbito en el backend.
- La publicación de un reemplazo es atómica con el resultado limpio del análisis; nunca desplaza la versión vigente al iniciar carga ni al rechazar el archivo.
- La referencia de una plancha adjunta a Tenida continúa enlazada a una versión documental, pero no determina la audiencia de Biblioteca. Tenidas ceremoniales siguen excluidas de plancha de trabajo.

## Controles

- Identidad distinta no lista ni carga versiones del autor; se comprueba también el acceso directo por ID.
- Membresía inactiva o Taller distinto impide carga/actualización.
- Grado publicado proviene del historial institucional vigente y no del formulario.
- Reemplazo limpio: aumenta versión, mantiene historial y cambia la versión publicada.
- Reemplazo rechazado o antivirus no disponible: conserva archivo, versión y descripción vigentes anteriores.
- Biblioteca excluye el contenido y sus facetas para grado insuficiente; listado legado, detalle por ID y descarga directa también validan el grado efectivo en backend.
- PDF/DOCX se validan por extensión y firma real; los objetos permanecen privados en almacenamiento. El gestor no puede usar rutas genéricas para crear, versionar, aprobar o publicar planchas fuera del flujo controlado.

## Estado de verificación y operación

Esta rama y su PR no equivalen a integración. Los gates exact-head de GitHub deben compilar backend/PostgreSQL y validar permisos, historial, publicación y rechazo antes de solicitar merge a `dev`. `srv01` sigue en pausa: no instalar, desplegar, hacer smoke de servidor, regresión física ni UAT. QA-042 queda agregado como control futuro y permanece `pending` hasta su ejecución física. Issue #97 sigue abierto; `main` no se modifica.
