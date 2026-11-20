# Continuidad IA — entrada mínima verificable

> **Índice operativo, NO fuente autónoma de estado ni nueva norma.** Su vigencia depende de comprobar GitHub y Drive en cada sesión. Reemplaza lecturas repetitivas, no reemplaza `AGENTS.md`, `START-HERE.md` ni `PMGM-GOV-003`. Los SHA aquí no son HEAD vivo.

## Inicio rápido (en este orden)

1. **GitHub, en vivo:** resolver SHA de ramas `dev` y `main` mediante refs; no copiar SHAs del histórico. Leer `START-HERE.md`, `AGENTS.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md` en el HEAD obtenido. Si hay contradicción, contrastar con normas y Línea Base oficial.
2. **Tablero:** listar PR abiertos **y todos sus archivos**, etiquetas `agente:*` y último comentario del Issue pertinente. `docs/handoffs/*` se lee por fecha y materia, NO toda la cronología. Sin reclamo de Issue + PR draft no se inicia trabajo.
3. **Drive por diferencias:** dentro de la carpeta oficial «Proyecto Centenario», listar metadatos modificados desde la última revisión comprobada. Leer solo Línea Base, START HERE o adendas que afecten el cambio. Los títulos, fechas y SHAs de adendas son antecedentes, jamás estado en vivo.
4. **Normas si corresponde:** cuando cambien cargos, atribuciones, firmas, permisos, ceremonias o procedimientos, revisar Constitución/Reglamento y formularios institucionales antes de implementar.
5. **Clasificar lo verificado:** `EN CÓDIGO / CI / RELEASE / QA FÍSICO / UAT / MAIN`; distinguir `PASS`, `NO_GO`, `PENDIENTE`, `UNKNOWN`, con fuente, SHA, hora y autorización. Una prueba sintética no constituye QA físico.
6. **Cerrar:** dejar un único resumen en PR/Issue y handoff nuevo si permanece trabajo para otro turno; una adenda fechada en Drive por sesión/lote; registrar únicamente deltas y enlaces. Nunca pegar secretos, tokens, PIN, firmas, IP ni huellas.

## Rutas autoritativas (enlaces estables, contenido leído de nuevo)

- Repositorio: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio
- Gobierno vivo: `AGENTS.md`, `START-HERE.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md`.
- Coordinación y automatización: [Issue #821](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/821).
- QA físico único `glmdch`: [Issue #525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525). `srv01` es histórico.
- Carpeta Drive oficial: https://drive.google.com/drive/folders/1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO
- Línea Base: https://docs.google.com/document/d/1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM/edit
- Índice liviano Drive (10-11): https://docs.google.com/document/d/1oRtKkU7LFQCo-U9i-TlrFl49VZ0lh1V5ceQZDkeKnQ0/edit
- Histórico QA / continuidad: https://docs.google.com/document/d/1gyhxyWULLLHUjm32zLu5fsrWI2wdfQlgcSm7s98LFZg/edit

## Comando mental de toda IA

**Verificar → reclamar → modificar en rama propia → probar exact-head → documentar deltas → entregar estados separados.** 
No leer documentos completos de cientos de páginas, logs ni SHA antiguos salvo que un resultado del último estado vivo lo requiera. No borrar, mover ni compactar historiales auditables para ahorrar tokens: usar enlaces y búsqueda acotada.

Para los campos mínimos verificables y criterios de fallo, consultar [contrato de estado](./AI-STATUS-EVIDENCE-CONTRACT.md).
