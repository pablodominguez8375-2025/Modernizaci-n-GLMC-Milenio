# Handoff — Auditoría automática de llegada pública QA — 16-10-2026

Issue #97 / #517. Base dev `2d354159859038e8cc91c0c65075ab1e7a1ce08c`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Sponsor autorizó expresamente montaje operativo QA glmdch sin depender de su PC ni intervención manual. Esta PR prepara una prueba remota sin secretos desde el runner de CI de DNS/HTTPS/OIDC para `https://sistema.granlogiamixta.cl/`, dejando evidencia en el instalable del mismo SHA. Solo consulta servicios públicos; nunca SSH, no instala nada ni afirma G7/G9/G10.

Pendiente real: canal autorizado al host, clave SSH dedicada y huella independiente, DNS/TLS, Environment protegido, respaldos externos y smoke/UAT en glmdch. PR #518 hacia main sin integración ni supuesto de aprobación tácita a promover aplicación.
