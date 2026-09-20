# PMGM QA v0.57 — Retiros gestionados por Secretaría del Taller

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
