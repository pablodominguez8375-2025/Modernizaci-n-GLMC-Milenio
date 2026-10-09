# Cruce normativo y helper de reducción de dispensas — 2026-10-09

## Fuentes oficiales cotejadas
- Drive, carpeta Proyecto Centenario: `Constitucion y Reglamento.pdf` (ID `19QlyAuknJ5hHBXNxVHiZMPqKmHpg9nTp`), Reglamento General arts. 3.2 a 3.6.
- `PROTOCOLO-PARA-LA-TRAMITACIÓN-DE-INSINUACIONES-AFILIACIONES-Y-SOLICITUDES-DE-CEREMONIAS-2026.docx` (ID `12SS3w2JCmpbMnC9p9KUL4jytsGle6Bp2`), de 31-08-2026.
- Línea Base Maestra (ID `1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM`), sección elegibilidad de ascensos. Código `dev@62049891eb9bdedc1c0adeab8d2ed9747969b471` al iniciar.

## Reglas verificadas
1. Art. 3.2 a): el graduante debe estar a plomo con Tesorería.
2. Art. 3.2 b/c): **dos trabajos diferentes**: uno de simbolismo del grado, otro de cultura general vinculado a enseñanza masónica. Ambos deben calificarse y aprobarse por Cámara del Medio y acompañar la solicitud; no basta contar dos cargas de archivo.
3. Art. 3.2 d): Aumento, 2 años continuados de Aprendiz, 30 Tenidas de 1° en los últimos 2 años y 10 Cámaras de Instrucción.
4. Art. 3.2 e): Exaltación, 2 años continuados de Compañero, 10 Tenidas de 2° en los últimos 2 años y 10 Cámaras de Instrucción.
5. Art. 3.3: propuesta por Vigilante o Venerable Maestro, aprobada por 2/3 de los Maestros asistentes en Cámara del Medio. **Distinta de la dispensa**.
6. Art. 3.4: para dispensa, en caso calificado y Tenida expresamente citada, acuerdo **unánime** de Cámara del Medio; máximo reducción del 50 % de d/e (antigüedad y asistencias). No afecta trabajos b/c ni Tesorería a).
7. Art. 3.6: autorización de Gran Secretaría previa a celebración.
8. El Protocolo exige identificar requisito y reducción en formulario, y contempla validaciones y visto bueno de instancias institucionales.

## Cambio técnico de alcance limitado
`AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum` sólo computa el mínimo efectivo posible a partir de una reducción autorizada **en unidades**, y rechaza excesos, números negativos, requisitos desconocidos y cualquier reducción de planchas. Para mínimo impar se admite reducir como máximo `floor(original/2)` unidades. No configura cantidades por decreto, ni implementa votación, ni valida autenticidad documental, ni se conecta a `AuthorizeAsync` o modifica los permisos.

Pruebas puras nuevas de límites, valores impares, prohibición de reducir trabajos, error y casos ordinarios. **Sin migraciones ni alteraciones de rama main.**

## Brechas/next
- Art. 3.2 b/c: registrar evidencia diferenciada por tipo de trabajo y acta de aprobación Cámara del Medio. Las versiones de planchas vinculadas a Tenida de PR #401 no acreditan presentación/aprobación.
- Versionar el mínimo de antigüedad continuada y la evidencia de continuidad: PR #402 es cronología, no dictamen normativo.
- Expediente real de dispensa: Tenida citada, padrón de Maestros habilitados (no llamar “padrón” a lista genérica; usar nómina de habilitados), votación unánime, acta, alcance/reducción y resolución de Régimen Interior, sin que estados booleanos sustituyan documentos.
- Integrar helper en autorización únicamente con evidencia verificable y snapshot versionado; impedir dispensar documentos b/c o dejar pasar pendientes.
- CI/Showcase/QA exact-head pendientes hasta validación del PR; `srv01` sigue pausado #97. `main` no se toca.
