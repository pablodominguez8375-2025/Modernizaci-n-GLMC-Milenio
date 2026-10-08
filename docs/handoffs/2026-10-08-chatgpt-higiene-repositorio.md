# Handoff — higiene de repositorio y procesos — 08-10-2026

## Estado vivo al abrir este corte

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- `dev@08d262f3bdc1604323c5fb20504f932bd70b7982`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin promoción.
- Issue de mantenimiento: #369, reservada por `agente:chatgpt`.
- Claude trabaja en Issue #371 / PR #372 sobre `AGENTS.md` y `docs/PMGM-GOV-003-coordinacion-multi-ia.md`; este corte no toca esos archivos.
- Issue #97 mantiene `srv01`, QA física y UAT pausados.

## Limpieza ya completada

1. PR #368 / Issue #59 integrados y cerrados. Past Activo queda separado de Retiro voluntario / En sueño.
2. PR históricos #58 y #60 cerrados sin merge como obsoletos/sustituidos; sus residuales legítimos permanecen en Issues vivos.
3. PR #370 integrado a `dev` como `08d262f3bdc1604323c5fb20504f932bd70b7982`:
   - CI, QA y Showcase usan concurrencia por PR/ref y cancelan ejecuciones obsoletas.
   - QA/Showcase ya no se duplican por `pull_request: closed` + `push: dev`.
   - PR usa matriz visual rápida: 9 escenarios × 3 viewports, 45 PNG.
   - `push: dev` conserva matriz completa: 790 PNG; 21 perfiles; 622 estados de menú/subvista.
4. Post-merge de `08d262f3...`: PMGM CI run 37774485320 SUCCESS; Pre-UAT 37774485319 SUCCESS; Showcase/Pages 37774485221 SUCCESS; QA Installable 37774485150 SUCCESS.
5. Publicación de Pages para `08d262f3...`: paquete `Proyecto-Centenario-QA-srv01-08d262f3bdc1.zip`, SHA256 `9ff46cbec36e291f0281d6e988f819b33aefd58916922fc8778c2cf8b572f201`.

## Hallazgos de higiene

- El primer inventario encontró 1.118 archivos versionados, sin `node_modules`, `bin`, `obj`, `dist`, ZIP, coverage, artifacts ni backups dentro de Git.
- Había 103 archivos en `docs/handoffs/`; no se borran ni reescriben porque forman parte de la trazabilidad y GOV-003 exige handoffs nuevos por agente.
- El inventario inicial encontró 295 ramas: 81 `docs/*`, 6 `codex/*` y 164 `feat/*`/ `feature/*`. Su borrado debe comprobar PR/Issue y ancestry antes de cualquier operación destructiva.
- `START-HERE.md` acumula checkpoints históricos. Por GOV-003 §6 no se reescriben bloques: este corte sólo añade al inicio una referencia a este handoff actual.
- `.github/workflows/postrc-installable-package.yml` sólo conserva un push trigger para `feature/treasury-payment-table-v1`; esa rama es ancestro de `dev` y quedó 567 commits atrás en la auditoría. Los flujos actuales Pre-UAT, QA y Deployment cubren los paquetes vigentes. Se retira ese workflow del árbol actual; Git conserva su historia.

## Drive revisado

Carpeta oficial `Proyecto Centenario` revisada el 08-10-2026 antes de este corte. El archivo más reciente es la Línea Base Maestra, modificada hoy por los registros de #368/#369. No apareció un documento oficial nuevo posterior que cambie alcance, reglas o prioridades; las adendas más recientes siguen siendo las de 07-10 ya conocidas/consolidadas según corresponda.

## Continuación

- No tocar `main`.
- No desplegar `srv01`.
- Mantener coordinación con PR #372 de Claude sin editar sus archivos.
- Después de este corte, el residual de #369 es la depuración física de ramas históricas. El conector GitHub disponible permite auditar y cerrar PR, pero no expone una operación `delete-ref`; no simular ni forzar borrado de ramas sin una herramienta autorizada.
- Si se retoma la limpieza, volver a comprobar HEAD, PR abiertos, ramas y Drive antes de actuar.

## Refresco de base durante el corte

- Mientras corrían los gates, Claude integró PR #372 y luego PR #376; `dev` avanzó a `0e1fbb833e6436a77da7c9a8b1cf186ee9e55612`.
- #373 se refresca sobre ese `dev` antes de integrar; no hay solapamiento de archivos con #372 ni #376.
- Se repiten gates exact-head después del refresco.

## Novedad Drive posterior al refresco

- Revisada adenda Claude del 08-10-2026 para PR #376 (UI QA v0.93).
- `dev@0e1fbb8` separa claramente Tesorería y Hospitalaria dentro de Mi ficha → Mis pagos; no cambia el alcance de #369/#373.
- La descarga de comprobantes sigue pendiente en Issue #36.
- Esta adenda complementa el proceso liviano de PR #372.

## Decisión sobre PR documentales

- Se evaluó omitir CI/QA/Showcase en PR que sólo cambien `START-HERE.md` o `docs/handoffs/**`.
- La protección de rama `dev` no es legible con la credencial del conector (GitHub 403), por lo que no se puede confirmar si esos checks son obligatorios.
- Para evitar que un PR documental quede bloqueado en estado pending, se descartó `paths-ignore` en este corte.
- Se mantienen las optimizaciones seguras y ya verificadas: concurrency/cancel-in-progress, Showcase PR reducido, eliminación de duplicados post-merge y retiro del workflow Post-RC histórico.
