# Vistas personales de Mi cuenta — 26-09-2026

## Hallazgo

El sistema Logial referencial separa la cuenta del miembro en Resumen, Mi ficha, Datos contables, Datos de asistencia y Mis planchas. Centenario ya contiene parte de esas vistas y los datos del autoservicio, pero los concentraba en la página «Mi ficha».

## Revisión de cobertura

- **Resumen:** próximas tenidas, avisos y calendario ya existentes.
- **Mi ficha:** identidad, datos institucionales y edición auditada del contacto propio ya existentes.
- **Datos contables:** cartola personal de Tesorería disponible; Hospitalaria expone sólo un estado general, no movimientos personales ni ayudas.
- **Datos de asistencia:** asistencia de los últimos 12 meses e historial de instrucción ya disponibles; no se ofrece selector anual.
- **Mis planchas:** listar únicamente las propias. El autor puede cargar y reemplazar con nueva versión su plancha aunque Secretaría haya hecho la carga inicial. La nueva versión conserva el mismo recurso, descripción corta y asociación a la Tenida; tras integridad y antivirus, queda como versión vigente y se publica en «Planchas de trabajo», clasificada por grado efectivo del autor. Secretaría también puede cargar/actualizar dentro de su ámbito.

## Corrección aplicada en rama

La pantalla existente del Portal del Hermano incorpora cinco vistas internas accesibles, con selección dorada institucional y controles responsivos. Reutiliza el perfil auto-restringido y sus datos actuales; conserva «Mi ficha» como acceso global. La vista «Mis planchas de trabajo» requiere un endpoint que verifique la autoría en backend, limite reemplazos al mismo autor y conserve el historial. Una plancha se publica en Biblioteca una vez íntegra y limpia, en vez de exponerse durante la carga.

## Verificación local

Frontend: 205 pruebas Vitest aprobadas antes del nuevo test de contrato, lint sin errores y build de producción exitoso. Se agregó cobertura para las cinco vistas; corresponde reejecutar Vitest/lint/build después del último cambio y confirmar el total actualizado antes de abrir PR.

## Estado del corte

Vistas de resumen, ficha, contabilidad y asistencia implementadas; el flujo integrado de carga/versionado/publicación automática de planchas sigue en desarrollo en `feature/member-account-views`, derivada de `dev@14019d2887a782250021a1eac2f3e8dc936e360c`. PR/CI exact-head pendientes. No se ha publicado Pages ni generado/validado un artefacto QA para este cambio. `srv01` sigue en pausa; no se ejecutan instalación, smoke autenticado, regresión física ni UAT. Issue #97 sigue abierto; `main` permanece intacta.
