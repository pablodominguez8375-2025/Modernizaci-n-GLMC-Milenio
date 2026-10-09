# Handoff — #47 paso 1: requisito de avance en autorización real

- Rama `fix/advancement-authorization-gate-20261009-gpt`, base inicial `dev@f138abe456b94f64b3c995a65322f493963edb0e`.
- Objetivo: no autorizar Aumento de Salario ni Exaltación sin decisión verificable de la política de avance. Mostrar en matriz que falta evaluación cuando no hay umbrales versionados y evidencias; bloquear aun cuando los vistos buenos financieros estén completos. Sin inventar mínimos.
- Este corte **no** configura ni calcula automáticamente datos reales; quedan próximos el proyecto de reglas configurables, cálculo Gestión Logial/Docencia/Planchas, snapshot/auditoría y UI administrativa. Issue #47 debe permanecer abierto.
- Iniciación y otras ceremonias no requieren requisitos de avance. QA física `srv01` sigue pausada por Issue #97. No cambiar `main` ni funciones de Claude.
