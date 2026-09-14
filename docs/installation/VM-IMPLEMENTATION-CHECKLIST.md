# Proyecto Centenario — checklist de implementación de VM

Este checklist deja evidencia mínima para instalar, validar y eventualmente promover una VM institucional. Debe completarse por cada instalación o actualización relevante.

## A. Identificación del paquete

- [ ] Fecha de instalación registrada.
- [ ] Responsable técnico registrado.
- [ ] `SOURCE_SHA` copiado desde `BUILD-INFO.txt`.
- [ ] `BUILD_RUN_NUMBER` copiado desde `BUILD-INFO.txt`.
- [ ] SHA-256 externo del ZIP verificado.
- [ ] `MANIFEST.sha256` interno validado sin errores.
- [ ] Canal confirmado: pre-UAT / RC / estable.

Evidencia:

```text
Fecha:
Responsable:
SOURCE_SHA:
BUILD_RUN_NUMBER:
Hash ZIP:
Canal:
```

## B. VM e infraestructura

- [ ] Ubuntu Server 24.04 LTS x86_64 o plataforma aprobada equivalente.
- [ ] CPU suficiente: 8 vCPU recomendado / 4 vCPU mínimo UAT.
- [ ] RAM suficiente: 32 GB recomendado / 16 GB mínimo UAT.
- [ ] Almacenamiento: 500 GB recomendado / 150 GB mínimo UAT.
- [ ] Docker Engine instalado.
- [ ] Docker Compose v2 disponible.
- [ ] Hora/NTP correctos.
- [ ] DNS institucional definido.
- [ ] Puertos TCP 80 y 443 disponibles.
- [ ] SSH administrativo restringido.
- [ ] Firewall revisado.
- [ ] PostgreSQL, MinIO y servicios internos no expuestos públicamente.
- [ ] Destino de respaldo externo a la VM definido.

Evidencia:

```text
Hostname VM:
IP privada:
FQDN institucional:
vCPU:
RAM:
Disco:
Docker:
Docker Compose:
Destino backup externo:
```

## C. Secretos y configuración

- [ ] `/etc/pmgm/pmgm.env` creado fuera del repositorio.
- [ ] Permisos restrictivos aplicados al archivo de secretos.
- [ ] Placeholders de ejemplo eliminados.
- [ ] Password PostgreSQL robusto y exclusivo.
- [ ] Password Keycloak robusto y exclusivo.
- [ ] Credenciales MinIO robustas y exclusivas.
- [ ] Identidad bootstrap institucional revisada.
- [ ] URL pública usa HTTPS.
- [ ] No se reutilizan credenciales QA/demo.
- [ ] No existen secretos dentro de Git.

## D. Instalación técnica

Comando recomendado:

```bash
./INSTALAR.sh
```

Registrar resultado:

- [ ] Integridad del paquete OK.
- [ ] Preflight de VM OK.
- [ ] Preflight de piloto OK.
- [ ] Contenedores construidos/iniciados.
- [ ] HTTPS disponible.
- [ ] OIDC/Keycloak disponible.
- [ ] Login institucional de prueba correcto.
- [ ] Smoke autenticado OK.
- [ ] Health checks OK.
- [ ] Sin errores críticos en logs.

Evidencia:

```text
Fecha/hora instalación:
URL validada:
Resultado preflight VM:
Resultado preflight piloto:
Resultado smoke:
Observaciones:
```

## E. Persistencia, backup y recuperación

- [ ] PostgreSQL operativo.
- [ ] MinIO operativo y privado.
- [ ] Backup inicial creado.
- [ ] Backup inicial verificado.
- [ ] Manifest del backup validado.
- [ ] Copia del backup transferida fuera de la VM.
- [ ] Procedimiento de restauración conocido por el operador.
- [ ] Recovery drill realizado en entorno controlado o CI correspondiente.

Evidencia:

```text
Ruta backup inicial:
Fecha backup:
SHA/manifest:
Destino copia externa:
Resultado verificación:
Resultado recovery drill:
```

## F. Seguridad y privacidad

- [ ] `DemoData:Enabled` deshabilitado para operación institucional.
- [ ] Acceso público limitado a HTTPS.
- [ ] Servicios internos sin publicación directa a Internet.
- [ ] Roles y claims OIDC revisados.
- [ ] Auditoría habilitada.
- [ ] Almacenamiento documental privado.
- [ ] Flujo antimalware disponible para documentos.
- [ ] Gates de Ley 21.719 del CI aprobados para el SHA instalado.
- [ ] Clasificación de datos aprobada para el SHA instalado.
- [ ] Migration safety gate aprobado.
- [ ] No se cargan datos personales reales hasta completar aprobación institucional/jurídica aplicable.

## G. UAT institucional

- [ ] Perfil Hermano validado.
- [ ] Perfil Autoridad de Taller validado.
- [ ] Perfil Autoridad de Gran Logia validado.
- [ ] Mi ficha validada.
- [ ] Gestión Logial validada.
- [ ] Régimen Interior validado.
- [ ] Gran Secretaría validada.
- [ ] Tesorería validada.
- [ ] Hospitalaria validada.
- [ ] Insinuados/ceremonias validados.
- [ ] Biblioteca Virtual validada.
- [ ] Gran Archivero validado.
- [ ] Calendario y notificaciones validados.
- [ ] Permisos negativos probados.
- [ ] Observaciones UAT registradas en GitHub.
- [ ] Observaciones críticas cerradas o formalmente aceptadas.

## H. Aprobación y promoción

La instalación sólo puede considerarse candidata a estable cuando exista evidencia de los pasos anteriores.

- [ ] CI exact-head completamente verde.
- [ ] UAT aprobado.
- [ ] Sponsor/Product Owner aprueba la versión.
- [ ] Tag/versión estable identificado.
- [ ] Backup inmediatamente anterior a promoción verificado.
- [ ] Plan de rollback definido.
- [ ] Ventana de mantenimiento acordada.
- [ ] Responsable de soporte identificado.

Evidencia de aprobación:

```text
Versión/tag:
SHA aprobado:
Fecha UAT:
Resultado UAT:
Aprobación Sponsor/Product Owner:
Fecha de promoción:
Responsable técnico:
Plan de rollback:
```

## I. Actualizaciones posteriores

Para cada nueva versión aplicada sobre la misma VM:

- [ ] No se actualiza automáticamente desde `dev`.
- [ ] Se identifica tag/SHA aprobado.
- [ ] Se toma backup verificado antes del cambio.
- [ ] Se revisan migraciones.
- [ ] Se ejecuta actualización en ventana controlada.
- [ ] Se repiten health checks y smoke.
- [ ] Se ejecuta validación funcional mínima.
- [ ] Se conserva evidencia de versión anterior y nueva.

Este checklist forma parte de la trazabilidad técnica del Proyecto Centenario y no reemplaza las actas, aprobaciones institucionales ni evaluaciones jurídicas que correspondan.
