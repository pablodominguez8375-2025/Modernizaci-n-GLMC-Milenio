# Etiquetas de tipo de ceremonia — PMGM-GAP-001 · GAP-001

Fecha: 2026-09-22
Rama: `fix/gap-001-etiquetas-ceremonia` (apilada sobre PR #126)

## Defecto corregido

Existían cuatro copias locales de `ceremonyTypeLabel()` en el frontend. Tres de ellas
(Ceremonias, Gran Secretaría, Gestión Logial) rotulaban como **"Exaltación"** cualquier
tipo distinto de Iniciación o Aumento de salario. El backend ya define
`affiliation` e `incorporation` en `CeremonyCodes.Type`, y PR #116 los incorpora al
contrato del frontend: sin esta corrección, toda Afiliación o Incorporación se habría
mostrado como Exaltación en esas tres pantallas.

## Cambio

- Nuevo módulo único `frontend/src/ceremonyTypes.ts` con los cinco tipos del backend:
  Iniciación, Afiliación, Aumento de salario, Exaltación, Incorporación.
- Un código desconocido se muestra como `Tipo no reconocido (código)`; nunca se
  disfraza de un grado real.
- Ceremonias, Gran Secretaría y Gestión Logial usan el módulo compartido.
- `CeremonyType` se amplía a los cinco tipos, con el mismo valor exacto que PR #116
  (la línea se fusiona sin conflicto).
- Demo (datos ficticios): una solicitud de **Afiliación** autorizada, pendiente de
  Plancha, en la bandeja de Gran Secretaría (Taller Demostrativo Nº 23).

## Fuera de alcance, explícitamente

- **"Otra"**: figura en el formulario 2026 pero no existe como código en el backend.
  No se agrega sin decisión normativa y contrato backend.
- **`LodgeSecretariatPanel.tsx`** conserva su copia local: PR #116 inserta código
  inmediatamente debajo y eliminarla generaría un conflicto evitable. Esa copia no
  tiene el defecto (muestra el código crudo). Se retira al integrar PR #116; el test
  `ceremonyTypes.test.ts` la lista como excepción explícita.
- Fila en `PROJECT-CONTINUITY-MASTER.md`: pendiente al integrar, porque PR #116 edita
  ese archivo.

## Pruebas

- `ceremonyTypes.test.ts`: etiquetas correctas, no-regresión del defecto, códigos
  desconocidos, fuente única, y **paridad exacta con `CeremonyCodes.cs`** (se omite en
  builds que sólo tienen `frontend/`, como `Dockerfile.showcase`; corre en CI).
- `CeremoniesPage.test.tsx`: el test que documentaba el gap se reemplaza por uno que
  verifica el uso del módulo compartido.
- Suite: 167/167 · lint 0 · build OK. Sin conflictos nuevos con PR #116 (simulado).

## Estado de salidas

- Código: rama/PR, no integrado en `dev`.
- Demo GitHub Pages: se publica desde `dev` tras el merge (verificar SHA visible).
- Instalable QA: generado por el workflow del PR; **despliegue QA en srv01 pendiente** (Issue #97).
