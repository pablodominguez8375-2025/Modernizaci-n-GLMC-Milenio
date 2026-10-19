# Handoff #533 — Etapa B: trazabilidad de Asamblea y fuente de saldo

Fecha 2026-10-19. Rama `feature/gran-asamblea-persistencia-saldo-20261019-gpt`, base `dev@1e670779bf6be9be9332347c4c40d1f135ebcc6c`, responsable ChatGPT. QA se mantiene en otro hilo; NO se tocará host, entorno, credenciales ni `main`.

## Frontera de trabajo
Crear modelos y migración aditiva propiedad del contexto Régimen Interior y política conservadora de proyección de saldo desde cuadros de Gran Tesorería. No habilitar endpoints de sufragio ni modificar UI hasta completar integración con los datos reales y controles de auditoría.

## Decisiones vigentes
- Un saldo **positivo** con Gran Tesorería bloquea a todos los electores de ese Taller; ningún umbral configurable. Solo Régimen Interior puede levantar/restablecer por resolución motivada, registrada y reversible.
- Dos exportaciones: todos los asambleístas vs solo sufragantes; globalmente por apellidos y nombres es-CL, RUT para desempate, firma en blanco, sin revelar causas disciplinarias ni votos.
- Estado desconocido/ausencia de deuda comprobable jamás es «al día». Múltiples monedas no se suman sin cotización aprobada; rectificaciones se resuelven por trazabilidad y corte.

## Invariantes de seguridad
La persistencia provisional no autoriza inserción masiva ni cierre automáticamente. La futura evaluación debe verificar que la calidad de Venerable Maestro está acreditada, el estado de membresía/derechos del Tribunal y el saldo GT provienen de fuente legítima. Cierre/auditoría/CSV/PDF requieren endpoints, transacción y controles RBAC posteriores. Protección Ley 21.719 y datos personales; no recopilar votos.
