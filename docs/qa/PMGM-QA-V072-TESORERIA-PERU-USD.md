# PMGM-QA-V072 — Tesorería Perú en USD y libros CLP/USD

## Alcance aprobado
Validar el alcance de PMGM-ADR-005: registro Perú en USD, separación de libros y conformidad bancaria de Gran Tesorería. Este protocolo define pruebas automatizadas/técnicas. No constituye aprobación de UAT ni reemplaza revisión institucional.

## Casos de aceptación
1. La cuota ordinaria peruana vigente desde 01-01-2026 se obtiene como `6 USD`; no se convierte a CLP.
2. Cónyuge, estudiante y tercera edad del Perú no reciben tarifa inventada; un plan/cargo de esas categorías se bloquea sin fuente aplicable.
3. Plan de cuota del Taller peruano conserva el total en USD y presenta el aporte oficial y el componente local por separado.
4. Consultar CLP y USD muestra saldos independientes; reportes, cierres y conciliaciones filtran por moneda y nunca suman ambos libros.
5. El perfil del Hermano devuelve libros separados y no publica los campos históricos agregados si existen varias monedas. Cada movimiento/cargo conserva su moneda.
6. El Cuadro peruano se crea y presenta en USD; la generación de la cuota ordinaria usa USD 6 y no usa importes CLP como fallback para categorías peruanas sin tarifa.
7. Registrar un Cuadro o pago declarado por el Taller no cambia por sí solo la regularidad institucional. Sólo la conciliación por el acceso existente de Gran Tesorería guarda responsable/fecha de conformidad bancaria y deja al Taller al día cuando el Cuadro está cuadrado.
8. La migración asigna `CLP` por compatibilidad a datos previos sin modificar importes; configuraciones y cierres aceptan una instancia por organización/año/moneda.

## Límites operacionales
Checks verdes, empaquetado o publicación Pages sólo acreditan esas etapas. No instalar, desplegar manualmente, ejecutar smoke/regresión física ni UAT en `srv01` mientras siga pausado por el Sponsor; Issue #97 sigue pendiente. Antes de afirmar publicado, verificar que `qa-current.json.sourceSha` coincida con el SHA integrado vigente de `dev`.
