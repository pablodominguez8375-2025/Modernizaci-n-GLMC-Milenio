# PMGM-REQ-036 — Gran Archivero

## Objetivo
Mantener el catálogo histórico de documentos de la Gran Logia Mixta de Chile con trazabilidad, control de acceso y preservación de integridad, separado de Biblioteca Virtual.

## Reglas funcionales
- Gran Archivero sólo admite documentos institucionales de ámbito Gran Logia/Orden.
- No admite planchas de trabajo; éstas pertenecen a Biblioteca Virtual cuando su publicación está autorizada.
- No duplica archivos: referencia una versión existente del Gestor Documental.
- La versión debe estar en estado `available`, con integridad y escaneo antimalware completados.
- Cada versión sólo puede tener un registro archivístico.
- El código archivístico es único.
- El retiro del catálogo activo es lógico y trazable; no elimina la versión documental fuente.
- La descarga exige autenticación y rol/autorización de Gran Archivero; no se crean enlaces públicos permanentes.

## Acceso
Rol institucional `grand_archivist` con `pmgm_scope=order`, más `grand_lodge_admin` para administración central.

## Privacidad y Ley 21.719
Tratamiento PMGM-DPA-012. Los metadatos sensibles/restringidos no van a logs ni exportaciones masivas por defecto. Los identificadores técnicos de actor permanecen en persistencia para trazabilidad, pero la proyección UI utiliza nombres visibles autorizados.

## Exclusiones
- CENDOC no es un módulo del Proyecto Milenio.
- Biblioteca Virtual sigue siendo un módulo separado.
- Planchas de trabajo no forman parte de Gran Archivero.
