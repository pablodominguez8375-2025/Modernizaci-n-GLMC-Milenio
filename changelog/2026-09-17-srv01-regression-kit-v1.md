# 2026-09-17 — Kit de regresión QA srv01 v1

## Objetivo

Agregar una capa de regresión reproducible al instalable QA de `srv01`, ligada al SHA exacto instalado y separada de la UAT formal histórica.

## Alcance

- nueva matriz `PMGM-QA-SRV01-REGRESSION.template.json`;
- 21 controles QA:
  - QA-001 a QA-020 mantienen la cobertura funcional histórica;
  - QA-021 agrega Consejo de Administración;
- gate `tests/qa_srv01_regression_gate.py`;
- preparador `scripts/prepare-srv01-regression.sh` que toma SHA desde `BUILD-INFO.txt`;
- registrador seguro de resultados/evidencias;
- documentación operacional;
- workflow del instalable valida e incluye todo el kit;
- `INSTALAR.sh` encadena explícitamente al paso de regresión.

## Principio de gobierno

Este kit **no reemplaza la UAT institucional formal** ni modifica retroactivamente el plan RC1 de 20 casos. Su propósito es detectar defectos en el corte actual de `dev` antes de solicitar aceptación institucional.

## Datos y seguridad

- sólo datos ficticios en QA;
- referencias de evidencia minimizadas;
- prohibidos secretos, tokens y datos personales reales en Git;
- un `pass` requiere al menos una referencia de evidencia;
- un `fail` requiere notas explicativas.

## Salida esperada

Después de instalar un artifact QA:
1. preparar ejecución desde `BUILD-INFO.txt`;
2. registrar los 21 controles;
3. validar 21/21;
4. sólo después considerar el corte técnicamente listo para UAT institucional.
