# PMGM-QA-V041 — Extracto de Acta 2026 funcional

**Estado:** implementado en rama Post-RC  
**Fuente institucional:** `EXTRACTO-DE-ACTA-2026.docx`  
**Datos del showcase:** exclusivamente ficticios

## Flujo incluido

1. registrar y cerrar la Tenida realizada;
2. registrar asistencia y excusas;
3. registrar balotajes o votaciones como recuentos anónimos agregados;
4. generar automáticamente el borrador del Extracto;
5. completar apertura, acta anterior, correspondencia, decretos y Saco de Proposiciones;
6. completar trabajo presentado, aportes, Bien General y Tronco de Beneficencia;
7. registrar clausura y cierre de cadena;
8. crear una versión del acta;
9. aprobarla preservando las versiones anteriores como reemplazadas.

## Controles

- Los nombres de asistentes provienen de asistencia; no se redigitan en el extracto.
- Los resultados de balotaje provienen del escrutinio cerrado.
- El sistema no registra ni permite reconstruir el voto individual.
- Una diferencia entre habilitados y votos contabilizados exige explicación.
- El balotaje blanco/negro identifica primer, segundo o tercer trámite.
- Las firmas permanecen como campos de formalización del documento.
- El contenido final utiliza el endpoint real de actas versionadas del instalable.

## Demostración

Perfil: **Autoridad de Taller · Demostración**.  
Ruta: **Gestión Logial → Tenidas, asistencia y actas**.  
Versión visual: `UI QA v0.41`.
