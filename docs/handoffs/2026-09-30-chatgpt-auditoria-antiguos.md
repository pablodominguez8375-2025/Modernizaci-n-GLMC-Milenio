# Auditoría de PR e issues históricos — 2026-09-30

Solicitud del PO: «Revisa los PR y los ISU antiguos». Issue #230; PR #231; agente ChatGPT.

## Resultado

Se revisaron los 8 PR y 24 issues abiertos anteriores al claim. Se cerraron los PR #43 y #67 como reemplazados, sin merge ni eliminación de ramas; y el issue #46 como completado. Se conservan 6 PR y 23 issues históricos. Este conteo excluye el issue/PR de esta auditoría. Las decisiones se comentaron individualmente en los 32 casos y los tres cierres se comprobaron por lectura de retorno.

Comparación contra `dev@972f03d251f17bdcac1546cec27ef6cee02c770b`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`. Se revisaron cuerpos/comentarios, diferencias completas, archivos y símbolos del código vigente, migraciones y pruebas. No se dedujo cierre por antigüedad, nombre coincidente de archivo, aceptación parcial ni presencia de mocks.

## PR históricos

| Caso | Estado tras revisión | Evidencia / pendiente |
| --- | --- | --- |
| [#131](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/131) | Abierto | La corrección documental de GAP-001 y continuidad todavía no está integrada. dev conserva afirmaciones de ausencia que deben contrastarse con los flujos hoy implementados. Rebasar y actualizar la matriz con evidencia vigente; no reutilizar automáticamente los cierres propuestos sobre una base antigua. |
| [#116](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/116) | Abierto | La aceptación histórica de Secretaría no acepta el circuito completo de Afiliación/Incorporación. AdmissionsPage, AdmissionNormativeEndpoints, WithdrawalSignaturePolicy y la migración CompleteAdmissions2026 del PR siguen ausentes en dev. Rebasar, resolver compatibilidad y validar el alcance completo. |
| [#67](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/67) | Cerrado, reemplazado | La causa original está resuelta en dev mediante 4f829c6239a78fe0ec9845b431c919466a10d245 y 3cc8c8fd64e7d466d1052cabe323a01960abac85. AddAdmissionCaseReferenceToCeremony corre en PmgmDbContext; LinkAdmissionCaseToCeremony en AdmissionsDbContext agrega índice/FK sin repetir la columna. El cliente MinIO usa Quay y el CI vigente pasó. La migración alternativa del PR no debe integrarse. |
| [#60](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/60) | Abierto | No están integrados InstitutionalStatusEndpoints/Policy, TreasuryOrdinaryDuesEligibilityService ni el movimiento de reincorporación. Tener una categoría tarifaria Past Activo no completa las transiciones institucionales. Coordinar con #59 y preservar la regla vigente de Hospitalaria anual separada. |
| [#58](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/58) | Abierto | El ledger personal y sus endpoints/pruebas no están integrados. La tesorería logial actual no completa la cuenta corriente en Mi ficha. Conciliar con #190/#191 para evitar un segundo modelo incompatible. |
| [#45](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/45) | Abierto | La vista transversal y foto protegida existen funcionalmente, pero el texto propuesto para PMGM-REQ-025 no está incorporado. Queda sincronización normativa/documental; rebasar y conservar esta diferencia aunque el producto haya avanzado. |
| [#43](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/43) | Cerrado, reemplazado | Secretaría operacional fue integrada por PR #120 en 5704400c6fdd0b787dcb4ec17e36be9e69f4484a. dev contiene SecretariatOperationsEndpoints, AddOperationalSecretariat, OperationalSecretariatPanel y persistencia de correspondencia, pendientes y tabla; las pruebas operationalSecretariat y políticas de backend están presentes. Se cierra esta implementación alternativa sin fusionarla. |
| [#1](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/1) | Abierto | Es el comparador dev→main. La promoción sigue bloqueada por la pausa y aceptación operacional/UAT pendiente (#97, #25, #27–#32). No corresponde fusionar ni cambiar la referencia congelada como resultado de esta revisión. |

## Issues históricos

| Caso | Estado tras revisión | Evidencia / pendiente |
| --- | --- | --- |
| [#191](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/191) | Abierto | Quedan presentación contable institucional, validación operacional y ampliación de concurrencia a ajustes/cierre o imputación. Los recibos ya tienen pruebas concurrentes y conflictos HTTP 409; esto no completa todo el issue. |
| [#190](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/190) | Abierto | El tarifario está parcialmente integrado; falta aceptación completa del origen oficial, categorías/Oriente/país y tratamiento USD. No cerrar por la existencia de tablas o del aporte propio. |
| [#97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97) | Abierto | Despliegue y QA física/UAT en srv01 siguen pausados. Los gates y paquetes automatizados no sustituyen ejecución en servidor ni aceptación institucional. |
| [#66](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/66) | Abierto | Afiliación e Incorporación completas siguen pendientes en PR #116. La aceptación parcial de Secretaría no cierra estos circuitos. |
| [#65](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/65) | Abierto | Hay avances en Gran Maestría, rechazo y ceremonias. Falta una matriz actual de criterios y evidencia integral del sprint; corregir GAP-001 mediante #131 antes de declarar cierre completo. |
| [#59](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/59) | Abierto | Los estados y reincorporación de PR #60 siguen pendientes. Una categoría tarifaria no implementa sus transiciones; Hospitalaria anual es una regla separada. |
| [#56](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/56) | Abierto | Es un control vinculante de paridad visual/arquitectónica. Queda aceptación de los criterios abiertos y revisión institucional; una captura automatizada no los completa. |
| [#48](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/48) | Abierto | La política admite aprobación del Consejo en los datos de elegibilidad, pero no demuestra el flujo persistido completo de deliberación/votación/autoridad. Queda implementar y validar el circuito sin inventar reglas. |
| [#47](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/47) | Abierto | Existen política de elegibilidad y mínimos configurables. Falta cerrar la evidencia integral desde registros reales, reglas versionadas y decisión autorizada; las piezas AdvancementEvidence de #116 no están integradas. |
| [#46](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/46) | Cerrado, completado | PR #214 integró Presente/Ausente/Justificada. MemberPortalPage muestra fecha, grado, tema, estado y encargado, sólo consulta. MemberPortalPage.test impide progreso temático; LodgeInstructionHttpWorkflowTests cubre registro autorizado y consulta propia. La publicación de Pages ya fue verificada y hoy está en dev vigente. La baseline general/UAT continúa en #56/#97, no es motivo para mantener abierto este cambio técnico. |
| [#44](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/44) | Abierto | La implementación transversal, nombres/Taller, búsqueda y foto protegida está en dev, con DTO minimizado y QA v0.60. Queda comprobar explícitamente el conjunto de autorización HTTP (hermano de otro Taller, anónimo 401 y aislamiento del expediente/foto), además de sincronizar REQ-025 en #45. Los tests de proyección/mock no prueban por sí solos esos escenarios de extremo a extremo. |
| [#37](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/37) | Abierto | La ficha fija no completa formularios configurables, campos personalizados y versionado de definiciones. Falta ese alcance funcional. |
| [#36](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/36) | Abierto | Mi ficha fue restituida parcialmente. Quedan cuenta corriente/recibos y fotografía personal; #58 contiene trabajo aún no integrado. |
| [#32](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/32) | Abierto | El entorno piloto operacional y su evidencia siguen pendientes; no ejecutar mientras srv01 esté pausado. |
| [#31](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/31) | Abierto | La recuperación y decisión final de UAT requieren ejecución operacional y evidencia, no sólo CI/Pre-UAT. |
| [#30](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/30) | Abierto | La ola D requiere UAT operacional de documentos, Biblioteca, Archivo y notificaciones. Subsisten además remanentes funcionales en #2/#3/#5. |
| [#29](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/29) | Abierto | La ola C requiere UAT operacional de insinuados, Secretaría, calendario y Gestión Logial; no aceptada por esta auditoría. |
| [#28](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/28) | Abierto | La ola B requiere UAT operacional de Régimen Interior, regularidad y ceremonias. |
| [#27](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/27) | Abierto | La ola A requiere UAT operacional de identidad, autorización, bootstrap y membresía. |
| [#25](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/25) | Abierto | La UAT trazable de la RC congelada no se ejecutó. Los gates actuales no autorizan promover main ni reemplazar una aceptación del piloto. |
| [#5](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/5) | Abierto | Gran Archivo actual registra/retira y controla documentos. Faltan la jerarquía fondo/sección/serie, préstamos/devoluciones físicos y preservación/derivados del alcance completo. |
| [#4](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/4) | Abierto | CalendarPage sólo ofrece agenda y mes. La vista semanal solicitada no está implementada; el calendario proyectado, ICS y zona horaria no completan ese criterio. |
| [#3](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/3) | Abierto | Hay notificación interna, plantillas, cola y deduplicación. El correo permanece en cola sin adaptador/dispatcher de transporte efectivo; faltan entrega/reintento automático y prueba integrada de proveedor. |
| [#2](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/2) | Abierto | El almacenamiento privado y parte de retención están implementados. El alcance conserva preservación con maestro y derivados de consulta, todavía sin modelo completo; el cierre anterior de #6 no cierra este issue más amplio. |

## Comprobaciones técnicas

- Pruebas dirigidas ejecutadas en esta revisión, sobre el checkout del SHA comparado: `npm test -- src/MemberPortalPage.test.ts src/api/candidateIntakeApi.test.ts src/operationalSecretariat.test.ts`: **3 archivos y 17 pruebas aprobadas**, sin cambios funcionales.
- Backend: se revisaron las pruebas existentes y el CI ya aprobado del SHA comparado (**351/351**). No se ejecutó backend local ni se confundió una prueba mock con autorización HTTP real.
- CI post-merge del SHA comparado: 36719272907; Showcase/Pages: 36719273069; QA installable: 36719272748; Pre-UAT: 36719272840, todos SUCCESS.
- Publicación verificada antes de la auditoría: [demo](https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/); `qa-current.json.sourceSha=972f03d251f17bdcac1546cec27ef6cee02c770b`. ZIP `Proyecto-Centenario-QA-srv01-972f03d251f1.zip`; SHA-256 `264d92887428ac27ee960713a291993597cae2a32a5164387050a5f58cdd7ba5`; **809/809 checksums internos**, BUILD-INFO consistente. Es evidencia del corte comparado, no del futuro merge de este informe.
- #46: `MemberPortalPage.tsx`, `MemberPortalPage.test.ts` y `LodgeInstructionHttpWorkflowTests.cs` conservan los tres estados, consulta propia y protección contra progreso temático. La aceptación técnica integrada de #214 ya está publicada; la baseline general y UAT se conservan en sus issues.
- #43: la alternativa histórica queda reemplazada por `SecretariatOperationsEndpoints.cs`, `20260919210000_AddOperationalSecretariat.cs`, `OperationalSecretariatPanel.tsx` y pruebas de políticas/mock. Integración #120, commit `5704400c6fdd0b787dcb4ec17e36be9e69f4484a`.
- #67: core aplica `20260911181000_AddAdmissionCaseReferenceToCeremony`, Admissions aplica índice/FK en `20260911190000_LinkAdmissionCaseToCeremony`; se evita fusionar la variante vieja que reubica la segunda migración.
- #44 conserva un remanente de verificación, aunque `CandidatePublicationEndpoints.cs`, `GetPublishedPhotoAsync`, `PublishedCandidatePhoto.tsx`, búsqueda de App y pruebas de proyección implementan el comportamiento solicitado. No se declara ausencia de la funcionalidad ni aceptación integral de escenarios HTTP todavía no comprobados.

## Orden propuesto para retomar

1. Corregir la matriz documental de brechas (#131); sincronizar REQ-025 (#45) y completar escenarios HTTP de #44.
2. Recuperar y rebasar #116/#66 y #60/#59 sobre dev vigente, con revisión institucional del alcance. Preservar el contrato móvil/SHA/UI v0.63 de #226.
3. Conciliar #58/#36 con tesorería #190/#191 antes de integrar un ledger personal paralelo.
4. Completar remanentes independientes: correo efectivo #3, calendario semanal #4, preservación #2 y dominio archivístico #5.
5. Mantener #97 y las olas UAT pendientes mientras srv01 esté pausado. Ninguna promoción a main.

## Coordinación y alcance

Sólo este handoff nuevo queda reservado en el primer PR. START-HERE se enlazará mediante una única línea en un PR documental posterior al merge, sin modificar bloques históricos. La Línea Base Maestra recibirá el resultado y la evidencia final mediante actualización puntual y lectura de retorno.

No se modificaron funcionalidades, migraciones, permisos ni reglas institucionales. No se editó ninguna rama histórica; no hubo despliegue en srv01, QA física ni UAT. Los resultados y estados aquí son una fotografía de la auditoría; los nuevos SHA/gates del informe se registrarán en el recibo documental posterior.
