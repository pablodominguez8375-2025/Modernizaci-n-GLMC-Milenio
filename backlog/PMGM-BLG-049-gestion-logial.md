# PMGM-BLG-049 — Gestión Logial del Taller

**Estado:** Núcleo implementado; expansión MVP QA en desarrollo  
**Prioridad:** P0 para QA  
**Versión base API:** 0.13.0  
**Actualización funcional:** 2026-09-08

## Objetivo
Entregar a cada Taller una Gestión Logial integrada sobre la base maestra institucional, con ficha del hermano, Secretaría, Tenidas, asistencia, instrucción, planchas de trabajo, Tesorería del Taller y Hospitalaria, evitando duplicar personas/historial y manteniendo trazabilidad.

## 1. Núcleo ya implementado
- Creación y listado de Tenidas por Taller.
- Tipos de Tenida: regular, solemne, instrucción, aniversario, fúnebre y especial.
- Grados: aprendiz, compañero, maestro o todos los grados.
- Cierre de Tenida con bloqueo posterior de nuevas asistencias.
- Selector minimizado de miembros activos del Taller.
- Registro de asistencia: presente, justificado y ausente.
- Rectificación append-only de asistencia.
- Consulta de asistencia vigente tomando el último evento por miembro.
- Actas por versiones inmutables.
- Aprobación/supersedencia de versiones de acta.
- Auditoría persistente.
- Interfaz React responsive y cliente OIDC Bearer.

## 2. Ficha del hermano — MVP QA
La ficha personal debe mostrar, según permisos:
- identidad/datos institucionales;
- Taller actual e historia de Talleres;
- grado vigente e historia de grado;
- cargos y períodos;
- fechas institucionales relevantes;
- asistencia a Tenidas;
- historial de instrucciones;
- planchas de trabajo propias;
- estado e historial de cuotas;
- comprobantes de pago;
- anotaciones institucionales autorizadas.

El hermano podrá editar sólo datos personales autorizados, por ejemplo domicilio, teléfono, correo y fotografía. No podrá editar grado, iniciación, aumento de salario, exaltación, retiros, reintegros, defunción ni otros eventos institucionales protegidos.

## 3. Taller — identidad propia
Cada Taller puede mantener:
- descripción / acerca de;
- historia/origen;
- fecha de creación;
- datos relevantes;
- datos de contacto autorizados;
- logo propio.

El Venerable Maestro es la máxima autoridad del Taller. La administración del perfil del Taller queda trazada y los datos institucionales críticos siguen sujetos al gobierno de Gran Logia.

## 4. Secretaría del Taller
La Secretaría del Taller administra:
- Tenidas;
- asistencia por grado;
- actas;
- registro/propuesta de fechas institucionales conforme al flujo autorizado;
- anotaciones institucionales autorizadas;
- solicitudes de iniciación, aumento de salario y exaltación;
- solicitudes/reservas de salas y templos.

Gran Secretaría aprueba/autoriza las solicitudes institucionales cuando corresponda.

## 5. Instrucción / Docencia — decisión vigente
La instrucción se modela inicialmente como una **clase/instrucción realizada**, no como un porcentaje de avance por temas.

Responsables:
- Segundo Vigilante → primer grado / Aprendices.
- Primer Vigilante → segundo grado / Compañeros.
- Inmediato Ex Venerable Maestro → Maestros / tercer grado y superiores.

Cada instrucción registra:
- fecha;
- Taller;
- grado;
- tema tratado;
- responsable/instructor;
- asistencia o inasistencia de cada hermano;
- observaciones opcionales.

En la ficha del hermano, el MVP muestra:
- fecha de la instrucción;
- tema tratado;
- asistió / no asistió.

Evaluaciones, puntajes y avance curricular por tema quedan para una fase posterior.

## 6. Planchas de trabajo
- Trabajo elaborado por un hermano.
- Asociado a autor, Taller, grado, título/tema y fecha.
- Puede vincularse a una Tenida.
- Permanece en Gestión Logial y en el historial del hermano.
- No va al Gran Archivo por defecto.
- Puede proyectarse a Biblioteca Virtual cuando sea autorizada.
- La Biblioteca aplica el grado mínimo y demás permisos.

## 7. Tesorería del Taller — cuotas
Cada Taller tiene un Tesorero del Taller.

La cuota mensual mantiene dos montos independientes y versionados por vigencia:
1. monto cobrado al hermano;
2. monto que el Taller paga a Gran Tesorería por ese hermano.

Los montos pueden cambiar entre períodos sin modificar el histórico.

Se permiten:
- pago total;
- abonos/pagos parciales;
- imputación a meses específicos;
- saldo pendiente;
- reversas/ajustes trazables.

Cada pago genera comprobante y debe poder enviarse por correo al hermano. El hermano consulta estado de cuenta y descarga comprobantes desde su ficha.

## 8. Tesorería del Taller — contabilidad y reportes
Ingresos:
- cuotas;
- otros ingresos extraordinarios autorizados.

Egresos:
- insumos;
- útiles;
- derechos;
- gastos operativos;
- celebraciones/actividades autorizadas;
- otros egresos debidamente registrados.

Reportes mínimos:
- ingresos por mes;
- egresos por mes;
- saldo;
- resumen mensual por tipo;
- detalle/libro de movimientos;
- drill-down desde resumen a detalle;
- proyectado vs realizado;
- proyección financiera;
- PDF/Excel.

## 9. Rendición del Taller a Gran Tesorería
Generar nómina mensual con, según permisos y minimización:
- hermano;
- RUT cuando sea necesario;
- Taller;
- cargo;
- grado;
- período;
- monto esperado para Gran Tesorería;
- monto efectivamente transferido;
- diferencia.

Conciliación y alertas tempranas cuando existe pago menor o mayor al esperado.

## 10. Hospitalaria del Taller
Administrar el Bolso de Hospitalaria/ofrendas y movimientos destinados a:
- ayudas a hermanos;
- préstamos/apoyos;
- donaciones;
- incendios/terremotos/contingencias;
- actividades benéficas.

Todos los movimientos deben quedar contabilizados.

## 11. Reposiciones
- Monto de incorporación configurable cuando corresponda.
- Al fallecer un hermano con derecho a reposición, los hermanos activos sujetos a la regla generan una cuota de reposición.
- Monto configurable por vigencia; referencia actual mencionada: $1.500, nunca fijo en código.
- Hospitalaria del Taller controla obligación, pago, deuda y total recaudado.
- El Taller transfiere la recaudación a Gran Hospitalaria.
- Gran Hospitalaria procesa el pago correspondiente a deudos/herederos.
- Conciliación y auditoría de todo el circuito.

## 12. Seguridad y privacidad
- Operación limitada al Taller y perfiles autorizados.
- El hermano accede a su propia información conforme a finalidad y política.
- RUT no es identificador técnico.
- Datos de asistencia, grado, cuotas y ayudas no se exponen a roles sin necesidad funcional.
- `Cache-Control: private, no-store` para vistas personales/financieras/sensibles.
- Auditoría sin copiar contenido sensible innecesario.
- Ley 21.719 transversal.

## 13. Orden de implementación QA
1. [x] Tenidas, asistencia y actas.
2. [ ] Ficha del hermano / vista personal.
3. [ ] Instrucciones + asistencia + historial personal.
4. [ ] Perfil del Taller.
5. [ ] Planchas de trabajo + vínculo opcional a Tenida.
6. [ ] Tesorería: configuración de cuotas por vigencia.
7. [ ] Tesorería: pagos/abonos/estado de cuenta/comprobantes.
8. [ ] Tesorería: ingresos/egresos/reportes/proyección.
9. [ ] Rendición y conciliación con Gran Tesorería.
10. [ ] Hospitalaria: Bolso/ayudas.
11. [ ] Hospitalaria: reposiciones y conciliación con Gran Hospitalaria.
12. [ ] UI QA integrada y datos sintéticos.

## Criterio de aceptación del siguiente corte
Un Taller de prueba puede registrar una instrucción de un grado, marcar asistencia, y el hermano puede consultar desde su vista personal la fecha, el tema y si asistió, sin poder alterar esa información institucional.