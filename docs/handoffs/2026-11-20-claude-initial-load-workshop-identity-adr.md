# ADR (borrador) — Identidad de Talleres en la Carga inicial (#694 / #1049)

Estado: PROPUESTO. Decisión de producto pendiente del Sponsor. ChatGPT aceptó la opción 1 como diseño candidato (#1049 7038868173). No autoriza Apply, datos reales, migración ni merge.

## Contexto
`Talleres.csv` trae `workshopCode, number, name, orienteCode, city, country`. `Organization` solo tiene `Number`, `Name`, `OrienteCode` (sin código externo). `Organization.Number` no puede igualarse a `workshopCode`.

## Alternativas
1. **(Candidata)** `workshopCode` = clave local del lote CSV (enlaza Hermanos/Cargos con su Taller del mismo archivo, no se persiste). Identidad en BD = `(Number, OrienteCode)` con unicidad real verificada; colisión o ausencia de unicidad = bloqueo. Sin migración, no invalida el release firmado.
2. Campo nuevo `Organization.ExternalCode`: migración y cambio de esquema; solo después del corte de Talleres.
3. `workshopCode ≡ Number`: RECHAZADA (Number solo no es clave segura; se repite entre orientes).

## Brechas de la política actual (a cerrar en un cambio posterior, no en este PR)
- `InitialLoadWorkshopMatchPolicy` busca por `Code`; la opción 1 exige resolver por `(Number, OrienteCode)`.
- No se detecta hoy `(Number, OrienteCode)` duplicado en BD bajo distintos `Code`.
- `OrienteCode` vacío no marca el snapshot como ambiguo (solo hace contradictoria una fila emparejada).
- Lote vacío es válido por vacuidad (cubierto por `CanPresentPlan`).

## Pruebas
`InitialLoadWorkshopIdentityTests` caracteriza el comportamiento actual (7 casos). No fijan las brechas anteriores como correctas.

## Restricciones
PREVIEW_ONLY, `supportsApply=false`, sin datos reales, sin tocar main/glmdch.
