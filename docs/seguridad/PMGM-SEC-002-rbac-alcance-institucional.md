# PMGM-SEC-002 — RBAC y alcance institucional

**Estado:** Base técnica activa  
**Prioridad:** P0

## Objetivo
Definir una capa de autorización independiente del proveedor de identidad para que el mismo backend pueda integrarse con un proveedor OIDC/JWT sin acoplar las reglas institucionales a Microsoft, Google u otro IdP específico.

## Claims internos esperados
El token validado puede mapear los siguientes claims internos:

- `pmgm_role`: rol institucional.
- `pmgm_scope`: alcance transversal. El valor `order` representa alcance Gran Logia.
- `pmgm_org`: UUID de Taller/organización autorizado. Puede repetirse para múltiples organizaciones.

El proveedor de identidad definitivo deberá mapear sus grupos/roles a estos claims o a una capa equivalente.

## Roles base
- `grand_lodge_admin`: administración transversal.
- `internal_affairs`: Régimen Interior.
- `grand_secretariat`: Gran Secretaría.
- `grand_treasury`: Gran Tesorería.
- `lodge_admin`: administración de Taller.
- `lodge_secretariat`: Secretaría de Taller.

Los nombres son códigos técnicos; las etiquetas visibles serán institucionales y configurables.

## Principios
1. Autenticación no equivale a autorización.
2. Un rol de Taller no obtiene acceso automático a otros Talleres.
3. El historial de un hermano se filtra por alcance salvo que el rol posea autorización transversal explícita.
4. Régimen Interior y administradores de Gran Logia pueden operar reportes transversales cuando su claim de alcance sea `order`.
5. Las áreas sólo deben recibir los datos necesarios para su función.
6. Una transferencia de Taller no amplía automáticamente los permisos sobre el historial documental del Taller anterior.
7. Los endpoints de escritura institucional requieren autorización adicional al simple login.

## Transferencias
- La solicitud puede originarse desde un Taller autorizado o desde un rol transversal habilitado.
- La aprobación y ejecución inicial requieren alcance `order` y rol `internal_affairs` o `grand_lodge_admin`.
- La regla puede ampliarse más adelante con flujos configurables y firmas formales.

## Régimen Interior
El resumen `/api/regimen-interior/summary` requiere:
- usuario autenticado;
- `pmgm_scope=order`;
- rol `internal_affairs` o `grand_lodge_admin`.

## Evolución
Antes de producción deberán añadirse:
- auditoría de autorizaciones denegadas relevantes;
- políticas formales por operación;
- pruebas automatizadas de acceso positivo y negativo;
- mapeo documentado del proveedor OIDC elegido;
- MFA para roles administrativos según política institucional.
