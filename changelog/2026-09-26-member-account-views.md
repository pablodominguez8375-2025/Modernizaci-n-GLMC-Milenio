# Vistas personales de Mi cuenta — 26-09-2026

## Hallazgo

El sistema Logial referencial separa la cuenta del miembro en Resumen, Mi ficha, Datos contables, Datos de asistencia y Mis planchas. Centenario ya contiene parte de esas vistas y los datos del autoservicio, pero los concentraba en la página «Mi ficha».

## Revisión de cobertura

- **Resumen:** próximas tenidas, avisos y calendario ya existentes.
- **Mi ficha:** identidad, datos institucionales y edición auditada del contacto propio ya existentes.
- **Datos contables:** cartola personal de Tesorería disponible; Hospitalaria expone sólo un estado general, no movimientos personales ni ayudas.
- **Datos de asistencia:** asistencia de los últimos 12 meses e historial de instrucción ya disponibles; no se ofrece selector anual.
- **Mis planchas:** no existe hoy una vista personal respaldada por el endpoint de autoservicio. Los permisos de documentos no autorizan descarga o carga personal desde esta pantalla.

## Corrección aplicada en rama

La pantalla existente del Portal del Hermano incorpora cinco vistas internas accesibles, con selección dorada institucional y controles responsivos. Reutiliza el perfil auto-restringido y sus datos actuales; conserva «Mi ficha» como acceso global. La vista de planchas explica su disponibilidad pendiente y no muestra adjuntos. No cambian rutas, permisos, lógica institucional ni API; tampoco se crean mocks paralelos.

## Verificación local

Frontend: 205 pruebas Vitest aprobadas antes del nuevo test de contrato, lint sin errores y build de producción exitoso. Se agregó cobertura para las cinco vistas; corresponde reejecutar Vitest/lint/build después del último cambio y confirmar el total actualizado antes de abrir PR.

## Estado del corte

Implementado en `feature/member-account-views`, derivada de `dev@14019d2887a782250021a1eac2f3e8dc936e360c`. Pendiente de PR/CI exact-head, aprobación e integración en `dev`. No se ha publicado Pages ni generado/validado un artefacto QA para este cambio. `srv01` sigue en pausa; no se ejecutan instalación, smoke autenticado, regresión física ni UAT. Este trabajo no implica QA/UAT aceptadas. Issue #97 sigue abierto; `main` permanece intacta.
