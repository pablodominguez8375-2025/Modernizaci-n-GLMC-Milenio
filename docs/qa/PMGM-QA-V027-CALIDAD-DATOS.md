# QA v0.27 — Calidad de datos de Régimen Interior

## Objetivo de demostración

Mostrar que Proyecto Milenio no sólo almacena historia institucional: también ayuda a Régimen Interior a detectar datos que deben ser corroborados antes de una decisión administrativa.

## Recorrido sugerido

1. Abrir **Calidad de datos** desde Gestión institucional.
2. Mostrar el resumen de errores, advertencias y miembros afectados.
3. Filtrar por `Error` para priorizar inconsistencias críticas.
4. Seleccionar una regla, por ejemplo **Exaltación anterior al aumento**.
5. Explicar la tarjeta de observación: miembro, Taller, fechas relacionadas, explicación y acción sugerida.
6. Mostrar que la pantalla declara **Sólo revisión** y no tiene botones de corrección automática.
7. Cambiar el Taller relacionado para demostrar alcance por Orden/Taller.
8. Buscar por nombre, Nº institucional o Taller.

## Mensaje clave

La plataforma no modifica historia masónica por inferencia. Detecta posibles inconsistencias y deriva la revisión a un responsable autorizado, preservando trazabilidad y evidencia.

## Casos ficticios incluidos en showcase

- exaltación anterior al aumento de salario;
- afiliación vigente después de defunción;
- fecha destino distinta de fecha efectiva de traslado;
- reintegro sin retiro previo;
- hito de grado duplicado;
- más de una afiliación vigente.

## Controles de privacidad

- acceso restringido a Régimen Interior;
- datos mínimos para explicar el hallazgo;
- sin correo, teléfono, dirección, notas o contenido documental;
- `Cache-Control: private, no-store` en QA integrado;
- cliente Bearer-only y same-origin.
