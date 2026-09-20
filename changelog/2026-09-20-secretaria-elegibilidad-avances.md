# Secretaría — elegibilidad de Aumento de Salario y Exaltación

- La elegibilidad deja de ser declarativa: se calcula desde el historial efectivo de grado, asistencias a Tenidas, instrucciones realizadas y Planchas de Trabajo.
- Los mínimos 2026 quedan versionados en `institutional_rule_settings`:
  - Aumento de Salario: 24 meses, 30 Tenidas de 1.er grado, 10 instrucciones de 1.er grado y 2 Planchas de Trabajo.
  - Exaltación: 24 meses, 10 Tenidas de 2.º grado, 10 instrucciones de 2.º grado y 2 Planchas de Trabajo.
- Secretaría puede registrar una dispensa excepcional sólo con acuerdo afirmativo de Cámara del Medio, referencia de acta y requisito/reducción identificados.
- La dispensa requiere validación de Régimen Interior y no admite una reducción superior al 50% de ningún requisito.
- Gran Secretaría queda bloqueada para autorizar si falla la elegibilidad de avance o cualquiera de los vistos buenos institucionales vigentes.
- La Demo muestra el resultado alcanzado/mínimo mediante el mismo contrato usado por la API real.

## Validación del corte

- HEAD funcional: `8646bab18154639c36a70ec817a0fcfa4e4989a5`.
- PMGM CI #1369: `SUCCESS` (324 pruebas backend).
- PMGM Showcase Demo/Pages #595: `SUCCESS`.
- QA srv01 Installable #233: `SUCCESS`.
- Artefacto: `10612530517`; digest `sha256:8306d627784507a2cf96d6ec706628b23fc25d60815d57ef35f93993318b11b6`.
- La generación del instalable no equivale a despliegue físico: `srv01`, QA-001..QA-026 y UAT restante siguen pendientes/diferidos.
