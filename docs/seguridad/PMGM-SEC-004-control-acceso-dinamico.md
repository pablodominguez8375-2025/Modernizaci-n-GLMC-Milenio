# PMGM-SEC-004 — Perfiles técnicos, menús, vistas y acciones

Solo administradores que ya tienen CanConfigureSystem administran el catálogo y solicitan impresión de su revisión. Asignar un perfil técnico no habilita por sí mismo CanConfigureSystem, firma, aprobación ni cargo institucional; los endpoints de negocio conservan su autoridad final. No se promocionan roles del proveedor OIDC desde esta API.

- Seis acciones independientes por vista: view/create/write/edit/delete/print. Acciones requieren view. Print no significa exportación ni firma.
- Cargos base protegidos: no editar, borrar, reemplazar grants ni asignar como si fueran perfiles custom. Se pueden duplicar como perfil técnico sin heredar atribuciones institucionales.
- Baja lógica preserva perfil y asignaciones e inactiva estas últimas.
- Identificadores OIDC son exactos y sensibles a mayúsculas; no usar correo como sustituto.
- Perfil lodge requiere Taller existente; perfil order no recibe OrganizationId. Evaluación exige organización exacta y ámbito institucional vigente; no existe wildcard implícito entre Talleres.
- Vigencias se evaluan con fecha civil America/Santiago e intervalos inclusivos; no se aceptan intervalos invertidos ni superpuestos para el mismo sujeto/perfil/Taller.
- Menú/vista activos y seleccionados; grants duplicados o de vistas desconocidas se rechazan; ausencia de grant deniega en el evaluador.
- Snapshot JSONB versionado + auditoría comparten transacción y bloqueo PostgreSQL. expectedVersion antiguo devuelve 409 sin escritura ni auditoría de éxito; malformed snapshot no se convierte en catálogo vacío.
- Lecturas administrativas y evaluación son private/no-store. Los sujetos y permisos del catálogo son restringidos; no se incluyen en endpoints públicos.
- La impresión del informe administrativo requiere POST autorizado y auditado antes de window.print. Un grant print de otra vista no permite imprimir este catálogo administrativo.
- El adaptador de demo sólo maneja datos ficticios. La demo no certifica sincronización de identidad, roles OIDC ni atribuciones institucionales.

La administración y el evaluador son el contrato técnico. La aplicación de permisos a cada acción institucional debe conservar los controles específicos del dominio; el catálogo no reemplaza esos controles ni inventa un circuito de delegación institucional.
