# Handoff — usuarios vinculados a Hermano activo (#362)

Decisión PO: cuenta institucional exige Hermano activo en un Taller; correo de ficha obligatorio; clave inicial aleatoria enviada al correo registrado y cambio obligatorio al primer ingreso. Excepción sólo administradores de plataforma. Perfil técnico no crea cargo institucional.

Base dev05d01fba2126581f22487bbcd6efccbb46c476d4; main6dfb9546a4873baff15955cf86abfd7d47e3d111. Reservas en Issue #362: módulo UserAccounts/Program, identidad existente, tests, SystemOperationsPanel/formulario/API, configuración Keycloak/SMTP instalable y registros GOV004/requisito. Sin CSS ni ramas históricas #58/#60. Gate exact-head antes de integrar; despliegue QA pendiente, srv01/UAT #97 pausados.

Revisión Drive: sólo Línea Base con cierre #266; sin novedades hasta este corte en raíz/subcarpetas revisadas; revisión recursiva completada sin novedades. La pantalla actual sólo simula usuarios; no hay alta real API. Implementación, pruebas, publicación y recibo final pendientes.

Implementación PR: UserAccounts extiende API/Keycloak y SMTP STARTTLS; vínculo identity_links existente, sin migración; formulario institucional y adaptador sintético compatibles, excepción sólo plataforma; clave efímera 24 caracteres y UPDATE_PASSWORD; compensación por fallos, auditoría minimizada, recuperación realm SMTP. Requisito039, registro GOV004 #362, catálogo y runbook de configuración protegida incorporados. Pruebas locales frontend465/465, build y lint SUCCESS; gates privacidad/clasificación/migraciones SUCCESS. Backend HTTP PostgreSQL y proveedor falso agregados; resultados CI exact-head/publicación pendientes. No hay cuentas ni correos reales creados.
