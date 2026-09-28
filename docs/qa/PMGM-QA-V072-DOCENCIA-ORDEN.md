# PMGM-QA-V072 — Docencia de la Orden y registro por grado

**Estado:** criterios implementados y verificados en CI/demo; la aceptación física y UAT permanecen pendientes.
**Fecha:** 2026-09-28  
**Requisito técnico:** `docs/PMGM-ARCH-018-docencia-de-la-orden-lectura.md`

## Perfiles y acceso

| Perfil | Vista | Grados permitidos | Mutaciones de instrucciones |
| --- | --- | --- | --- |
| Segundo Vigilante del Taller | Docencia | Aprendices de su Taller | Registrar y completar, asistencia del grado autorizado |
| Primer Vigilante del Taller | Docencia | Compañeros de su Taller | Registrar y completar, asistencia del grado autorizado |
| Inmediato Ex-Venerable Maestro | Docencia | Maestros de su Taller | Registrar y completar, asistencia del grado autorizado |
| Gran Segundo Vigilante | Docencia de la Orden | Aprendices de todos los Talleres | Denegadas |
| Gran Primer Vigilante | Docencia de la Orden | Compañeros de todos los Talleres | Denegadas |
| Inmediato Ex Gran Maestro | Docencia de la Orden | Maestros de todos los Talleres | Denegadas |
| Jefatura de Docencia | Docencia de la Orden | Los tres grados | Denegadas |

## Controles automatizables

1. El instructor consulta/crea sólo su grado y sólo en su Taller; la lista de asistentes corresponde al grado vigente a la fecha de la instrucción.
2. Al marcar una sesión como realizada y registrar asistencia, el hermano ve su registro en `Mi ficha` con fecha, tema, grado y estado de asistencia.
3. Los perfiles institucionales sólo consultan; un acceso directo al endpoint de escritura devuelve prohibición y no produce cambios.
4. El reporte permite filtrar por Taller/período. La Jefatura puede consultar un grado o todos; cada Gran Oficial mantiene el grado fijo aunque altere la URL.
5. Sólo se cuentan instrucciones realizadas y para cada hermano/sesión se considera la última asistencia registrada. La respuesta agrega presentes/ausentes y no contiene nombre, correo ni ID de hermano.
6. La demostración muestra más de un Taller y sesiones ficticias en los grados aplicables; no hace llamadas de red.

## Evidencia y límites

Registrar SHA exacto de la rama/PR y resultado de CI, Showcase/Pages y QA Installable en el handoff. Los controles locales y CI no prueban instalación ni configuración de roles en `srv01`. No instalar, desplegar, ejecutar smoke/regresión física ni UAT mientras continúe la pausa del Sponsor y Issue #97. No promover a `main`.

## Resultado automatizado — 2026-09-28

PR #206 exact-head `8dc936301b597b1c2aeaf0b48ec309c8456c59bc`: PMGM CI #1651, Showcase #954 y QA Installable #592 SUCCESS. Post-merge `dev@7f2079b3c1d89564cc181f6efad5f231de6db081`: PMGM CI #1652, Showcase/Pages #955, QA Installable #593 y Pre-UAT #393 SUCCESS. Artifact Pages #10967675504 digest `sha256:f7ae60ac8f7af3c8a8aa051b8c2b19820626e571795f7a4c0f221395a0054053`; qa-current confirma SHA integrado. Artifact QA #10966054591 digest `sha256:19cac46d170b79fe09d2f8be0814c78d2be0f14540e51e6eb63552feab965414`, BUILD-INFO confirma el mismo SHA, MANIFEST 780/780 comprobado. No hubo instalación física, UAT ni aceptación operacional.
