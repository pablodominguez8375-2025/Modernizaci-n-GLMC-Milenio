# PMGM-ARCH-015 — Resumen del Taller y acceso delegado

**Estado:** alcance autorizado; implementación en rama de trabajo / revisión pendiente
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

Implementación local sobre `dev@14019d2887a782250021a1eac2f3e8dc936e360c`: navegación y etiqueta, endpoints, política de autorización, entidad/migración, auditoría y cobertura de permisos. Frontend: 206 pruebas, lint y build locales exitosos. La compilación/pruebas backend y los gates del repositorio esperan CI exact-head del PR. No hay integración en `dev`, publicación de demo, artefacto QA, instalación, aceptación de QA ni UAT por este estado. `srv01` sigue en pausa, Issue #97 permanece abierto y `main` no se modifica.
