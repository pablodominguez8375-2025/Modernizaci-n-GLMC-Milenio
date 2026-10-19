# Proyecto Centenario — #533 Etapa C3: trazabilidad de propuestas Gran Tesorería

Inicio 19-10-2026, `dev@11f9227248850a804815964b8b70c7345d11f3b1`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`.
Responsable ChatGPT, Issue #533; QA físico en otro hilo, no desplegar.

## Alcance
Persistencia **append-only** de actos de *propuesta* y *revisión independiente* de antecedentes históricos de Gran Tesorería, sin generar todavía un certificado final. No crear endpoint público, no habilitar sufragio, no cambiar `AssemblyGrandTreasuryLedgerPolicy` ni la restricción SQL de cierre de Asamblea.

## Criterios
- Una propuesta requiere autoridad explícita GT, `scope=order`, actor autenticado, preflight de C2 satisfactorio; requiere referencias y digest de evidencia.
- Una revisión exige **un segundo funcionario de Gran Tesorería**, identidad y alcance verificados, distinto de quien propuso. Revisión no es certificación, puede observar/rechazar, nunca habilitar padrón.
- Cada evento se agrega, jamás se reescribe; la base rechaza UPDATE/DELETE/TRUNCATE. Una revocación posterior también deberá ser otro acto documentado.
- Un mismo expediente no debe mezclarse entre asambleas o talleres. La futura ruta de escritura debe aplicar `DynamicGrandTreasuryAccess.AllowsAsync(..., "write")`, auditar `IAuditService` y verificar que la evidencia documental es auténtica. Política pura no sustituye controles del endpoint.
- Sin datos reales, UI, firma criptográfica final, ni despliegue QA. La Ley 21.719 obliga a controlar consultas/retención de referencias.

## No incluido
Certificado de balance utilizable en voto, correcciones bancarias o rectificaciones, acto de cierre de padrón, PDF/CSV, integración Tribunal/VM y permisos web. Mantener #533 abierto. Los gates CI/Showcase/Installable exact-HEAD decidirán integración exclusivamente a dev.
