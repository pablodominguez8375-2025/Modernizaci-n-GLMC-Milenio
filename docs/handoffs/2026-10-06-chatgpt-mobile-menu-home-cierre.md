# Cierre técnico — Menú móvil → Inicio

- Reclamo documental: Issue #343; agente ChatGPT/Codex. Alcance: enlace único en START-HERE y continuidad verificable del PR #342.
- PR #342 integrado por squash en f3983654cd102b223f03a1b327e7597b6e97e800, desde dev 86302aaba94b6e6e7ceb6df2120c252854e229c5; main 6dfb9546a4873baff15955cf86abfd7d47e3d111 sin promoción.
- HEAD funcional 71fa1658c6a17924f515b4df23f268b7057c00a2; CI37479375958, Showcase37479376284 y QA37479376377 SUCCESS exact-head.
- Resultado: Menú se cierra al seleccionar Inicio aunque la vista Inicio ya esté activa; igual protección para Mi ficha/Pendientes, Agenda y Avisos.
- Pruebas: 434 frontend; lint y TypeScript/Vite correctos; 505 backend en CI sin fallos/omitidos. Seis regresiones reproducidas antes y resueltas después.
- QA funcional: artifact11420556935, digest externo d997875bf11bbb15d0d76eb55b26f4990a33d68d7eeb1e5f80a137121ec2b53d; ZIP interno de4a49dd412291bfcdace6dfa7d8ee78ff94b4c3a1a4c7610bee73cba0b314bd; CRC y 1060 manifest entries PASS; fuentes MobileTabBar y test exactas.
- Sin cambios de modelo/contratos/reglas de datos, sin migraciones ni cambios de permisos. Corrección UI pedida directamente por el PO.
- Drive revisado sin novedades posteriores a la revisión previa; propuesta de reorganización de menús sigue pendiente del PO.
- Registro final de SHA dev/Pages, paquete QA público, SHA-256, workflows postmerge y adenda Drive: [recibo persistente PR #342](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/342#issuecomment-6018492830), actualizado después de comprobar la publicación.
- Despliegue QA pendiente; srv01/UAT pausados (#97). No promover main ni ejecutar la propuesta de menús sin instrucción del PO.
