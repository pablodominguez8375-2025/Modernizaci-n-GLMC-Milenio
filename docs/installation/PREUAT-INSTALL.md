# Proyecto Centenario — instalación pre-UAT

Este documento acompaña el paquete instalable pre-UAT. El paquete se construye desde un SHA exacto y no contiene secretos ni datos institucionales reales.

## Servidor recomendado

- Ubuntu Server 24.04 LTS x86_64.
- 8 vCPU, 32 GB RAM y 500 GB de almacenamiento expandible para una VM institucional.
- Docker Engine y Docker Compose v2.
- DNS institucional y puertos 80/443 disponibles para el modo piloto HTTPS.
- Salida HTTPS para descarga inicial de imágenes de contenedor y actualizaciones.

Para laboratorio/UAT se puede utilizar como mínimo 4 vCPU, 16 GB RAM y 150 GB.

## Contenido del paquete

- Código fuente exacto del candidato pre-UAT.
- `infrastructure/docker-compose.first.yml`: instalación autenticada de primera implementación para pruebas controladas.
- `infrastructure/docker-compose.pilot.yml`: piloto institucional con HTTPS/OIDC.
- `infrastructure/first.env.example` y `infrastructure/pilot.env.example`: plantillas sin secretos.
- Scripts de preflight, backup, restore y recovery drill.
- Documentación técnica y funcional versionada.
- `BUILD-INFO.txt` y `MANIFEST.sha256` para trazabilidad e integridad.
- `pmgm-repository.bundle` para conservar la historia Git incluida en el paquete.

## Instalación rápida en VM

1. Descomprima el ZIP en una carpeta exclusiva, por ejemplo `/opt/proyecto-centenario`.
2. Revise `BUILD-INFO.txt` y verifique `MANIFEST.sha256`:

   ```bash
   sha256sum -c MANIFEST.sha256
   ```

3. Para un laboratorio autenticado, copie la plantilla:

   ```bash
   cp infrastructure/first.env.example infrastructure/first.env
   ```

4. Edite `infrastructure/first.env` y reemplace todas las claves y contraseñas de ejemplo. No reutilice secretos de QA.
5. Valide el host y la configuración Docker disponible según los scripts del paquete.
6. Levante la primera implementación:

   ```bash
   docker compose --env-file infrastructure/first.env -f infrastructure/docker-compose.first.yml up -d --build
   ```

7. Verifique el estado:

   ```bash
   docker compose --env-file infrastructure/first.env -f infrastructure/docker-compose.first.yml ps
   ```

8. Antes de cualquier carga institucional, pruebe backup y restore en un ambiente de ensayo con los scripts incluidos.

## Piloto institucional HTTPS

Para la VM definitiva use `infrastructure/pilot.env.example` como base. Configure dominio, certificados/ACME, OIDC y secretos reales en `infrastructure/pilot.env`, nunca en Git.

Ejecute primero:

```bash
./scripts/preflight-pilot.sh
```

Después levante el stack:

```bash
docker compose --env-file infrastructure/pilot.env -f infrastructure/docker-compose.pilot.yml up -d --build
```

## Actualizaciones

Cada paquete pre-UAT o candidato posterior identifica el SHA fuente. Antes de actualizar:

1. backup verificado;
2. ventana de mantenimiento;
3. revisión de migraciones;
4. despliegue del nuevo paquete;
5. health checks y smoke test autenticado;
6. recovery drill según el procedimiento institucional.

No se debe reemplazar una instalación productiva directamente desde la rama `dev`. La promoción estable requiere UAT aprobado y autorización institucional del Sponsor.

## Seguridad y privacidad

- No habilite `DemoData:Enabled` en Production.
- Mantenga MinIO/S3 privado; el contenido documental se entrega únicamente por endpoints autenticados.
- La ficha privada de insinuados no es la publicación transversal. RUT, domicilio, correo, teléfono, entrevista, documentos y observaciones internas no deben aparecer en la vista general.
- Ley 21.719: antes de producción deben cerrarse las revisiones legales pendientes, políticas de retención configurables, encargados/transferencias y controles organizacionales aplicables.
