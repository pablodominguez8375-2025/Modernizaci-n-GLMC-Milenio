# Tesorería — tarifas Decreto N.º 1.759

Fecha: 2026-09-23  
Estado: integrado en `dev` por PR #140; código SHA `7747eab76f339395efa3356e92017c19f5abb0f7`.

- Se incorpora clasificación institucional del Oriente del Taller para que Gran Tesorería calcule la tarifa decretada.
- Se separa aporte oficial de Gran Tesorería, cuota cobrada por el Taller y diferencia retenida por el Taller.
- El total local se configura por categoría/vigencia; se rechazan aportes manipulados y totales locales bajo la tarifa institucional.
- Se modelan las tarifas CLP de Santiago y otros Orientes, los USD 6 ordinarios de Perú sin conversión ficticia, y se listan los derechos ceremoniales.
- Past Activos se excluyen del aporte de Gran Tesorería.
- Se agrega QA-035. Pagos ceremoniales conciliados por expediente, conversión multimoneda Perú y automatización de cuotas de cesantía son alcance pendiente.
- Migración: `20260923140000_AddTreasuryTerritoryToOrganizations`.

Gates post-merge del SHA funcional: CI #1451, Showcase/Demo/Pages #695, QA Installable #333 y Pre-UAT #332 SUCCESS. QA física/UAT pendiente por Issue #97; `main` intacta.
