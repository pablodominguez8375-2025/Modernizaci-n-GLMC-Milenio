# #574 — Política de seguridad preparatoria «Ver como» (ChatGPT)

Fecha: 23-10-2026. Base dev `18fbbfed7cede2823c0f3fec2760f2aa528b5f48`, main estable `b77de59e170ebfa2abec71b418f6d8b0407ba7ad`. Issue #574 asignado a `agente:chatgpt`; coordinación según PMGM-GOV-003.

## Objetivo acotado
Construir política de backend y pruebas unitarias puras para que un futuro flujo «Ver como» de soporte sea solo lectura, no permita autoobservación ni acceso a otras cuentas privilegiadas, exija un motivo y caduque a más tardar en 30 minutos. **No habilita endpoint de suplantación, no entrega token ajeno ni accede a credenciales**, no constituye sesión persistente ni auditoría.

## Contrato seguro esperado
- Actor real mantiene su propia identidad y permisos de soporte expresos verificados en el servidor, sin asignación ficticia de cargos de Taller.
- Requerir sujeto operador/objetivo reales y distintos, rechazo de objetivos administradores y de duración mayor a 30 min; motivo específico obligatorio.
- Solo GET/HEAD sobre recursos permitidos explícitamente, con lectura de objetivo autorizada también en servidor. Denegar por defecto POST/PUT/PATCH/DELETE y métodos desconocidos incluso durante sesión activa.
- Una sesión debe pertenecer al operador que la inició y expirar irrevocablemente; la prueba de modo lectura no autoriza a ver RUT completo, contacto reservado, pagos ni documentos sensibles.

## Bloques pendientes antes de uso real
Implementar emisión/revocación y validación de sesión, permisos dinámicos `soporte.ver_como` acotados por perfil, selección/autenticación del objetivo, auditoría inmutable de inicio/lecturas/salida, notificación al usuario según revisión jurídica (Ley 21.719), integración en cada endpoint habilitado, protección en red/servidor, exclusión de GET con efectos colaterales y UAT. La UI (selector/banda fija/botón Salir) pertenece a Clau tras API integrada y aceptación visual.

## Límites
No tocar la PR #655 de cuentas (mismo módulo administrativo de #604), ni ramas UI de Claude, ni `main`, producción ni QA físico. Cualquier integración requiere 3/3 controles CI/Showcase/Instalable exact-head, `behind=0` y revisión.
