# ADR (borrador) — Identidad de Talleres en la Carga inicial (#694 / #1049)

Estado: PROPUESTO / diseño candidato aceptado por ChatGPT (#1049 7038868173). Decisión final de producto: Sponsor. NO autoriza Apply, datos reales, migración ni merge.

## Decisión candidata
- Identidad en BD = **(Number, OrienteCode)**. `workshopCode` es **solo referencia local del CSV** (enlaza Hermanos/Cargos con su Taller dentro del mismo lote); no se persiste ni se compara con la BD.
- Sin migración (no invalida el release firmado). Un campo nuevo `Organization.ExternalCode` se pospone hasta después del corte de Talleres.
- `workshopCode ≡ Number`: RECHAZADA (Number solo no es clave segura; se repite entre orientes).

## Política vigente vs LEGACY
| Política | Estado |
|---|---|
| `InitialLoadWorkshopCompositeKeyPolicy` + `InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity` | **Operativa candidata** (solo planificador offline; PREVIEW_ONLY) |
| `InitialLoadWorkshopMatchPolicy.Inspect` + `InitialLoadWorkshopBatchPolicy.Inspect` (por `Code`) | **LEGACY, NO OPERATIVA.** El lector EF real devuelve `Code` vacío (no hay mapeo de workshopCode en `Organization`): con esa política todo snapshot real sería «ambiguo». No debe volver a usarse en el planificador |

## Reglas fijadas por pruebas
- Fail-closed: snapshot ausente, incompleto o duplicado; lote vacío; claves entrantes duplicadas por Code o por identidad; atributos contradictorios (nombre/ciudad/país).
- Una sola fila contradictoria bloquea el lote completo, sin conteos parciales.
- Mismo `Number` en distinto `OrienteCode` NO colisiona.
- **La normalización solo detecta colisiones** (trim, ceros a la izquierda, mayúsculas/minúsculas de `OrienteCode`): una variante de forma (`23`/`023`/` 23`, `SCL`/`scl`, espacios/tab/NBSP) se trata como AMBIGUA y bloquea; la coincidencia exacta sigue siendo Ordinal.
- **Prohibido inferir identidad canónica a partir de la normalización** hasta que exista evidencia QA del formato institucional real (lectura de datos reales no autorizada hoy).

## Pendientes / riesgos
1. Formato canónico real de `Number`/`OrienteCode` sin verificar en QA.
2. Snapshot de lectura sin transacción de solo lectura declarada (consistencia entre lectura y futuro Apply).
3. Un eventual Apply debe revalidar dentro de la transacción de escritura (no implementado; fuera de alcance).
4. Resumen/UI: `PREVIEW_VALID` es solo validación estructural del CSV (`structuralValidationPassed`), no reconciliación con BD (`databaseReconciliationPerformed=false`).

## Pruebas
- `InitialLoadWorkshopLegacyCodePolicyTests`: caracteriza la política LEGACY por Code (no contrato).
- `InitialLoadWorkshopCompositeComplementTests`: complementa (sin duplicar) las de #1049: filas persistidas con Code vacío no son duplicados, la ruta LEGACY rechazaría el snapshot real, mismo workshopCode en filas distintas bloquea, whitespace variantes (tab/NBSP/salto de línea), `0`/`00`, un conflicto bloquea todo, coincidencia exacta = skip.

Restricciones: PREVIEW_ONLY, `supportsApply=false`, sin datos reales, sin migración, sin main/glmdch.
