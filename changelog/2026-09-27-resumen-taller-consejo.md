# Resumen del Taller y acceso delegado — 2026-09-27

## Hallazgo y decisión

La revisión de los menús referenciales dejó «Libro de Oro» como una idea pendiente de validar. El Sponsor definió que debe reemplazarse por **Resumen del Taller**, una vista unificada para los miembros del Consejo de Administración.

## Cambio implementado en rama de trabajo

- Se reutiliza la proyección existente de Ficha de Taller; no se crea una portada paralela.
- Los ocho cargos del Consejo tienen acceso de lectura acotado a su Taller.
- El Venerable Maestro puede otorgar o revocar acceso de sólo lectura a un Maestro con membresía activa en su mismo Taller.
- Se pide fundamento, se audita quién/cuándo y se mantiene historial. El permiso pierde efecto si el hermano deja de estar activo o deja de tener grado de Maestro.
- El delegado no pasa a integrar el Consejo ni adquiere capacidades de otros módulos.
- El API aplica los permisos en servidor. La base incorpora concesiones revocables con unicidad de una concesión vigente por miembro/Taller.

## Verificación y estado

Frontend local: 206 pruebas Vitest, lint y build exitosos. La prueba API cubre que concesión/revocación usan el Taller elegido y autenticación bearer. La migración, compilación/pruebas backend y CI exact-head esperan revisión y gates del PR.

Estado actual: en `feature/lodge-council-summary`, basado en `dev@14019d2887a782250021a1eac2f3e8dc936e360c`; aún no integrado, no publicado en Pages ni generado como artefacto QA. `srv01` sigue en pausa. No implica instalación, QA aceptada ni UAT. Issue #97 sigue abierto; `main` permanece intacta.
