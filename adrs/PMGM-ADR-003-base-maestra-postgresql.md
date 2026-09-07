# PMGM-ADR-003 — Base maestra institucional en PostgreSQL

## Estado
Aceptado — v0.1.

## Contexto
Los procesos institucionales requieren compartir información de miembros, Talleres, cargos, periodos y documentación. Mantener bases aisladas por módulo genera duplicación, divergencia y procesos de reconciliación manual.

## Decisión
1. PostgreSQL será el motor relacional principal del Proyecto Milenio.
2. Existirá una base maestra institucional para los datos transaccionales y maestros del ecosistema.
3. Los módulos reutilizarán entidades maestras y no crearán copias funcionales de PERSONA, MIEMBRO u ORGANIZACION.
4. Los identificadores técnicos serán internos y no significativos, preferentemente UUID.
5. Identificadores nacionales como RUT no serán claves primarias técnicas ni identificadores públicos de recursos.
6. La evolución del esquema se gestionará mediante migraciones versionadas.
7. Los documentos binarios podrán almacenarse fuera del motor relacional; PostgreSQL conservará los metadatos y referencias necesarias.

## Consecuencias positivas
- Consistencia institucional.
- Menos duplicación y sincronización manual.
- Mejor auditoría y reportería transversal.
- Simplificación del MVP.

## Consecuencias / controles
- El modelo debe tener límites de dominio claros para evitar una base de datos sin gobierno.
- Los accesos deben pasar por servicios/aplicación y respetar permisos.
- Backups, restauración y disponibilidad de PostgreSQL se convierten en controles críticos.

## Criterio de revisión
La decisión del motor podrá revisarse únicamente si aparece un requisito técnico demostrado que PostgreSQL no pueda satisfacer razonablemente. La existencia de módulos nuevos, por sí sola, no justifica introducir otra base maestra.
