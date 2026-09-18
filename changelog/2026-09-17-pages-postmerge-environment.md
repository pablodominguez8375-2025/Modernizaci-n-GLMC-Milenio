# 2026-09-17 — Corrección post-merge GitHub Pages

## Causa raíz confirmada

La API de GitHub Pages reporta `build_type=workflow`. El environment `github-pages` tiene branch policy personalizada y permite únicamente `dev` y `main`.

Los runs post-merge disparados por `pull_request.closed` construyen correctamente el `merge_commit_sha`, pero GitHub registra el deployment con la rama feature de origen. El job de deploy era rechazado por la política del environment antes de tomar runner.

## Corrección

- los deploys normales por `push` mantienen el environment protegido `github-pages`;
- el fallback post-merge usa un environment dedicado `github-pages-merged`;
- el fallback solo corre cuando el PR fue realmente fusionado hacia `dev`;
- el código desplegado sigue siendo el `merge_commit_sha`, no el head de la feature;
- no se modifica `main`;
- no se relaja la política existente de `github-pages`.

## Seguridad

El environment alternativo existe únicamente para el mecanismo de entrega posterior a un merge ya aprobado. No se usa para PR abiertos ni para ejecutar código no fusionado.
