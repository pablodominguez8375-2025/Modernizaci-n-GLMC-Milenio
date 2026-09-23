# QA-035 — Tarifario de Tesorería según Decreto N.º 1.759

**Fecha:** 23-09-2026  
**Fuente:** Decreto N.º 1.759, emitido el 15-12-2025, vigente desde el 01-01-2026.  
**Estado:** especificación de aceptación; ejecutar en QA srv01 después de instalar el SHA integrado.

## Criterios

1. Gran Tesorería clasifica cada Taller por Oriente. Esa clasificación es institucional y no la modifica el Tesorero del Taller.
   Los datos de Oriente se consultan en rutas autorizadas de Tesorería; el DTO general de opciones/perfil de organización mantiene su minimización y no expone ese dato transversalmente.
2. El tarifario oficial aplicable en CLP es:

| Tipo de miembro | Santiago | Otros Orientes |
|---|---:|---:|
| Ordinario | $21.000 | $15.000 |
| Cónyuge | $13.000 | $10.000 |
| Tercera edad | $10.000 | $8.000 |
| Estudiante | $8.000 | $8.000 |

3. Para Perú, el decreto fija USD 6 para cuota ordinaria. El flujo de cargos actual es CLP; hasta implementar cobros multimoneda, debe mostrar tarifa no disponible y bloquear cargos; no convertir USD a CLP ni inventar equivalencias para cónyuges/tercera edad.
4. El Tesorero configura cuota total local por tipo y vigencia. El sistema calcula el aporte oficial a Gran Tesorería, lo presenta de solo lectura y rechaza cuotas locales inferiores al aporte oficial.
5. La diferencia entre cuota local y aporte oficial es el ingreso del Taller. Ejemplos: cónyuge Santiago a $15.000 = $13.000 Gran Tesorería + $2.000 Taller; otro Oriente a $15.000 = $10.000 + $5.000.
6. La composición mensual para Gran Tesorería excluye Past Activos. Si el Taller define una cuota local para Past Activos, esa obligación queda local y su aporte institucional es cero.
7. El catálogo muestra los derechos únicos del decreto: Iniciación $41.000; Aumento de Salario $31.000; Exaltación $41.000; Afiliación $26.000; Incorporación $31.000. El enlace de pago/conciliación de estos derechos con cada solicitud ceremonial requiere un incremento aparte; este corte no debe declarar conciliación ceremonial implementada.
8. Las cuotas de cesantía del decreto (primeros tres meses 100% con $0; segundo trimestre 75% $5.250/$3.750; tercero 50% $10.500/$7.500; cuarto 25% $15.750/$11.250 para Santiago/otros Orientes) no se aplican automáticamente en este corte; documentar su modelado como trabajo pendiente, sin asignarlas como tarifas ordinarias.

## Validación técnica

- API: clasificación institucional, importes oficiales por categoría/oriente/vigencia y rechazo de valor oficial alterado o cuota local bajo el mínimo.
- SQL/PostgreSQL: la organización persiste su Oriente con migración reversible.
- UI y Demo Pages: cuota Gran Tesorería no editable; total local editable; diferencia visible; tarifa de Perú sin conversión ficticia.
- Cargos: una vez por miembro/período; sin cargos de Gran Tesorería para Past Activos.
- Gates: pruebas backend y PostgreSQL, frontend, build, migration gate, CI exact-head, Showcase/Pages y QA Installable desde el SHA correspondiente.

## Fuera de alcance / seguimiento

La gestión multimoneda para Perú, la aplicación automática de cuotas por cesantía y la conciliación de derechos ceremoniales contra pagos requieren modelos y flujos propios antes de presentarse como terminados.
