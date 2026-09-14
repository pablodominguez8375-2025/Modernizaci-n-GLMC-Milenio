# PMGM-ARCH-002 — Máquina de estados de insinuación 2026

**Estado:** diseño funcional para implementación  
**Rama:** `dev`

## Estados

1. `draft` — formulario en preparación.
2. `presented_first_degree` — presentada en tenida de 1.er grado.
3. `waiting_initial_review` — corre plazo previo a deliberación.
4. `initial_review` — deliberación de la instancia correspondiente.
5. `approved_for_publication` — aprobación unánime registrada.
6. `observed` — requiere corrección sin crear expediente nuevo.
7. `published` — publicación institucional activa.
8. `interviews_in_progress` — entrevistas y antecedentes en preparación.
9. `third_degree_review` — antecedentes conocidos en 3.er grado.
10. `third_degree_approved` — votación favorable en 3.er grado.
11. `ready_for_ballot` — publicación mínima cumplida y expediente completo.
12. `ballot_approved` — balotaje favorable en 1.er grado.
13. `rejected` — rechazo registrado con fecha y causas.
14. `eligible_for_ceremony_request` — habilitada para solicitar iniciación.
15. `closed` — expediente cerrado por finalización o causa registrada.

## Transiciones obligatorias

- `draft` → `presented_first_degree`: requiere formulario completo, patrocinante y tenida.
- `presented_first_degree` → `waiting_initial_review`: automática al registrar presentación.
- `waiting_initial_review` → `initial_review`: sólo después de 7 días como mínimo.
- `initial_review` → `approved_for_publication`: requiere unanimidad.
- `initial_review` → `observed`: si faltan antecedentes o existen observaciones.
- `observed` → estado de revisión correspondiente: corrección dentro del mismo expediente.
- `approved_for_publication` → `published`: publicación registrada por el flujo autorizado.
- `published` → `interviews_in_progress`: mantiene contador de publicación en paralelo.
- `interviews_in_progress` → `third_degree_review`: requiere tres informes y antecedentes exigidos.
- `third_degree_review` → `third_degree_approved`: requiere votación favorable.
- `third_degree_review` → `rejected`: si la votación rechaza.
- `third_degree_approved` → `ready_for_ballot`: requiere al menos 20 días corridos de publicación.
- `ready_for_ballot` → `ballot_approved`: balotaje favorable.
- `ready_for_ballot` → `rejected`: balotaje desfavorable.
- `ballot_approved` → `eligible_for_ceremony_request`: automática una vez persistido el resultado.

## Reglas de integridad

- Ningún cliente puede enviar directamente un estado final; las transiciones se ejecutan mediante comandos de dominio.
- Todos los plazos se validan en backend.
- Un expediente observado se corrige y reingresa sin duplicarse.
- Un rechazo conserva fecha, etapa, referencia y causas.
- Una nueva presentación posterior a rechazo debe vincularse al expediente previo y sólo puede comenzar cuando se cumpla el plazo institucional y conste la subsanación de causas.
- Las transiciones producen auditoría append-only.

## Datos mínimos del expediente

- persona;
- Taller que presenta;
- maestro/a patrocinante;
- fecha y referencia de presentación en 1.er grado;
- instancia de revisión inicial;
- resultado/unanimidad;
- publicación y fechas;
- entrevistas 1, 2 y 3;
- referencias a autobiografía y demás antecedentes;
- votación de 3.er grado;
- balotaje de 1.er grado;
- rechazos/nuevas presentaciones;
- historial de estados.

## Integración

Al llegar a `eligible_for_ceremony_request`, el sistema debe permitir crear la solicitud de iniciación reutilizando la identidad y el expediente existente. No se vuelve a solicitar información que ya existe y está vigente.
