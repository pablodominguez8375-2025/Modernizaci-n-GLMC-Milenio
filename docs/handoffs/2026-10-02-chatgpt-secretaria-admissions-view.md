# Handoff — vista Secretaría Afiliación e Incorporación — 2026-10-02

## Corte validado
- PR: #276 (draft), Issue: #275.
- Rama: feature/admissions-residual-275.
- SHA funcional/documental validado: a6c11ece5a787239815cfc808fa919de3a17aad1.
- Base sincronizada: dev@6d44e21673f19b958c70e9e7bcd6638f8eed0cff.
- Gates del SHA exacto: PMGM CI #1820 SUCCESS; PMGM Showcase #1178 SUCCESS; QA Installable #816 SUCCESS.

## Entrega
Se creó la vista independiente de Secretaría «Afiliación e Incorporación», separada de Insinuados y del circuito normal de iniciación. La vista busca un hermano activo existente, reutiliza su Person/Member, permite Afiliación simple o activación, y permite Incorporación con Obediencia, Taller, grado y Pacto de Paz/Amistad. El alta usa el endpoint vigente de expedientes de admisiones y conserva trazabilidad.

Archivos principales:
- frontend/src/AdmissionsPage.tsx
- frontend/src/admissions.css
- frontend/src/AdmissionsPage.test.ts
- frontend/src/App.tsx
- frontend/src/api/pmgmApi.ts

## Límites
No se crean personas duplicadas, no se reutiliza CandidateWorkshopIntakePage, no se modifica main y no se instala srv01. La PR #116 sigue sin fusionar y su regla «NO FUSIONAR TODAVÍA» permanece vigente.

## Pendientes
Quedan fuera de este corte el procedimiento explícito de traslado, materialización idempotente completa y UAT institucional. No inventar tarifas, autoridades ni reglas contables; requieren definición/documentación antes de programar.

## Continuidad
Antes de cualquier commit posterior, volver a comparar la rama con el HEAD vivo de dev y repetir los tres gates sobre el SHA exacto.