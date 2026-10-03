# Handoff — ChatGPT — corrección final de admisiones #275 / #276

Base viva revisada: dev `6083248ff8c8abade4e9fb79676439c1eca8ebf2` (Claude UI QA v0.84, PR #303). Incorporada en la rama existente sin reabrir trabajos cerrados. Main `6dfb9546a4873baff15955cf86abfd7d47e3d111` continúa congelada.

## Corrección del estado previo

El recibo de ec08044 afirmaba cierre técnico y pedía nueva autorización. La autorización permanente para squash de alcances aprobados a dev existe; no se requiere repetirla. Sin embargo, la revisión detectó defectos reales: dos guardados independientes, replay por fecha, incorporación externa incompleta, traslado incompatible con CRV cerrada, controles normativos sin consumo y pantalla que fabricaba actor/decisión. No fusionar este corte mientras sus pruebas/gates no estén comprobados.

## Implementado en este incremento

Materialización atómica entre core/admissions con transacción compartida SERIALIZABLE; recibo por expediente; replay de misma carga y conflicto para carga diferente; incorporación crea Member sobre Person existente con grado acreditado; exige ceremonia autorizada, Plancha emitida y Tenida cerrada con acta/extracto. Traslado conserva origen retirado y enlaza destino ya materializado, sin alta duplicada.

Proyector único conecta revisión art. 2.3/indulto, lectura previa de primer grado, tercer grado/balotaje posterior y comisión vigente para activación/incorporación. Secretaría incorpora panel de actuaciones/requisitos con capacidades de API; no registra votos individuales. Tenidas permiten afiliación/incorporación. Pantalla relee expediente luego de materializar.

Registro GOV-004: `docs/modelo-datos/cambios/2026-10-03-issue-275-materializacion-atomica.md`.

## Verificación y límites

Compilación local .NET sin errores/advertencias y frontend pruebas/lint/build verificadas antes de publicación. Las pruebas PostgreSQL locales no pueden certificarse: no existe servicio de base disponible; la suite retorna anticipadamente cuando PMGM_TEST_POSTGRES no está configurada. CI sí configura PostgreSQL y debe ejecutar las pruebas HTTP nuevas. El recibo externo de cierre identifica SHA/runs/artefactos nuevos; este documento no certifica gates futuros ni un QA anterior.

Pendientes antes del cierre: Actions del nuevo SHA, revisión visual/funcional, ZIP/checksum/MANIFEST/SOURCE_SHA/BUILD_RUN_ID exactos, verificación GitHub/Drive y revisión final de solapamientos. Si dev cambia, actualizar y repetir gates. UAT institucional/srv01 permanecen pendientes por decisión Sponsor y no son nueva solicitud de autorización para dev. #116 histórico permanece NO FUSIONAR TODAVÍA.

START-HERE sólo se actualizará con una línea en PR documental posterior al merge funcional. No alterarlo en esta rama funcional.

## Sincronización y verificación adicional

Se incorporó dev `c6810dabc976281e919abfc00265cc159f79309b` (Claude #305, Hospitalaria v0.85), conservando sus pestañas. Admisiones reutiliza ActionDrawer: alta, evidencia, actuación, ceremonia y materialización permanecen cerradas hasta pulsar su botón. El perfil sintético sólo determina las acciones de demostración; en modo institucional se conservan intactas las capacidades devueltas por la API. Secretaría del Taller no muestra decisiones de Gran Maestría ni revisión art. 2.3. El expediente sintético usa el UUID del Taller 23, permitiendo consultar su comisión de Maestros.

Frontend: 389 pruebas, lint y build locales aprobados en esta sincronización. El script de Showcase añade una comprobación específica de tramitación de afiliación en ocho tamaños: requisitos cargados, formularios cerrados, ceremonia bloqueada y ausencia de facultades ajenas al cargo. Su resultado se comprobará en Actions del nuevo SHA; no está certificado por la compilación local.

El corte anterior `17b0678d30192eb9f84d796d89f5f7aeccfa1f08` tiene CI #1881, Showcase #1250 y QA #888 SUCCESS. Es antecedente del corte anterior, no evidencia del incremento siguiente. La demo mantiene solicitudes de ceremonia y revisión documental operativa reservadas a la API instalada: no constituye UAT institucional ni autorización ceremonial.
