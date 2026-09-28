# PMGM-ARCH-018 — Consulta de docencia de la Orden

**Estado:** implementado, integrado en `dev@7f2079b3c1d89564cc181f6efad5f231de6db081`; PR #206.
**Fecha:** 2026-09-28

## Decisión funcional

Se mantiene el registro operativo de instrucciones en el Taller y el historial individual asociado a la asistencia de cada hermano. Se agrega una vista institucional **Docencia de la Orden**, de solo lectura, para consultar las instrucciones realizadas, por Taller, por período y por grado. El reporte consolidado evita exponer nombres, identificadores u otros datos personales de asistentes; entrega cantidades presentes/ausentes y los datos de cada sesión realizada.

| Cargo institucional | Grado que puede consultar |
| --- | --- |
| Gran Segundo Vigilante | Aprendices (primer grado) |
| Gran Primer Vigilante | Compañeros (segundo grado) |
| Inmediato Ex Gran Maestro | Maestros (tercer grado) |
| Jefatura del Departamento de Docencia | Los tres grados |

Los Grandes Oficiales pueden seleccionar un Taller o consultar el conjunto de Talleres del grado que supervisan. Jefatura de Docencia puede seleccionar un grado o consultar todos los grados. Los filtros no amplían el permiso concedido por cargo. Se autorizan únicamente sesiones marcadas como realizadas; sesiones programadas o canceladas se excluyen.

## Autorización y límites

- La autorización se valida en backend en cada consulta; la interfaz sólo refleja esas capacidades.
- La vista institucional no concede gestión de sesiones, cierre, asistencia, permisos de Taller, firmas, aprobaciones ni atribuciones de nombramiento.
- Ninguno de los cargos de lectura institucional recibe las capacidades de edición de los vigilantes de Taller.
- La Jefatura de Docencia puede consultar los tres grados por definición expresa del Product Owner. Esta regla técnica no define cómo se designa esa jefatura.
- El reporte muestra Taller, fecha, grado, tema, cargo responsable y conteos de asistencia; no muestra instructores por nombre ni hermanos por nombre.
- Los historiales de `Mi ficha` continúan siendo individuales y sólo para el propio hermano conforme al flujo vigente.

## Base normativa y funcional

La Constitución y Reglamento General vigente consultados desde Drive (copia con fecha de modificación 2026-09-17) asignan la instrucción de Aprendices al Segundo Vigilante, de Compañeros al Primer Vigilante y de Maestros al Inmediato Ex-Venerable Maestro (Art. 12.5). Sus Arts. 20.1 y 20.2 asignan la supervisión institucional de Maestros al Inmediato Ex Gran Maestro y la supervisión general de instrucción a los Grandes Vigilantes, con Primero para segundo grado y Segundo para primer grado. El Art. 22.1(b) describe funciones del Departamento de Docencia. La consulta de los tres grados por su jefatura se documenta como decisión de producto de solo lectura, sin inferir atribuciones normativas adicionales.

La Línea Base Maestra de Drive, `LINEA BASE MAESTRA - Proyecto Centenario - 17-09-2026` (actualizada 2026-09-28), y GOV-001 sección 10 fijan el registro de sesiones y el historial individual. La referencia `Documento_Maestro_App_Gestion_Taller_Masonico_FINAL.md` se usa como referencia funcional, no como autoridad normativa o especificación técnica.

## Verificación

- Probar acceso cruzado: Segundo Gran Vigilante → Aprendices solamente; Primer Gran Vigilante → Compañeros solamente; Inmediato Ex Gran Maestro → Maestros solamente; Jefatura → los tres.
- Probar que Taller seleccionado y todos los Talleres producen el mismo ámbito de grado autorizado.
- Probar filtros de período, sólo sesiones realizadas, conteos por última asistencia registrada y Talleres sin actividad.
- Probar que perfiles de lectura reciben `403` al invocar creación, marcar realizada o registrar asistencia.
- Confirmar que la asistencia permanece enlazada a `Mi ficha` y que los nuevos registros figuran en la sesión del mismo Taller/grado.
- Las pruebas automatizadas y el demo con datos ficticios no sustituyen instalación, regresión física ni UAT. Issue #97 y la pausa de `srv01` siguen vigentes.

## Resultado del corte 2026-09-28

PR #206: exact-head `8dc936301b597b1c2aeaf0b48ec309c8456c59bc`; PMGM CI #1651, Showcase #954, QA Installable #592 SUCCESS. Post-merge dev `7f2079b3c1d89564cc181f6efad5f231de6db081`: CI #1652, Showcase/Pages #955 (Deploy SUCCESS), QA Installable #593 y Pre-UAT #393 SUCCESS. Frontend local 216/216, lint/build SUCCESS; gates privacidad, clasificación y migración SUCCESS. Checksum ZIP QA distribuido en Pages `567b15253b585c7919ae90afe05dda43d5827112885b783b7f7172c874e14cc3`; QA Actions artifact digest `sha256:19cac46d170b79fe09d2f8be0814c78d2be0f14540e51e6eb63552feab965414`. `srv01` sigue pausado: no instalación ni UAT; no promoción a `main`.
