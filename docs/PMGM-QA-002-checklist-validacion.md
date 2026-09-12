# PMGM-QA-002 — Checklist de validación de ambiente QA

**Estado:** Activo  
**Uso:** ejecutar después de cada despliegue relevante y antes de promover un incremento.

## A. Identificación del despliegue
- [ ] Fecha/hora registrada.
- [ ] Responsable registrado.
- [ ] SHA Git desplegado registrado.
- [ ] Rama/tag de origen registrada.
- [ ] CI del SHA revisado y verde.
- [ ] Backup previo registrado cuando corresponde.

## B. Infraestructura
- [ ] `docker compose config` sin errores.
- [ ] PostgreSQL en ejecución y saludable.
- [ ] MinIO en ejecución.
- [ ] Bucket `pmgm-documents` creado y sin acceso anónimo.
- [ ] ClamAV en ejecución.
- [ ] API en ejecución.
- [ ] Frontend en ejecución.
- [ ] PostgreSQL no expuesto públicamente.
- [ ] MinIO/console no expuestos públicamente.
- [ ] ClamAV no expuesto públicamente.
- [ ] Sólo web/reverse proxy accesible según diseño.

## C. Salud
- [ ] `/health/web` responde correctamente.
- [ ] `/health/live` responde correctamente.
- [ ] `/health/ready` confirma PostgreSQL.
- [ ] `/api/system/info` informa versión esperada.

## D. Seguridad documental
- [ ] Carga de archivo permitido funciona.
- [ ] Archivo con extensión/MIME inconsistente es rechazado.
- [ ] Archivo con firma binaria inconsistente es rechazado.
- [ ] SHA-256 queda calculado/verificado internamente.
- [ ] ClamAV analiza antes de disponibilidad.
- [ ] Documento no disponible no puede descargarse.
- [ ] Bucket permanece privado.
- [ ] URL/ID directo no elude autorización.

## E. Biblioteca Virtual
- [ ] Sólo recursos publicados aparecen en catálogo.
- [ ] Sólo versiones disponibles pueden descargarse.
- [ ] Grado 1 ve recursos con grado mínimo 1.
- [ ] Grado 1 NO ve ni descarga grado mínimo 2 o 3.
- [ ] Grado 2 ve recursos grado mínimo 1 y 2.
- [ ] Grado 2 NO ve ni descarga grado mínimo 3.
- [ ] Grado 3 ve recursos grado mínimo 1, 2 y 3 si cumple las demás reglas.
- [ ] Conteos/facetas no revelan recursos de grado superior.
- [ ] Búsqueda por ID directo no vulnera permisos.
- [ ] Descarga desde interfaz está autenticada/autorizada.

## F. Gran Archivo — cuando el módulo esté implementado
- [ ] Catálogo y contenido tienen permisos separados.
- [ ] Registro `hidden` no aparece a usuario no autorizado.
- [ ] `catalog_only` muestra sólo metadata permitida.
- [ ] Acceso individual por hermano funciona con vigencia.
- [ ] Restricción por grado funciona.
- [ ] Grado + autorización individual pueden combinarse.
- [ ] Investigaciones/amonestaciones/disciplinarios parten restringidos.
- [ ] Acceso sensible queda auditado.
- [ ] Publicar en Biblioteca no modifica original archivístico.

## G. Gran Secretaría — cuando el muro institucional esté implementado
- [ ] Publicación para todos los hermanos autorizados funciona.
- [ ] Segmentación por grado funciona.
- [ ] Segmentación por Taller/cargo/hermano funciona.
- [ ] Documento restringido no filtra metadata sensible.
- [ ] Versionado oficial conserva historial.
- [ ] Confirmación de lectura funciona cuando se exige.
- [ ] Notificación no incluye contenido reservado por canal inseguro.

## H. Ventana Fraterna — cuando el módulo esté implementado
- [ ] Sólo intranet autorizada accede.
- [ ] Crear/publicar/retirar aviso funciona.
- [ ] Búsqueda y categorías funcionan.
- [ ] Reporte y moderación quedan auditados.
- [ ] Adjuntos usan ciclo documental seguro.
- [ ] No existe publicación pública externa por defecto.

## I. Núcleo institucional
- [ ] Consulta de miembro funciona.
- [ ] Historia de pertenencia se conserva.
- [ ] Transferencia de Taller conserva origen y crea pertenencia destino.
- [ ] Régimen Interior accede sólo con rol correcto.
- [ ] Gran Tesorería y Gran Hospitalaria mantienen sus fuentes diferenciadas.

## J. Ceremonias
- [ ] Elegibilidad consulta Régimen Interior.
- [ ] Elegibilidad consulta Tesorería.
- [ ] Elegibilidad consulta Hospitalaria.
- [ ] Iniciación respeta plazo de publicación configurado.
- [ ] Gran Secretaría no emite autorización antes del flujo previo.
- [ ] Evidencias de decisión quedan congeladas/auditadas.

## K. Privacidad Ley 21.719
- [ ] No se usa RUT como PK técnica.
- [ ] Datos de prueba son sintéticos o están autorizados.
- [ ] Roles aplican mínimo privilegio.
- [ ] Logs no contienen tokens ni secretos.
- [ ] Auditoría registra actor/acción sin copiar datos sensibles innecesarios.
- [ ] Retención/legal hold se comportan según política aplicable.

## L. UX y localización
- [ ] UI se presenta en español de Chile donde corresponda.
- [ ] Fechas/horas son coherentes con `America/Santiago`.
- [ ] Moneda se presenta como CLP en módulos aplicables.
- [ ] Navegación principal no muestra módulos sin permiso.
- [ ] Errores de autorización no revelan información interna.

## M. Resultado
- [ ] APROBADO PARA SIGUIENTE ETAPA.
- [ ] APROBADO CON OBSERVACIONES.
- [ ] RECHAZADO.

### Observaciones
Registrar defectos, evidencia, severidad, responsable y decisión de corrección antes de promoción.
