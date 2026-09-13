# PMGM QA v0.54 — Demo operacional y datos temporales

## Alcance

La demo pública utiliza datos completamente ficticios y simula 20 Talleres. Cada Taller contiene 24 registros operativos: 12 Maestros, 5 Compañeros, 5 Aprendices y 2 PAS activos.

## Cargas temporales

- Fotografía de insinuado: JPG, PNG o WebP, máximo 10 MB.
- Antecedente de entrevista: Word `.docx` o PDF, máximo 50 MB.
- La carga se conserva sólo en memoria del navegador durante la sesión QA.
- Al cerrar o recargar la demo, los archivos temporales desaparecen.
- La demo no publica fotografías ni documentos cargados y no debe utilizar datos reales.

## Pruebas de aceptación

1. Seleccionar un Taller y comprobar los 24 registros y la distribución por grado/PAS.
2. Abrir una ficha de insinuado, cargar una fotografía y comprobar que queda disponible durante la sesión.
3. Registrar tres entrevistas, cargar Word/PDF y verificar que sólo se muestran resumen y resultado.
4. Agregar una cuarta entrevista para comprobar que el flujo admite adicionales.
5. Recargar la página y confirmar que las cargas temporales ya no están disponibles.
