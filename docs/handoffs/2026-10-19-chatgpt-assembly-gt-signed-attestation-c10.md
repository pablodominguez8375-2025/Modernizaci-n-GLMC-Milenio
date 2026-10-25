# #533 — C10 constancia documental firmada append-only

Inicio 2026-10-19. Rama `feature/assembly-gt-signed-attestation-c10-20261019-gpt`, base `dev@f22b5797c6578a9af286b5ec2e31ac3939bc30fc`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intocable. QA físico pertenece a otro hilo.

## Objetivo
Persistir de modo no destructivo una **constancia de revisión documental firmada** de Gran Tesorería, no un certificado de saldo ni voto. Los eventos de propuesta/revisión C3 son append-only. C10 agrega registro firmado en nueva tabla: vinculación compuesta mismo Taller/Asamblea de propuesta y revisión; un único sello por revisión; solo revisión `reviewed`, revisor sujeto de la firma y distinto de proponente, tiempos ordenados, SHA-256 del documento y sobre de firma; triggers contra UPDATE/DELETE/TRUNCATE y contra asociación cruzada. La validación RSA-PSS por C6/C7 y la autorización GT explícita de C8 se deben ejecutar antes del armado del registro; queda prohibida la creación directa desde payload HTTP.

No se crean endpoints públicos ni se autentican fuentes financieras reales. La DB por sí sola no valida la firma RSA ni autentica permisos; el almacén no certifica deudas, no autoriza sufragio y no congela padrón. El consumo futuro DEBE revalidar la firma contra el registro vigente de confianza (incluidas revocaciones). Probar con actores y RSA sintéticos y PostgreSQL, sin claves reales.

C9 (saldo apertura) es PR independiente #577 sobre dev, no modificar su rama. Es necesario además custodiar/rotar claves institucionales, conectar deuda histórica GT por moneda/corte, RBAC/auditoría en endpoint y fuentes Tribunal/RI, cierre y exportación. CI exact-head 3/3 antes de integrar. Gobernanza GOV-003: no modificar main/QA/srv01.
