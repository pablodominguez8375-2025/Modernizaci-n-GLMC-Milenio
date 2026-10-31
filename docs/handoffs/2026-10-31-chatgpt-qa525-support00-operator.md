# QA525 — adaptación postDB Taller00

PR815 desde dev02271268; main6dfb9546 intacta. EF99 físico confirmado; Taller00 ausente y cero conflictos. Firma del paquete02271268 PASS, no instalado. Código implementa contrato acumulado exacto PR801+PR813, sonda EF100 y catálogo de soporte, ruta explícita postDB100 sin cambiar EF99 original, scope de acta y prepared separados. 191 pruebas Python locales PASS; prueba real PostgreSQL C# añadida para CI. No hay PostgreSQL/.NET local en esta sesión; no atribuirle PASS hasta CI. CI HEAD y publicación aún pendientes.

Siguiente trabajo necesario para montaje: migrador EF objetivo único con identidad DDL y recuperación real G6; no incluido ni autorizado por este cambio. Ver docs/qa/QA525-SUPPORT00-SCHEMA-CUT.md. Posteriormente nueva firma, intervención EF99→100, actas postDB, build/apply dual y UAT Ver como. No eludir bloqueos ni usar SQL manual, Down, PR771/737/766, srv01, main o datos reales. No afirmar QA actualizado por integrar esta adaptación.
