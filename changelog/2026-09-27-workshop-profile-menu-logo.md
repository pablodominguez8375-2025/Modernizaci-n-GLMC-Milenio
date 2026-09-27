# Ficha del Taller: menú directo, nombre y logo — 27-09-2026

Issue #199, a partir de la observación del Product Owner. En rama `feature/workshop-profile-menu-logo-20260927` desde `dev@c6da21c92beac84a70fca0aadb6b26a984842242`.

- Entrada explícita **Taller → Ficha del Taller**, visible con el acceso de lectura o edición ya disponible.
- Edición de nombre oficial, fecha de iniciación, ciudad/Oriente y país bajo permisos existentes.
- Logo opcional PNG/JPEG, hasta 2 MiB, firma validada, ClamAV, storage privado y auditoría; carga/reemplazo/retiro.
- El cambio de nombre conserva ID/relaciones. No cambia `TreasuryTerritory` ni reglas tarifarias.
- Demo y criterios QA-V071 actualizados para validar comportamiento. QA física/UAT e instalación siguen pendientes; `srv01` pausado.

## Validación local

- Frontend: 215/215 pruebas, lint SUCCESS, build SUCCESS (warning previo del bundle de ~1 MB).
- Backend: no validado localmente; el entorno no tiene dotnet SDK. Queda sujeto a CI exact-head.
