# PMGM #544 — Instrucciones de Docencia visibles solo para grado exacto

Fecha: 2026-10-22. Agente: ChatGPT (backend). Base real: `dev@2078769b21dd3aaaf1cea17742d7528d96cb5381`; `main@b77de59e170ebfa2abec71b418f6d8b0407ba7ad`.

## Regla PO

Un hermano sin cargo vigente ve las instrucciones `lodge_instruction_day` **solamente** si el evento pertenece a su Taller activo y su grado coincide exactamente con su grado efectivo. Maestro no ve instrucciones de Aprendiz/Compañero; Compañero no ve Aprendiz/Maestro. Los cargos vigentes de ese Taller y administradores institucionales conservan excepciones autorizadas.

## Hallazgo

`CalendarGradeReader.CanSee` valida primero `Grade == null || Grade == "all"` devolviendo `true` a miembro de Taller, antes de distinguir Docencia. Por eso una instrucción sin grado concreto (legado o mal catalogada) puede llegar a hermanos de cualquier grado. Este caso viola la política de instrucciones exactas y la intención de privacidad de `#544`.

## Cambios a ejecutar

- Colocar la rama de instrucción antes del retorno general; exigir Taller válido y membresía activa; para regular, grado del evento conocido y grado efectivo exacto. Para cargo vigente de ese Taller se permite instrucción, sin extrapolar a Taller ajeno; administración institucional conserva excepción.
- Conservar `Grade null/all` como general para tenidas/calendario de otros tipos, sin regresión de jerarquía Maestro ve grados inferiores de tenidas.
- Pruebas unitarias para tres grados, grado null/all/desconocido, cargo vigente/vencido, otro Taller y admin, más equivalencia entre permisos de Agenda y avisos que reutilizan `CalendarGradeReader`.
- Sin migración ni ampliación de claims/permisos, exclusivamente regla de visibilidad.
- Handoff a Clau/Claude y otras IA: UI de Agenda no se modifica; si la demo contiene instrucción de grado `all` deberá clasificarse de modo específico o quedar oculta a miembros regulares. No añadir tratamiento alternativo en frontend.

**Puerta:** PR draft `fix/calendar-instruction-exact-degree-544-20261022-gpt`, CI+Showcase+Instalable 3/3 SUCCESS SHA exacto y compare behind=0; merge únicamente dev. No tocar main/glmdch. Registrar en GitHub issue #544 y Drive al terminar.

## Implementación (22-10-2026)

- `CalendarGradeReader.CanSee` ahora evalúa `lodge_instruction_day` ANTES del retorno general `Grade null/all`, obliga `OrganizationId` real y membresía activa del Taller y niega grado desconocido a hermanos ordinarios. Administrador institucional y cargo vigente del mismo Taller conservan sus permisos actuales; oficio de otro Taller nunca autoriza la lectura.
- `CalendarGradeVisibilityTests`: nueve combinaciones de grado exacto existentes, más `null/all/invalid` contra los grados 1/2/3, vigencia de cargo, membresía, Taller ajeno y tenidas generales.
- `CalendarNotificationRecipientGuardTests`: no encolar instrucción `null/all` a miembros regulares aunque estén activos; confirma excepción para oficio del mismo Taller y bloqueo para Taller ajeno. Se usa la misma política de lector en lista Agenda, exportación ICS y guard de avisos, sin crear rutas paralelas.
- No se creó ni modificó ninguna tabla, columna, dato histórico, variable de configuración o RUT. No hay migraciones; el cambio es estrictamente una evaluación dinámica de visibilidad.
- Handoff UI para Clau: las entradas `lodge_instruction_day` con grado ausente o `all` serán invisibles al miembro regular hasta reclasificarse con el grado correcto; **no** forzar la visibilidad desde frontend, demo o notificaciones. Otros eventos generales conservan su comportamiento. Capturas de UI no proceden, al no modificarse vista ni CSS.
