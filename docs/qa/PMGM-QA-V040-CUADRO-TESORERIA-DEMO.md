# PMGM-QA-V040 — Cuadro mensual de Gran Tesorería en demo e instalable

**Estado:** implementado en rama de función, pendiente de integración a `dev`  
**Fuente:** `CUADRO PAGO GRAN TESORERÍA.xlsx` institucional 2026  
**Datos de demostración:** ficticios

## Alcance demostrable

1. seleccionar Taller y crear el cuadro del período;
2. generar la nómina desde pertenencias vigentes;
3. separar integrantes por grado y conservar el cargo vigente;
4. mostrar cuota base, ajuste, total y referencia de la plancha que respalda una rebaja;
5. registrar transferencia o depósito;
6. calcular total esperado, pagado y diferencia;
7. enviar el cuadro a Gran Tesorería;
8. conciliar únicamente con diferencia cero e identidades resueltas;
9. actualizar la regularidad financiera desde el backend al conciliar.

La pantalla se incorpora al frontend que consumen tanto el showcase como el paquete instalable posterior a RC1. La versión visual se identifica como `UI QA v0.40`.

## Ruta de demostración

1. escoger el perfil **Gran Tesorero · Demostración**;
2. abrir **Gestión institucional → Cuadro mensual**;
3. pulsar **Crear cuadro del mes**;
4. pulsar **Generar nómina**;
5. registrar la transferencia sugerida;
6. enviar y conciliar.

## Controles preservados

- El RUT no se expone en la demostración pública.
- Los valores del archivo institucional son ejemplos de prueba, no un tarifario normativo permanente.
- Toda rebaja necesita referencia de autorización.
- La conciliación no admite diferencias ni identidades pendientes.
- El resultado financiero se integra con elegibilidad de ceremonias sin duplicar la fuente de verdad.
