# Continuidad — selección del perfil tras el alta

Issue #266. Fecha 05-10-2026. Base dev 71a3fe9277973c68a6f3832aec055b2d7d4f91a7; main6dfb9546a4873baff15955cf86abfd7d47e3d111. Corrección autorizada en el alcance del diseñador de perfiles dinámicos.

Reproducción en Pages fc3d57e9: crear QA Hospitalaria sólo lectura muestra éxito y código duplicado simultáneamente, con Guardar deshabilitado; el perfil existe y puede asignarse. AccessProfileDesigner.save no cambia editingCode tras create. La corrección selecciona el código persistido inmediatamente después de create/update, antes de guardar grants: evita el falso duplicado y permite reintentar sobre el mismo perfil si grants falla, sin intentar un segundo alta.

Reservas: frontend/src/AccessProfileDesigner.tsx y este handoff nuevo. No modificar App.tsx/identidad/workflows/migraciones. Sin cruces previstos con #116/#60/#58, comprobar inventario vivo antes del merge. Sin cambios de modelo/contratos/reglas de datos ni autorización institucional: sólo estado local del editor; los endpoints, versiones, validaciones de duplicado y grants siguen vigentes. Sin migración; no requiere nuevo diccionario.

Validación: reproducción previa; tests frontend/lint/build y gates exact-head CI/Showcase/QA; comprobación manual posterior al guardado y al reintento cuando sea posible. PR/gates/SHAs/publicación se completan en recibos GitHub/Drive. Conservar pendiente la prueba de fallo parcial si no se fuerza un fallo de grants en navegador.

Verificación pública anterior #324 completada: ZIP fc3d57e9,1025 MANIFEST,9 archivos críticos y bundle comprobados. Prueba manual sintética hospitalaria/view y revocación pasó: consulta sin Registrar movimiento y revocación bloquea datos. #266 sigue abierta para Gran Hospitalaria/regularidad/navegación/otros módulos. #191 definiciones contables; #66 UAT; #116 NO FUSIONAR TODAVÍA. Main/srv01/UAT pausados; despliegue QA pendiente.
