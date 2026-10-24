# GOV-004 — diccionario de salida Asamblea C24 (23-10-2026)

## Fuente canónica de inventario
Se lee exclusivamente `PmgmDbContext`: Member/Person, Membership, OfficeAssignment, Organization, InstitutionalStatusEvent a fecha de corte. El código canónico de V.M. debe originarse en configuración confiable backend: no se lo obtiene de pantalla. No existe una tabla o acta oficial validada de integridad/fin del mandato en este incremento.

`AssemblyUncertifiedMemberSource`: MemberId, PersonId, MembershipWorkshopId, nombres, RUT opcional, WorkshopName, estado de membresía ACTUALMENTE registrado, último estado institucional anotado antes del corte, colección de `AssemblyUncertifiedMasterOffice` con ID de oficio, Taller histórico, fechas y referencia documental grabada. Son datos privados; **no exportables** como Padrón, ni certificación, ni permiso de sufragio.

`AssemblyMemberSourceInventoryResult`: `InventoryReadCompleted` puede ser true tras inspección coherente; **`InstitutionallyCertified` es siempre false**. Errores se informan sin PII ni lote parcial; se rechaza soporte, más de una membresía de un mismo miembro al corte, ausencia de membresía, identidad/cargos incompletos, y cantidad excesiva.

## Cambios de esquema
Ninguno. No afecta tablas existentes, no agrega migración ni campos persistentes. Modificación a manifiesto CI para integrar batería PostgreSQL en shard B.

## Siguiente condición obligatoria
Integrar fuente acreditada de Gran Secretaría para períodos estatutarios VM, cobertura oficial de miembros, identidad/membresía histórica, GT, RI y Tribunal de Honor. Este inventario es solamente hechos de BD, NO alimenta el preparador C22/C23 automáticamente. Establecer cobertura total, evidencia certificada y UAT antes de activar materialización y cierre de Padrón.
