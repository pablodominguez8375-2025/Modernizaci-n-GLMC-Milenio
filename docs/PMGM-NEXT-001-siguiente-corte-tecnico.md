# PMGM-NEXT-001 — Siguiente corte técnico

## Hito bloqueante vigente: QA srv01

Antes de abrir un nuevo incremento funcional, el Proyecto Centenario debe cerrar el corte operativo vigente en `srv01`.

Orden obligatorio:

1. publicar Demo GitHub Pages del mismo SHA;
2. publicar instalable QA del mismo SHA;
3. desplegar el instalable exacto en `srv01`;
4. validar SHA-256 y `MANIFEST.sha256`;
5. ejecutar smoke autenticado;
6. probar Consejo de Administración con perfil autorizado;
7. ejecutar regresión QA-001..QA-021;
8. corregir defectos P0/P1 detectados;
9. congelar un nuevo candidato UAT desde el código realmente probado;
10. ejecutar UAT institucional;
11. promover a `main` sólo con aprobación expresa.

La automatización vigente está documentada en:

`docs/installation/QA-SRV01-AUTODEPLOY.md`

y se traza en Issue #97.

## Autorización de avance funcional en paralelo — 18-09-2026

El Sponsor / Product Owner instruyó expresamente continuar desarrollo, pruebas, documentación y siguientes funciones desde el estado vivo de `dev`.

Esta autorización permite trabajar el siguiente incremento funcional en una rama `feature/*` mientras Issue #97 permanece abierto. **No elimina el gate operacional de QA**: no se puede declarar el corte operacional, ejecutar UAT como evidencia del código nuevo ni promover a `main` hasta desplegar en `srv01` el SHA correspondiente y completar smoke/regresión.

El incremento activo es `feature/insinuaciones-flujo-reglamentario-v2`, que extiende el flujo real de insinuaciones ya existente y corrige brechas de alineamiento con el protocolo 2026.

## Siguiente incremento funcional después de QA

Una vez cerrado el hito anterior, continuar con el expediente de insinuación y sus transiciones reglamentarias. La fuente de verdad será dominio/backend antes que interfaz.

### Primera entrega demostrable

1. crear expediente;
2. registrar presentación en 1.er grado;
3. impedir revisión antes de 7 días;
4. registrar unanimidad/no unanimidad;
5. publicar;
6. calcular 20 días corridos;
7. registrar entrevistas y antecedentes;
8. habilitar o bloquear revisión de 3.er grado;
9. habilitar o bloquear balotaje;
10. producir historial auditable de estados.

### Segunda entrega

Conectar `ballot_approved` con creación de solicitud de iniciación y con PMGM-ARCH-003.

### Pruebas mínimas

- transición válida;
- salto de etapa rechazado;
- plazo insuficiente rechazado;
- corrección dentro del mismo expediente;
- reingreso tras rechazo con vínculo histórico.

No usar la RC1 histórica como evidencia de aceptación del código actual.

## Ajuste funcional prioritario aprobado — cierre documental de Tenidas

Antes de considerar cerrado funcionalmente el circuito de Secretaría/Tenidas, implementar la regla aprobada por el Sponsor / Product Owner:

- separar **Realizada** de **Cerrada**;
- Tenida regular/no ceremonial: exigir **Extracto de Acta PDF** para cerrar;
- Tenida ceremonial de Iniciación, Aumento de Salario o Exaltación: exigir **Extracto de Acta PDF + Plancha de Autorización de Ceremonia emitida por Gran Secretaría**;
- vincular la Plancha de Autorización al expediente de la Tenida correspondiente;
- bloquear el cierre en backend cuando falte un requisito;
- mostrar en Secretaría del Taller el estado de cumplimiento documental;
- mantener Plancha de trabajo del hermano y Acta completa como documentos opcionales que no condicionan el cierre;
- conservar compatibilidad/migración de registros históricos;
- actualizar pruebas, Demo GitHub Pages, instalable QA y kit de regresión.

Criterios mínimos de aceptación:

1. una Tenida regular sin Extracto no puede cerrarse;
2. una Tenida regular con Extracto puede cerrarse aunque no tenga Plancha de trabajo ni Acta completa;
3. una Tenida ceremonial con sólo Extracto no puede cerrarse;
4. una Tenida ceremonial con sólo Plancha de Autorización no puede cerrarse;
5. una Tenida ceremonial sólo puede cerrarse con ambos documentos válidos y vinculados;
6. la validación se aplica server-side y queda auditada;
7. la demo y el instalable QA reproducen el mismo comportamiento.

