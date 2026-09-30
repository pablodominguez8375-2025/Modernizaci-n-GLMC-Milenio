# Encabezado móvil — UI QA v0.63

- «Proyecto Centenario» se muestra completo en móvil, en dos líneas cuando es necesario, sin ellipsis ni límite que recorte el nombre.
- Demo y SHA corto permanecen visibles a 360/390 px, con fuente de 12 px; el selector de perfil queda debajo para evitar solapamientos.
- Contratos de tema verifican que breakpoints posteriores no oculten SHA ni vuelvan a truncar el nombre.
- Mantiene logo, paleta, tipografía corporativa y lema oculto. Sin cambios de escala global, main, srv01 ni declaración de UAT.
- Seguimiento #223; delegación de Claude y reasignación de tres archivos desde #43/#116 autorizada por Pablo el 30-09-2026.
- Verificación: frontend231 tests, lint, tsc/build; mediciones Chromium360/390/480/620/720/768/1024/1440 sin overflow global ni de encabezado/solapamiento. Matriz completa y gates exact-head exigidos antes del merge.
