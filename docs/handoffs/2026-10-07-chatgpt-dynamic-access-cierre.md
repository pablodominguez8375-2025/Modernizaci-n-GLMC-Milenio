# Handoff postmerge — #266

Reserva ChatGPT del cierre documental posterior a PR #360, desde dev `6c4bdd07122f45b7903ce13db95b8fec6fe2c7ff`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

PR #360 integrada: head probado `0aca94304f03881d8a1869163c6b44c34ed18d24`, árbol `01ae62f5d31b529385051e938f33656a77c7e9f9` idéntico al integrado. CI 37678667833, Showcase 37678667836 y QA 37678667832 SUCCESS. Backend 540/540, frontend 462, lint/build PASS. Paquete QA exact-head: ZIP interno SHA-256 `62688d45bd081314f87413ed803f983dbba5e11255b0adff047f1ebe90d48870`, MANIFEST 1109/1109 y seis fuentes críticas verificadas.

Reservas documentales: este archivo nuevo y una línea de START-HERE según GOV-003 §6. No cambios funcionales. Publicación postmerge y cierre GitHub/Drive en comprobación; no declarar finalizado hasta recibo verificado. Despliegue QA pendiente; srv01/UAT pausados por #97; no promover main.

## Resultado funcional integrado

El administrador autorizado puede crear y editar perfiles personalizados y asignar/revocar sus permisos. Los perfiles de sistema conservan sus protecciones. Vistas y operaciones separan ver, crear, escribir, editar, baja lógica e imprimir; los permisos técnicos se intersectan con autoridad institucional, vigencia y Taller. Revocar o expirar una asignación histórica no devuelve acceso legado.

PR #360 afecta 14 archivos: filtro DynamicViewAccess y Program.cs; pruebas unitarias/HTTP; App.tsx, proyección propia, hook y manifiesto/proxies de clientes; pruebas frontend; handoff de implementación y registro GOV-004. Sin schema, migración, CSS, identidad ni reglas institucionales nuevos. Controles financieros específicos preservados.

El filtro usa el Taller persistido del recurso y no acepta un selector falso. Consultas sin selector y navegación exigen permiso en todos los contextos gestionados del sujeto; esta intersección conservadora evita exposición de colecciones parciales. La proyección propia no devuelve catálogo, sujetos ni asignaciones. Impresión requiere autoridad del Taller y grant separado, queda auditada antes de imprimir; descargar/exportar se controla como lectura. Regularidad Hospitalaria propia mantiene contrato de #355, identidad y vista member, sin inventar regularidad administrativa.

Pruebas HTTP: denegación multi-módulo, continuidad de crear/editar perfiles, crear sin escribir, origen persistente contra selector falso, impresión auditada, denegación institucional con grant técnico y revocación. Frontend: ausencia de fallback, separación de acciones, llamadas indirectas, cliente sin proyección y permisos por contexto.

## Evidencias y continuidad

- Base funcional: dev c554a6e8d08d46cfe1f3f70f0552bc495b56ea3c.
- Integración funcional: dev 6c4bdd07122f45b7903ce13db95b8fec6fe2c7ff, PR #360 squash.
- Exact-head CI 37678667833, Showcase 37678667836 y QA 37678667832: SUCCESS. Backend 540/540, frontend 462; lint/build y gates privacidad (12 actividades), clasificación (113 campos / 6 catálogos) y migraciones (63) PASS.
- QA exact-head artifact 11509000947: digest exterior 1cadd5584843ee6f257b17610bc9e0355c8c96ab03362260261280f89b1d6ac1; ZIP interno y MANIFEST verificados arriba.
- Gates push de integración: CI 37680430242, Showcase 37680430228, QA 37680430256 y Pre-UAT 37680430230. QA/Pre-UAT SUCCESS; CI y publicación postmerge aún en ejecución al redactar. Su resultado final se registra en Issue #266 y Drive.
- PR documental #361 agrega este recibo y una línea a START-HERE. SHA documental integrado, gates, SHA público y checksum final se registran en el recibo de Issue #266 y Línea Base Maestra después de comprobarlos; consultar esos registros para superseder este estado intermedio.
- Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . Verificar downloads/qa-current.json.sourceSha contra dev vivo y SHA-256/BUILD-INFO/MANIFEST del ZIP público antes de declarar cierre.
- Drive: Línea Base Maestra, carpeta Proyecto Centenario, id 1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM. Contenido preservado y recibo añadido con lectura de retorno. Revisión del corte sin nuevas reglas aplicables.
- Main permanece 6dfb9546a4873baff15955cf86abfd7d47e3d111. Despliegue QA pendiente; srv01/QA física/UAT pausa #97. Checks y paquetes no prueban aceptación institucional.
- Al continuar consultar dev vivo, PRs/reservas, Issue #266 y Línea Base. Preservar ramas históricas #58/#60; no promover main. Este trabajo termina el alcance técnico de #266 cuando publicación y registros finales estén verificados; la validación operacional continúa pendiente bajo #97.
