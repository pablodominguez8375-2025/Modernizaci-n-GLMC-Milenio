# PMGM QA v0.43 — Carga directa de fotografía del insinuado

## Resultado

La ficha de insinuación permite seleccionar una fotografía desde computador o teléfono, verla antes de guardar y cargarla directamente al expediente privado. La demo y el instalable comparten el mismo contrato de interfaz.

## Reglas aplicadas

| Control | Demo | Instalable/API |
|---|---:|---:|
| JPG o PNG | Sí | Sí |
| Máximo 100 KB | Sí | Sí |
| Mínimo 500 × 500 píxeles | Sí | Sí |
| Vista previa antes de guardar | Sí | No aplica |
| Análisis antimalware | Simulado con dato ficticio | ClamAV obligatorio |
| Almacenamiento privado | Memoria temporal de la demo | `IDocumentObjectStore` privado |
| Integridad SHA-256 | No aplica | Sí |
| Evidencia de auditoría | Estado demostrativo | Sí |
| Reenvío a Gran Secretaría | Sí | Sí |
| Bloqueo después de publicación | Sí | Sí |

## Flujo

1. Secretaría Logial guarda la ficha del insinuado.
2. Selecciona un archivo JPG o PNG.
3. El navegador comprueba formato, peso y dimensiones y muestra la vista previa.
4. Al confirmar, la API repite todas las validaciones; el cliente no constituye una frontera de seguridad.
5. El instalable analiza el binario con ClamAV, calcula SHA-256 y lo almacena sin URL pública permanente.
6. La nueva fotografía se vincula al mismo expediente y su revisión vuelve a `pending_grand_secretariat`.

## Privacidad

La fotografía se clasifica como sensible, usa acceso `management_only` y queda dentro de una colección documental privada del Taller. La demo utiliza únicamente archivos que el evaluador seleccione localmente y no los envía a un servicio real.

## Pruebas esperadas

- Rechazo de extensión/tipo MIME no permitido.
- Rechazo sobre 100 KB.
- Rechazo si ancho o alto es menor a 500 píxeles.
- Aceptación en el límite exacto de 500 × 500.
- Persistencia del archivo seleccionado en el estado de demostración.
- Inmutabilidad del expediente una vez aprobado/publicado.
