# PMGM QA v0.57 — Retiros gestionados por Secretaría del Taller

## Incremento Secretaría — Planchas de Trabajo

- La plancha se registra como expediente independiente y admite múltiples registros por hermano.
- Conserva autor, grado, título, tema, fecha de presentación, descripción corta y vínculo opcional a una Tenida no ceremonial.
- El archivo nace privado en Secretaría del Taller; no se remite a Gran Secretaría ni a Gran Archivero.
- `Solicitar Biblioteca` sólo cambia el expediente a `library_requested` y prepara metadatos de catálogo. No publica automáticamente.
- La publicación efectiva continúa requiriendo la autorización documental y queda visible sólo desde una versión disponible.
- Verificación automatizada: mock funcional privado -> solicitado, sin `publishedAtUtc`; frontend completo, lint y build.

## Incremento Secretaría — solicitudes de avance y programación ceremonial

- Aumento de Salario admite sólo hermano con grado vigente Aprendiz; Exaltación, sólo Compañero.
- Se bloquea una segunda solicitud activa del mismo tipo para el mismo hermano.
- La fecha de solicitud es tentativa y no crea Tenida ni reserva espacio.
- La creación de cualquier Tenida ceremonial exige Plancha de Autorización emitida, del mismo Taller, tipo y fecha.
- La acción de programar desde Secretaría crea la Tenida y vincula la Plancha al expediente documental.

### Elegibilidad calculada

- El sistema cuenta desde el último evento efectivo del grado de origen: antigüedad completa en meses, Tenidas no canceladas con asistencia presente, instrucciones realizadas con asistencia presente y Planchas de Trabajo del grado.
- Aumento de Salario: mínimo 24 meses, 30 Tenidas de 1.er grado, 10 instrucciones de 1.er grado y 2 Planchas de Trabajo.
- Exaltación: mínimo 24 meses, 10 Tenidas de 2.º grado, 10 instrucciones de 2.º grado y 2 Planchas de Trabajo.
- Los mínimos se almacenan como reglas institucionales versionadas; la interfaz muestra alcanzado/mínimo y el backend decide.
- La dispensa exige acta afirmativa de Cámara del Medio, requisito/reducción identificados y validación de Régimen Interior. Una reducción superior al 50% queda bloqueada incluso si las dos instancias aprobaron.
- Gran Secretaría sólo puede autorizar cuando también están conformes Régimen Interior, Gran Tesorería, Gran Hospitalaria y Gran Maestría según la matriz vigente.

## Alcance

El Secretario del Taller puede iniciar un retiro voluntario o forzoso para un hermano con pertenencia activa en su propio Taller. La solicitud conserva causal, referencia documental, fecha efectiva, autor y trazabilidad.

## Reglas

- retiro voluntario aprobado: cierra las pertenencias activas y deja al hermano en estado institucional de retiro voluntario o sueño;
- retiro forzoso aprobado: cierra las pertenencias activas e inhabilita institucionalmente al hermano en toda la Orden;
- la historia anterior no se elimina ni se reescribe;
- no pueden existir dos retiros pendientes para el mismo hermano;
- Régimen Interior o Gran Secretaría resuelven el trámite;
- una resolución aprobatoria no materializa el retiro hasta completar las firmas de Venerable Maestro, Tesorero/a, Orador/a y Secretario/a;
- cada firmante actúa con su propio perfil y dentro del Taller correspondiente;
- la cuarta firma materializa el retiro, cierra las pertenencias activas y registra el evento institucional;
- una solicitud rechazada conserva expediente y resolución sin cambiar el estado del hermano;
- el Secretario sólo puede iniciar trámites de hermanos activos en su Taller.

## Criterios de aceptación

1. el Secretario registra tipo, fecha, causal y referencia de carta;
2. un usuario de otro Taller recibe `403`;
3. un retiro duplicado pendiente recibe `409`;
4. la autoridad puede aprobar o rechazar una sola vez;
5. la aprobación crea un evento institucional y cierra pertenencias activas;
6. la ficha e informes conservan todo el historial anterior;
7. cada creación y decisión queda auditada.
8. una carta aprobada con una, dos o tres firmas conserva la pertenencia activa;
9. un cargo no puede firmar por otro ni repetir su propia firma;
10. la cuarta firma materializa el retiro una sola vez y conserva fecha y actor de cada firma.
