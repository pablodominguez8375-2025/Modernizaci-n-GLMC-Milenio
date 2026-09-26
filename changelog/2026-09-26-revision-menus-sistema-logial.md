# Revisión de menús del Sistema Logial referencial — 26-09-2026

## Alcance y hallazgos

Se consultó en Google Drive el Documento Maestro de “Sistema Logial de Ejemplo - solo como referencia” y se comparó con el código/documentación del HEAD vivo `dev@14019d2887a782250021a1eac2f3e8dc936e360c`.

El cruce clasifica las vistas de Secretaría/Gestión Logial, Docencia, Tesorería, Hospitalaria, Mi ficha, perfiles y configuración como ya incorporadas, parciales o candidatas, según sus límites propios de Centenario. Las brechas con mejor encaje son terminar el ciclo de planchas de trabajo en Mi cuenta y Biblioteca Virtual; ofrecer consultas de asistencia/instrucciones filtrables por grado/período; y facilitar la consulta de Oficialidad/historial de cargos. Comisiones y una portada tipo Libro de Oro se dejan como candidatas sujetas a necesidad, gobierno y permisos.

## Adaptación a reglas de Centenario

- El ejemplo es referencia funcional; no se importan PHP/SQLite/cPanel ni su modelo SaaS.
- `Padrón` se reserva a electores de la Gran Asamblea; listados generales usan Cuadro del Taller/miembros.
- Las planchas de trabajo son trabajos de los Hermanos, separadas de documentos oficiales y de la Plancha de autorización. Aplican las reglas aprobadas de acceso sólo a planchas propias, descripción breve, reemplazo versionado por su autor aunque cargara originalmente Secretaría, historial preservado, clasificación por grado efectivo y publicación sólo tras integridad y antivirus. Esa función todavía no está integrada en `dev`.
- Se mantienen separados Consejo/Tenidas y Tesorería/Hospitalaria. Toda vista nueva debe reutilizar datos existentes y validar permisos en el backend.

## Estado de verificación

Documento de revisión y priorización; no agrega implementación funcional. No ejecuta instalación ni pruebas en servidor. `srv01` continúa en pausa, Issue #97 sigue abierto, `main` no se modifica, y esta revisión no implica aceptación de QA/UAT.
