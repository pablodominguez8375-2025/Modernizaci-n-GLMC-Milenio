# G3: enlace OpenResty → glmdch → QA LAB2 (18-10-2026)

**ESTADO: DISEÑO. NO EJECUTAR NI PUBLICAR AÚN.** Este documento no autoriza cambios de red, DNS, TLS, firewall, servicios, contenedores, agentes, secretos ni rama main. Referencias: issues #517 y #97; #470 y #479 como controles de seguridad.

## 1. Evidencia real del operador

- Dominio HTTPS canónico: https://sistema.granlogiamixta.cl/ ; DNS público IPv4 8.242.201.225.
- La VM glmdch escucha en 172.17.5.107/24 (ens18); para gateway QA los únicos listeners observados son **127.0.0.1:18080** y **127.0.0.1:18443**. No hay listener TCP 80 o 443 en esa VM.
- Health web QA HTTPS local en 127.0.0.1:18443: **HTTP 200** (con SNI/Host canónico). HTTP público raíz y OIDC well-known: **502**; cabecera del proxy público **Server: openresty**; certificado del endpoint público verificado por curl (ssl_verify_result=0). No es evidencia de fallo TLS.
- Jorge Pareja indicó en mensaje del 13-10 aportado por Sponsor: «la web de sistema.granlogiamixta.cl también está creado y redireccionado a la VM» y «localmente debe estar en el puerto 80».
- **Hipótesis probable, no confirmada:** el proxy exterior intenta alcanzar la VM por HTTP TCP 80, donde no hay listener. Falta leer configuración real del upstream o logs de OpenResty. La respuesta IPv6 ::ffff:8.242.201.225 indica dirección IPv4 mapeada, no prueba AAAA DNS nativo.
- El LAB2 funciona en privado y G8 fase 1 de diez sesiones OIDC/RBAC aprobó. G3 público, G8 matriz completa, G10 visual y UAT **pendientes**. Respaldo/restauración cifrados de dos BD y Silo probados en contenedores aislados de la misma VM; DR en otro host no probado.

## 2. Contrato de información indispensable con administrador perimetral

Antes de abrir cualquier puerto, obtener respuesta de Jorge o responsable autorizado SIN secretos:
1. Upstream **efectivamente configurado** para el virtual host: IP destino, puerto, protocolo HTTP/HTTPS, existencia de NAT o balanceador intermediario. Confirmar o rechazar destino 172.17.5.107:80.
2. IP/subred **de origen real** que recibe glmdch desde proxy, incluidos NAT/encadenamiento; ACL existente; ¿ruta proxy→VM es red privada aislada y bajo control institucional o cruza un tramo no confiable?
3. Extracto **sanitizado** del error_log que explique el 502 y control del cambio/reversión. No compartir credenciales de panel, configuración completa, claves ni datos personales.
4. Confirmar preservación de Host canónico, X-Forwarded-Proto=https, IP del cliente y WebSocket, si corresponde; declarar cuáles proxies son explícitamente confiables.

Si falta alguno de los cuatro datos, **G3 HOLD**. No asumir topología por un mensaje del operador.

## 3. Dos rutas de implementación mutuamente excluyentes, sujetas a aprobación

**A — Proxy entra por red privada confiable a VM:80 confirmado.** Diseñar un puente receptor únicamente en la IP observada 172.17.5.107 y TCP 80, nunca wildcard 0.0.0.0. Restringir **a nivel de red y host** el origen a las IPs reales del proxy, con denegación por defecto y pruebas negativas. Inspeccionar posibles servicios/colisiones antes. Su única salida es el gateway QA de loopback 127.0.0.1:18443 por HTTPS, validando CA/certificado y SNI del dominio; prohibido saltar verificación TLS. Mantener 18080 y 18443 vinculados exclusivamente a loopback, sin exposición pública. Reconstruir headers Forwarded y X-Forwarded desde salto confiable; rechazar valores suplantados del cliente. Verificar que un tramo HTTP en red privada cumple los controles de aislamiento y seguridad aprobados. Ninguna instalación se realiza con este documento.

**B — Destino distinto o enlace no confiable.** NO exponer HTTP claro hacia VM. Corregir upstream en la capa controlada por Jorge o establecer enlace autenticado cifrado (mTLS/VPN/túnel privado) y probar revocación. No abrir puertos, DNS, NAT ni firewall por ensayo-error.

**Dependencias transversales:** issue #470 — IP cliente auditada mediante KnownProxies/KnownNetworks precisos, BD2 y auditoría append-only con controles físicos; issue #479 — S3/MinIO mínimo privilegio y prueba en QA real; protección Keycloak admin/master; Silo sólo sintético con paquete firmado; secretos técnicos fuera de Git/Drive, sin cambiar main o srv01.

## 4. Criterios de aceptación y rollback

1. Aprobación escrita del responsable perimetral e ingeniería/seguridad del cambio exacto: protocolo, IP destino, IP origen, puerto, confianza y rollback.
2. Desde red externa genuina, https://sistema.granlogiamixta.cl/ debe responder el contenido esperado sin 502; OIDC well-known del realm pmgm HTTP 200 con issuer HTTPS canónico y redirects/webOrigins SOLO bajo FQDN institucional (sin localhost/IP/HTTP publicados).
3. Accesos /identity/admin y /identity/realms/master denegados; /auth/callback, login/logout y /identity/* funcionan. Probar 401 para anónimo, 403 para perfil/Taller no autorizado, y bloqueo directo de 18080/18443. No publicar rutas de administración, métricas ni consola.
4. Cabeceras seguras: HSTS, CSP de SPA sin romper Keycloak, Host y esquema HTTPS preservados; auditar IP de cliente real desde solicitud externa sin confiar en X-Forwarded falsificados.
5. Ejecutar smoke HTTPS/OIDC/API, negaciones por perfil, G8 matriz avanzada y G10 en 360/390/768/1440. Revisar #470/#479 antes de G3 PASS; registrar evidencia y diferencia entre SHA instalado y dev actual.
6. Ensayar reversión aislada del bridge/regla/upstream para volver a gateway local privado sin reiniciar ni borrar QA LAB2. Definir operador, ventana y plan de reversión previamente.

**No equivale a producción, UAT ni permiso de auto-deploy.** Sin segunda VM, DR entre hosts permanece no verificado.

## 5. Registro de continuidad

Documento derivado del diagnóstico y mensajes registrados el 18-10-2026 en issues #97 y #517 y bitácora de QA: https://docs.google.com/document/d/1I9bcJ7lKH4T2iQvxafY63HbOBr0ZsGZ8lDX3tSe2UfM/edit

Corte consultado: **dev f32da189e6b82ea34098ff5417db45f9c377eb98**; **main 6dfb9546a4873baff15955cf86abfd7d47e3d111** sin modificación; LAB2 firmado instalado sobre **4ef456fee3a578a9e6418f3f7072af5dd79eb045**. No equiparar este diseño con cambios físicos verificados.

## 6. Ejecución sin Jorge, autorizada por Sponsor solo para QA port80 (18-10-2026)

La restricción operativa cambió: no existe acceso a Jorge; el Sponsor pide expresamente resolver mediante el puerto 80 desde la VM. Por lo tanto se preparó **un instalador opt-in con retorno automático**, no un ajuste de OpenResty ni de firewall. Está en `scripts/qa/install-g3-port80-bridge.sh`, con prueba estática en `tests/test_qa_g3_port80_bridge.py`, ambos en esta misma PR DRAFT. La ejecución física exige que un administrador del host lo lance mediante el canal existente Termius; el conector GitHub/Drive de ChatGPT **no ejecuta comandos en glmdch**.

Su contrato es intencionalmente restringido:
- Verifica IP privada exacta `172.17.5.107/24`, puerto TCP 80 libre y que exista un único contenedor Caddy activo que publica `127.0.0.1:18443->443/tcp`.
- Reutiliza **la misma imagen Docker local** del gateway vigente, con `--pull never`; combina CA pública del host y root CA local pública de Caddy si existe; exige `curl --cacert` con SNI institucional + respuesta health 200 antes de permitir nuevo listener.
- Levanta contenedor **separado** `centenario-qa-g3-port80` en modo host-net para alcanzar loopback, con rootfs read-only, caps mínimas, usuario numérico aislado, Caddy admin/auto_https apagados. Su socket queda ligado exclusivamente a `172.17.5.107:80` y su origen TCP admitido es **solo la red `172.17.5.0/24`**. Si el proxy externo tiene otra IP de origen, responderá 403 (no prometer 200 público): ampliar ACL solo tras comprobar IP concreta de la conexión.
- Filtra consola Keycloak y realm master, preserva Host institucional y X-Forwarded-Proto=https, y reenvía a gateway QA **por HTTPS con validación CA y nombre**. No confía ciegamente en headers de cliente. Esto **no prueba la auditoría de IP real** #470 ni la seguridad de un posible tramo perimetral HTTP sin cifrar; ambos deben resolverse antes de cerrar G3 como puerta institucional.
- Validación de sintaxis Caddy previa y health a través de TCP80; si falla la prueba posterior, retira automáticamente el contenedor nuevo. No reinicia ni cambia LAB2. No modifica UFW, DNS, NAT, Docker Compose ni volúmenes. Reversión explícita: `sudo docker rm -f centenario-qa-g3-port80`.
- **Salida esperada con éxito local:** `G3: PUENTE_LOCAL_PASS TCP 172.17.5.107:80 -> HTTPS 127.0.0.1:18443`. Solo la prueba desde red externa del FQDN permite determinar si el 502 se resolvió. Si 502 persiste, faltan permisos de red/NAT/upstream del perímetro, inaccesibles mediante SSH a la VM, por lo cual no inventar reparación de ese componente.

**G3 no está aprobado por preparar ni ejecutar el bridge**: aún deben pasar controles de IP real #470, IAM #479, OIDC, accesos negativos, G8 ampliado/G10 y una prueba externa de ruta. El acceso HTTP proxy→VM en red interna se acepta aquí únicamente como candidato limitado a QA sintético; validar aislamiento y origen real. Queda prohibida la apertura wildcard o por IP pública sin controles comprobados.

## 7. Incidente de ejecución 18-10-2026: validación PASS, health TCP80 no conecta

**Evidencia física aportada por operador Termius:** archivo original descargado, `bash -n` PASS, socket QA `127.0.0.1:18443` LISTEN; su `sudo bash /tmp/centenario-g3-port80.sh` ejecutó preflight de CA Caddy, `caddy validate` respondió **Valid configuration**, luego `curl` a `sistema.granlogiamixta.cl:80` dio **(7) Could not connect to server** (HTTP `000`), y el instalador retiró `centenario-qa-g3-port80` automáticamente. La advertencia de `caddy fmt` no es un error bloqueante. **No se puede concluir todavía** si fue arranque asíncrono sin espera o Caddy sin permiso de bind 80 / archivos runtime: el contenedor previo se borró sin captura de logs.

**Corrección en la misma PR DRAFT:** script reintenta 24 veces con pausa de 0,5 segundos, lo inicia sin política de reinicio, habilita `unless-stopped` solo después de verificar HTTP 200 y, al fallar, obtiene `docker inspect` estado, código de salida, `docker logs --tail 20` y el umbral del puerto sin privilegios `/proc/sys/net/ipv4/ip_unprivileged_port_start`; después revierte exclusivamente el contenedor nuevo. Test offline comprueba orden de reintentos, lectura de logs previa a rollback y restarts post-health. **No dar por reparado G3 ni afirmar que el host cambió** sin nueva ejecución física. En caso de `bind: permission denied`, no elevar privilegios del puente ni cambiar sysctl global automáticamente; registrar evidencia y diseñar corrección explícita.

El operador debe descargar una **nueva versión pinneada**, verificar `git hash-object` contra el nuevo blob y ejecutar con `sudo bash` como subproceso **sin `set -e` en la shell interactiva**; la versión antigua local no contiene la corrección. Si diagnostica TLS o permisos, continuar por seguridad sin forzar deployment ni abrir puerto wildcard.
