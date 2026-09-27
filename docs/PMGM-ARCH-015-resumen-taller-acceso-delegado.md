# PMGM-ARCH-015 — Resumen del Taller y acceso delegado

**Estado:** integrado en `dev@f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`; demo Pages y artefactos QA/Pre-UAT generados para el mismo SHA. Instalación física, QA y UAT pendientes por pausa de `srv01`.
**Fecha:** 2026-09-27
**Módulo:** Gestión Logial / Perfil del Taller

## 1. Decisión funcional

El menú se denomina **Resumen del Taller** y reemplaza la denominación referencial «Libro de Oro». Reutiliza la proyección existente de Ficha de Taller, sin crear otra base, pantalla duplicada o servicio paralelo.

El resumen unifica datos institucionales ya disponibles: miembros activos y distribución por grado, autoridades vigentes, regularidad informada por Gran Tesorería y Gran Hospitalaria, Tenidas, instrucción y movimientos recientes. No expone datos personales de contacto.

## 2. Acceso ordinario del Consejo

El acceso de consulta se asigna a los ocho cargos que integran el Consejo de Administración conforme al Art. 10.1 de la Constitución y Reglamento: Venerable Maestro, Inmediato Ex-Venerable Maestro, Primer Vigilante, Segundo Vigilante, Orador, Secretario, Tesorero y Hospitalario. La autorización queda limitada al Taller de la reclamación institucional vigente. El perfil técnico `lodge_admin`, por sí solo, no equivale a un cargo del Consejo.

## 3. Delegación manual del Venerable Maestro

El Venerable Maestro vigente del Taller puede otorgar o revocar el permiso de lectura del resumen a otro hermano que:

- tenga membresía activa en el mismo Taller;
- mantenga el grado efectivo de Maestro (tercer grado);
- reciba el acceso sólo para esta vista.

El otorgamiento requiere un fundamento breve. Se registra el sujeto autenticado, fecha UTC y organización. Revocar es lógico: conserva el historial y consigna actor y fecha de revocación. No se habilita una delegación duplicada vigente para el mismo miembro y Taller.

En cada acceso al resumen se verifica nuevamente grado, membresía, Taller y estado de revocación. Una baja o cambio de grado deja el permiso sin efecto automáticamente; el Venerable aún puede revocarlo y consultar que dejó de ser efectivo. Un traslado no traslada la delegación a otro Taller.

Sólo la reclamación exacta del cargo Venerable Maestro dentro del Taller permite administrar estos permisos. La administración técnica del Taller y los roles de administración global no sustituyen al Venerable para esta operación.

## 4. Límites de competencia

El permiso delegado no convierte al hermano en integrante del Consejo ni le entrega voz, voto o atribuciones de gobierno. Tampoco concede edición, registro, firma, aprobación, autorización ni acceso a Tesorería, Hospitalaria, Secretaría, Gestión Logial u otros módulos. La información visible es sólo la proyectada en el Resumen del Taller.

La delegación manual es una decisión funcional expresa del Sponsor / Product Owner. No se presenta como potestad de delegación textual contenida en la Constitución; la Constitución fundamenta la composición del Consejo y su ámbito de trabajo.

## 5. Seguridad y auditoría técnica

- La API impone autorización de servidor para lectura, otorgamiento y revocación; la ocultación del menú no es un control de acceso.
- La lectura se basa en la capacidad del Consejo o en una delegación vigente y elegible.
- Los listados de Talleres incorporan el Taller delegado sólo mientras el miembro mantenga grado y membresía elegibles.
- El registro persiste en PostgreSQL, con claves foráneas, unicidad parcial para concesiones activas e historial de revocaciones.
- Los eventos de concesión y revocación quedan en la bitácora transversal.
- Respuestas personales usan `no-store`; el listado de administración muestra sólo hermanos elegibles o concesiones existentes.

## 6. No regresión y estado

No se alteran rutas ni permisos de Tesorería, Hospitalaria, Secretaría, Biblioteca Virtual, Consejo, Tenidas o planchas. Se conserva el límite de que «Padrón» sólo denomina a electores de la Gran Asamblea.

PR #183 quedó integrada por squash en `dev@f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`. Gates post-merge exact-head SUCCESS: PMGM CI #1598 (run 36283920282), Showcase/Pages #882 (run 36283920245), QA Installable #520 (run 36283920204) y Pre-UAT #374 (run 36283920260). Frontend local: 206/206 Vitest, lint y build SUCCESS.

Pages y QA identifican `SOURCE_SHA=f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b` en `qa-current.json`/BUILD-INFO. El ZIP Pages tiene SHA-256 `2b0278074b0d36046c74eb7a0437b2325e3154635fab2dd3c160e235cf0044bf`; MANIFEST validó 762/762 entradas en Pages y QA Actions. Artifact Pages #10919578545 (digest `sha256:25e4e9e3af1ecbe4f21b91d6789c0373f0731097d93ee0cecc5dea3070794f85`), QA #10919279685 (digest `sha256:784c82463fa9d299b69f58fc8efe0b045b58afe00e498bdb55dc01c155229f2b`) y Pre-UAT #10920025972 (digest `sha256:a342808632d9434ec951335111d7b7ff88a389ba346831a040b9e11e8bbb0790`). La demo se publicó en https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/.

Esto no implica instalación real ni aceptación QA/UAT. `srv01` continúa en pausa; no se ejecutaron instalación, smoke en servidor ni regresión física. Issue #97 sigue abierto y `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece intacta.
