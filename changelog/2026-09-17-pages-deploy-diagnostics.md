# 2026-09-17 — Diagnóstico GitHub Pages post-merge

## Objetivo

Determinar con evidencia de la API de GitHub por qué el build de Showcase finaliza correctamente pero el job final de Pages falla antes de ejecutar pasos.

## Diagnóstico incorporado

En ejecuciones post-merge del workflow `PMGM Showcase Demo` se consultan, sin exponer secretos:

- configuración actual de GitHub Pages;
- `build_type` y source;
- estado del environment `github-pages`;
- reglas de protección;
- deployment branch policies;
- últimos deployments del environment.

La información se imprime como metadatos técnicos en el log del build y no modifica la aplicación ni el artifact público.

## Hipótesis a validar

La URL pública existe y los builds son correctos. El fallo podría deberse a configuración/branch policy/permisos del environment de deployment. Este cambio evita seguir modificando frontend o infraestructura sin evidencia.

## Seguridad

- usa únicamente `GITHUB_TOKEN` efímero del workflow;
- no imprime el token;
- no ejecuta código adicional del PR con privilegios;
- no modifica `main`;
- no cambia datos ni servicios del producto.
