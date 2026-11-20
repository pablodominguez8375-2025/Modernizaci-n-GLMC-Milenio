# QA operativo con datos institucionales reales — decisión Sponsor 2026-11-20

## Decisión y alcance
El único host QA es `glmdch`. Debe funcionar como entorno institucional operativo y permitir pruebas de extremo a extremo con usuarios, Talleres, cargos, documentos y datos reales, preservando información válida entre actualizaciones. La demo sintética y CI son complementarios; no sustituyen aceptación funcional real.

**Esta decisión no es autorización de despliegue, fusión, importación inmediata de datos personales ni promoción de `main`.** El acceso a datos reales queda sujeto a habilitación operativa explícita y evidencia verificada. No crear segunda VM.

## Ruta mínima y eficaz para habilitar operación real
1. **Inventario de estado real (solo lectura):** verificar hostname `glmdch`, Compose y contenedores activos, versión/SHA API y WEB, identidad/Keycloak, PostgreSQL y MinIO, TLS, health y modo QA_SYNTHETIC. Registrar evidencia sin secretos ni datos personales.
2. **Protección de datos:** definir responsable, finalidad, base jurídica, categorías y usuarios autorizados; roles de mínimo privilegio y auditoría; asegurar que datos reales nunca se publiquen en GitHub, Pages, demostraciones, artifacts, logs o tickets. Separar claramente cuentas y datasets de pruebas sintéticas.
3. **Recuperación verificable:** respaldo G6 cifrado y externo de DB/identidad/objetos, hashes, restore aislado comprobado sobre el corte exacto, rollback de imágenes/API/WEB y plan de recuperación documentado. Confirmar privilegios DB y que cuentas de aplicación no sean superusuarios.
4. **Prueba funcional controlada:** empezar con un Taller y usuarios reales autorizados; verificar autenticación, perfiles, altas/cambios, adjuntos, Tesorería, trazabilidad, permisos negativos, lectura posterior y persistencia tras reinicio/actualización supervisada. No introducir datos reales sin GO específico.
5. **Aceptación y operación:** registrar resultados, incidentes, responsable y GO/NO-GO en GitHub y Drive. Conservar datos válidos, no usar `git reset --hard` ni instaladores históricos `srv01` en `glmdch`.

## Condiciones de parada
STOP si falta evidencia de respaldo restaurable, autorización de tratamiento, segregación, control de acceso, TLS, auditoría, rollback, integridad o versión firmada. No sustituir controles por una prueba sintética exitosa.

## Trazabilidad
- Decisión Sponsor: issue #1035, comentario 7029215439.
- Seguimiento vivo: Google Drive «Proyecto Centenario — Estado Maestro Operativo».
- QA525 y `glmdch`: verificar estado actual en el host antes de concluir que los controles históricos siguen vigentes.
- Cambios de software: solo PRs revisadas, CI del SHA exacto y GO específico para instalación.
