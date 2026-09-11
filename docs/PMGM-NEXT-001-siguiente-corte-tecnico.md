# PMGM-NEXT-001 — Siguiente corte técnico

El siguiente corte de desarrollo en `dev` debe comenzar por el expediente de insinuación y sus transiciones reglamentarias. No se debe implementar primero la interfaz; la fuente de verdad será el dominio/backend.

## Primera entrega demostrable
1. crear expediente;
2. registrar presentación en 1.er grado;
3. impedir revisión antes de 7 días;
4. registrar unanimidad/no unanimidad;
5. publicar;
6. calcular 20 días corridos;
7. registrar entrevistas y antecedentes;
8. habilitar o bloquear revisión de 3.er grado;
9. habilitar o bloquear balotaje;
10. producir historial auditable de estados.

## Segunda entrega
Conectar `ballot_approved` con creación de solicitud de iniciación y con PMGM-ARCH-003.

## Pruebas mínimas
- transición válida;
- salto de etapa rechazado;
- plazo insuficiente rechazado;
- corrección dentro del mismo expediente;
- reingreso tras rechazo con vínculo histórico.
