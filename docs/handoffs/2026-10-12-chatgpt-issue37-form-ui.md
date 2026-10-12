# Entrega multi-IA — Issue #37, interfaz de formularios, 12-10-2026

Repositorio: pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio.
PR funcional: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/459
Base de desarrollo: b039363d8acc6c42d04a1f01460d5171dd24689f. Verificar HEAD vivo antes de continuar.

## Cambios y reservas

- Sistema → Formularios: FormSchemaAdminPanel y CSS acotado permiten consultar el catálogo institucional, recuperar versiones previas, agregar/modificar solo campos configurables y publicar otra versión con fecha de vigencia y expectedPreviousVersion. La API restringe permisos a CanConfigureSystem; no hay permisos nuevos.
- Secretaría Taller: CandidateFormCapturePanel integrado a CandidateWorkshopIntakePage después de guardar la Ficha de Insinuado canónica. Sus nuevos valores complementarios se envían por métodos OIDC de CandidateIntakeApiClient usando el esquema confiable del servidor. Identidad, Taller, RUT, antecedentes oficiales y el flujo de aprobaciones NO cambian.
- Gran Secretaría: CandidateProfilePage muestra historial de capturas complementarias solo lectura. El GET de captura histórica autorizado devuelve también los campos y etiquetas exactas de esa versión sin exponer campos núcleo.
- Demo sintética usa únicamente datos ficticios y memoria temporal del navegador: candidateFormTypes.ts, candidateFormMock.ts, métodos mock en candidateIntakeApi.ts y pmgmApi.ts. No se almacena PII ni se publica información privada en GitHub Pages.
- Soporte de tipos texto, multilinea, fecha/hora, numérico, booleanos, listas y dependencias simples; adjuntos/fotos/referencias se prohíben hasta enlazarlos desde Gestor Documental con autorización y antivirus.
- CSS de ambos paneles aislado de estilos globales y menú principal: mobile 360, tablet 768, desktop 1440 mediante grid fluida. No hay evidencia de screenshots de navegador real en este corte; la comprobación visual institucional sigue pendiente.
- Pruebas Vitest candidateFormTypes.test.ts y candidateFormApi.test.ts, además de ampliación de CandidateFormCaptureHttpTests.cs para etiquetas históricas. Verificar sus resultados exactos en CI antes de fusionar.

## Riesgos y trabajo pendiente

- Issue #37 permanece abierta: extender a más formularios y perfiles/grados, validaciones por función, reportes y exportaciones Word/PDF, UAT institucional.
- Ley 21.719: aún debe aprobarse política de retención, rectificación y supresión o anonimización auditada de las capturas privadas, conciliada con el trigger de historial inmutable. Inmutabilidad no equivale a cumplimiento legal.
- main no se promueve; srv01 y UAT física pausadas por Issue #97.
- No afirmar Pages/qa-current.json actualizado sin comprobar la fuente SHA del artefacto.

## Procedimiento de reanudación

Consultar dev/main vivos, PR #459, Issue #37, START-HERE.md, AGENTS.md y GOV-003 §16, y diferencias en Línea Base Maestra de Drive. Asegurar que CI, Showcase y QA Installable del HEAD del PR son SUCCESS, resolver cualquier fallo, fusionar por squash solo en dev, verificar readback y cerrar con adenda en Drive y comentario de continuidad en GitHub. El PR y este archivo son el handoff de la entrega, no reemplazan estado vivo.
