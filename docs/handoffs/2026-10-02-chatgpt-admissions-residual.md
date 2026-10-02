# Handoff — residual Afiliación e Incorporación 2026

Fecha: 2026-10-02
Issue: #275
PR sucesor de #116: #276, draft.

## Base y decisión
La rama parte exactamente de dev `a3d08b7d615c7d02c88619cee54f6b1f5c2c7991`. PR #116 permanece histórico, draft y **NO FUSIONAR TODAVÍA**. El Sponsor confirmó que sus reglas normativas y funcionales siguen vigentes.

## Implementación registrada
El commit `026d0fe40247f079f1320af8952e5381027fa4ef` agrega sobre el modelo vigente los controles trazables de revisión Art. 2.3, indulto de Gran Maestría y reconocimiento de regularidad, utilizando `admission_decisions` y auditoría existente. `Program.cs` mapea el módulo. No se copió la rama histórica completa ni se alteraron tarifas, autoridades o `main`.

## Alcance residual pendiente
Siguen pendientes el circuito completo de Afiliación/Incorporación, firma manuscrita de Carta de Retiro Voluntario, Pacto de Paz/Amistad, cronología, comisión de información, traslado, materialización idempotente y la ejecución UAT-FLUJOS-017. La comisión histórica no se copia: requiere modelar tres integrantes y dispensa sobre el `dev` actual.

## Gates y límites
CI/Showcase/QA Installable deben ejecutarse sobre el SHA final exacto antes de cualquier squash. No instalar `srv01`, no hacer UAT institucional ni modificar `main`. Revalidar cruces con UI de Claude antes del siguiente commit funcional.
