# Gran Hospitalaria: sincronización explícita — #266

Base dev 1216e159f4dc2ad9459dedbf49b9be622a581933; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Rama feature/grand-hospitalaria-explicit-sync-20261005-gpt.

Alcance: separar las consultas de refreshGrand del POST sincronizar-defunciones; sincronización explícita y confirmada mediante ActionDrawer. Mantener el endpoint real, cálculo, idempotencia, autoridad institucional y adaptador demo. Reservas: frontend/src/HospitalariaPage.tsx, frontend/src/HospitalariaPage.test.tsx y este handoff. No tocar App.tsx (#116/#327), demoProfiles, estilos, workflows, gobierno, migraciones ni nginx de #327.

Drive revisado al inicio: raíz Proyecto Centenario y subcarpetas Logos/RefSystem. Novedades: instrucciones de seguridad 1YCinOVBZIEG5pUaeYl2TEZqv3v6djAr1fSA4v5H_Jyk y adenda propuesta 1GRwOO63fXl4BMwArc2uubHwK2oUE3lHlOsqVh7qrm7w; esta última declara pendiente de aprobación PO. No se interpretan como aprobación de todos los cambios sugeridos y no se ejecutan en este corte. Línea Base conserva pendiente Gran Hospitalaria. PR #327 no coincide con las reservas.

Sin cambios de modelo/contratos/reglas de datos: sólo disparador de interacción del POST ya existente; cálculo, validaciones, datos y permisos no cambian. Sin migración. No afirma extensión de grants de Orden ni cierre global #266.

Pruebas/gates/publicación/paquete QA: pendientes al reclamar. El siguiente recibo de cierre registrará resultados sobre el SHA exacto. Main/srv01/UAT pausados; despliegue QA pendiente.
