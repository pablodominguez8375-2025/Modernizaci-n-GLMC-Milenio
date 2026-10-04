# Handoff — Claude — Avisos con una acción visible e íconos SVG — 02-10-2026

- **Issue:** #282 (`agente:claude`). Aprobado por el PO el 02-10-2026, como pasos siguientes del PR #281.
- **Revisión previa de Drive (GOV-003 §11 4.1):** no hay archivos nuevos. Las planillas «Mesa de Ayuda» y «Gestion Adm GLMCH» siguen sin abrir, a la espera de decisión del PO.
- **Base:** `dev@824e984`.
- **Versión:** UI QA v0.75.

## Cambios

### Avisos (`NotificationsPage.tsx`)

- **Una sola acción visible por aviso.** Es la acción principal: «Revisar y decidir», «Ver ceremonias», etc., o «Marcar como leído» si el aviso no tiene acción. Cuando hay una acción principal, «Marcar como leído» pasa al menú «⋯».
- **Una sola etiqueta de estado por aviso,** por prioridad: «Requiere decisión», luego «Obligatoria», luego «Sin leer».
- **Nuevo botón «Marcar todos como leídos».** Pide confirmación y usa el mismo `notificationApi.markRead`. Los avisos que «Requieren decisión» quedan pendientes.
- **Filtros con contador:** «Pendientes (n)» y «Obligatorias (n)». En celular se ocultan la descripción y los contadores grandes del encabezado, y el texto de cada aviso se limita a 3 líneas.
- **Íconos:** los glifos de texto (◉ ◎ ▣ ◇ □ ✦) se reemplazaron por `InstitutionalIcon` SVG en azul institucional sobre el fondo dorado suave.

### Mi ficha

- **Íconos de estado:** «$» y «♥» se reemplazaron por los íconos SVG «treasury» y «hospitalaria», en dorado sobre azul.

## Verificación

- 293 tests en verde. tsc y oxlint en OK.
- Prueba con Playwright en escritorio y en celular de 390 px:
  - sin desbordes;
  - la confirmación de «Marcar todos» funciona;
  - Avisos en celular baja de 2.951 a 2.451 px.
- Auditoría móvil completa en local.

## Impacto en datos (PMGM-GOV-004)

**Ninguno.** Se usa la operación existente `markRead` por aviso; no hay contratos nuevos.
