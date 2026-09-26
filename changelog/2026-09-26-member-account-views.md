# Vistas personales de Mi cuenta — 26-09-2026

## Hallazgo

El sistema Logial referencial separa la cuenta del miembro en Resumen, Mi ficha, Datos contables, Datos de asistencia y Mis planchas. Centenario ya contiene parte de esas vistas y los datos del autoservicio, pero los concentraba en la página «Mi ficha».

## Revisión de cobertura

- **Resumen:** próximas tenidas, avisos y calendario ya existentes.
- **Mi ficha:** identidad, datos institucionales y edición auditada del contacto propio ya existentes.
- **Datos contables:** cartola personal de Tesorería disponible; Hospitalaria expone sólo un estado general, no movimientos personales ni ayudas.
- **Datos de asistencia:** asistencia de los últimos 12 meses e historial de instrucción ya disponibles; no se ofrece selector anual.
- **Mis planchas:** la pantalla existente no dispone hoy de un endpoint de autoservicio. El requisito confirmado es listar sólo planchas propias y permitir una carga asociada a una Tenida únicamente si ésta aún no tiene plancha; no se reemplazan trabajos existentes ni se consultan los de otros hermanos.

## Corrección aplicada en rama

La pantalla existente del Portal del Hermano incorpora cinco vistas internas accesibles, con selección dorada institucional y controles responsivos. Reutiliza el perfil auto-restringido y sus datos actuales; conserva «Mi ficha» como acceso global. La vista «Mis planchas de trabajo» deja explícito su comportamiento requerido y su estado pendiente: la carga personal necesita un endpoint seguro que compruebe autoría, Tenida elegible y ausencia de plancha asociada. La carga genérica protegida para Secretaría no sirve para un miembro.

## Verificación local

Frontend: 205 pruebas Vitest aprobadas antes del nuevo test de contrato, lint sin errores y build de producción exitoso. Se agregó cobertura para las cinco vistas; corresponde reejecutar Vitest/lint/build después del último cambio y confirmar el total actualizado antes de abrir PR.

## Estado del corte

Vistas de resumen, ficha, contabilidad y asistencia implementadas; autoservicio real de planchas pendiente en `feature/member-account-views`, derivada de `dev@14019d2887a782250021a1eac2f3e8dc936e360c`. No se abrirá PR del corte como completo hasta resolver el contrato seguro de planchas o separar explícitamente esa capacidad. No se ha publicado Pages ni generado/validado un artefacto QA para este cambio. `srv01` sigue en pausa; no se ejecutan instalación, smoke autenticado, regresión física ni UAT. Issue #97 sigue abierto; `main` permanece intacta.
