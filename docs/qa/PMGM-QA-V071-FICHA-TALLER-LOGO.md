# PMGM-QA-V071 — Ficha del Taller e identidad visual

**Issue:** #199 · **Estado:** criterios para validar en CI/Demo; QA física y UAT pendientes.

## Criterios

1. Secretaría del Taller, Gran Secretaría y Régimen Interior encuentran el acceso directo **Taller → Ficha del Taller** bajo sus permisos actuales.
2. Los perfiles con lectura consultan los datos y el logo; no se amplían los datos de Consejo ni permisos de edición.
3. Los perfiles de edición actualizan nombre, fecha de iniciación, ciudad/Oriente y país. Nombre obligatorio, máximo 200 caracteres; ciudad/país hasta 120; fecha no futura.
4. El ID del Taller y las relaciones existentes permanecen iguales al cambiar el nombre.
5. PNG/JPEG válido de hasta 2 MiB puede cargarse, reemplazarse y quitarse. SVG, MIME falsificado, firma no concordante, archivo vacío y tamaño excesivo se rechazan.
6. El archivo se analiza antimalware y se almacena privado; lectura autenticada y autorizada, `no-store`, `nosniff`; escrituras y cambios de metadatos auditados.
7. Cambios de ciudad/país no alteran `TreasuryTerritory`, cuotas ni tarifas. Demo usa datos ficticios.
8. Código, Demo y paquete QA se identifican con el mismo SHA de origen.

## Límites

No instalar ni desplegar en `srv01`, ejecutar smoke/regresión física o UAT mientras el Sponsor mantenga la pausa. El check CI, Pages o paquete QA no significa aceptación física. Issue #97 sigue pendiente.
