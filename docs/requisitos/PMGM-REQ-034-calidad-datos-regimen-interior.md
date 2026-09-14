# PMGM-REQ-034 — Calidad de datos de Régimen Interior

## Estado

Aprobado para QA v0.27.

## Objetivo

Detectar automáticamente inconsistencias cronológicas o relacionales en la historia institucional de los miembros para que Régimen Interior pueda corroborarlas con evidencia antes de efectuar cualquier corrección.

## Principios

- sólo lectura;
- ninguna regla corrige información automáticamente;
- cada hallazgo debe ser explicable;
- la historia de Talleres no se elimina ni reescribe;
- las reglas operan sobre las fuentes de verdad existentes;
- la salida aplica minimización de datos conforme a Ley 21.719.

## Alcance mínimo

La revisión debe cubrir afiliaciones, grados, estados institucionales, defunciones, cargos y traslados, con filtros por fecha de corte, Taller relacionado, severidad, regla y búsqueda.

Cada observación debe incluir como mínimo código, severidad, miembro, Taller cuando aplique, explicación, fechas necesarias y acción sugerida.

## Autorización

Sólo perfiles con capacidad `CanRunRegimenInteriorReports` pueden consultar el módulo.

## Privacidad

Se reutiliza PMGM-DPA-002. La respuesta no debe contener correo, teléfono, dirección, notas, razones privadas, evidencia documental ni datos individuales de Hospitalaria.

## Aceptación

- reglas detectadas con prueba PostgreSQL;
- endpoint protegido y no-store;
- frontend responsive y showcase;
- pipeline CI completo verde antes de merge.
