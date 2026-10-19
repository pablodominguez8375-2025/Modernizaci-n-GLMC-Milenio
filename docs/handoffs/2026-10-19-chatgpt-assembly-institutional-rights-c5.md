# #533 — C5 derechos institucionales con evidencia y fecha de corte

Base dev@11035fafa877a5d042a302ba3217c45538f3191f. ChatGPT desarrolla únicamente políticas backend; QA físico en otro hilo.

Alcance: recomponer derecho institucional, asistencia y sufragio a partir de evidencias oficiales de membresía y de cobertura del Tribunal de Honor y Régimen Interior. Las ausencia de eventos restrictivos no equivale a libertad de sanciones salvo que la cobertura de la fuente esté autenticada y certificada para persona/fecha. A efectos de la política, cualquier fuente incompleta produce estado desconocido; por tanto el motor electoral ya integrado falla cerrado.

Sin endpoints, datos reales, acceso a expedientes reservados, voto nominal, cierre de Asamblea ni exportación. Falta conectar lectores canónicos autorizados, control RBAC en servidor, auditoría y certificación financiera GT. No fusionar sin CI exact-HEAD. main y QA no modificados.

## Entrega de código
- `AssemblyInstitutionalRightsEvidencePolicy`: exige identidad de persona/Taller/corte, membresía confirmada, cobertura certificada de Tribunal de Honor y Régimen Interior con evidencia y restricciones fechadas, incluidas revocaciones. Ausencia de cobertura o un registro inválido ⇒ seis salidas nulas (desconocidas), nunca permiso.
- `AssemblyEvidencedEligibilityPolicy`: une categoría institucional acreditada por V.M., derechos calculados y balance del Taller desde Gran Tesorería con la regla de sufragio ya integrada. Si falta cobertura TH, una categoría acreditada o saldo verificable no autoriza voto.
- Pruebas: `AssemblyInstitutionalRightsEvidencePolicyTests` y `AssemblyEvidencedEligibilityPolicyTests`. Ninguna lectura de documentos reservados ni RUT, no se almacena el sentido del voto.
- TODO para finalizar #533: adaptadores autenticados de membresía, mandatos V.M., cobertura Tribunal/RI y certificación histórica GT; endpoints RBAC y auditoría; cierre de corte y PDF/CSV. No usar cobertura declarada por navegador.
- QA y main fuera de alcance, CI exact HEAD obligatorio.
