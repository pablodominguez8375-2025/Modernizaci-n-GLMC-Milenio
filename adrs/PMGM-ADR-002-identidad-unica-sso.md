# PMGM-ADR-002 — Identidad única y SSO

## Estado
Aceptado — v0.1.

## Contexto
El ecosistema institucional actual y futuro contiene múltiples servicios. Mantener credenciales independientes por módulo aumenta fricción, riesgo, soporte y dificultad para revocar accesos. El principio acordado del Proyecto Milenio es que cada usuario disponga de un único acceso institucional.

## Decisión
1. La autenticación será centralizada mediante un proveedor de identidad compatible con OpenID Connect/OAuth 2.0.
2. Los módulos de negocio no implementarán almacenes independientes de contraseñas.
3. La autorización se resolverá mediante roles, permisos y contexto de organización.
4. MFA será obligatorio para perfiles administrativos o de alto privilegio y podrá ampliarse por política.
5. La revocación del acceso se realizará centralmente.
6. La ficha PERSONA/MIEMBRO y la identidad técnica USUARIO estarán relacionadas, pero no serán la misma entidad.

## Consecuencias positivas
- Un solo inicio de sesión para la intranet y servicios integrados.
- Revocación centralizada.
- Menor superficie de almacenamiento de credenciales.
- Mejor trazabilidad.
- Integración futura con servicios externos mediante estándares.

## Consecuencias / costos
- El proveedor de identidad se convierte en un componente crítico.
- Requiere diseño de alta disponibilidad, recuperación y políticas de acceso.
- Los módulos deben implementar autorización correcta aunque la autenticación sea centralizada.

## Pendiente
Seleccionar proveedor concreto de identidad y documentarlo en ADR posterior sin alterar esta decisión de estándar/arquitectura.
