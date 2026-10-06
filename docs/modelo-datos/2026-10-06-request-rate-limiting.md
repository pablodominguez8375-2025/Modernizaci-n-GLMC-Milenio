# Registro GOV-004 — límites operacionales de solicitudes — #331 / PR #332

Base: dev `5b2d131dcfd9da0104eb680e0a79b8760616fa77`. Fuente: instrucciones Drive de endurecimiento del 05-10 y continuación del PO del 06-10.

## Antes y después

Antes: las solicitudes autorizadas no tenían límite de frecuencia en la API. Después: presupuestos por sujeto autenticado e issuer, independientes por categoría, aplicados tras autenticación/autorización y antes de las operaciones de dominio. No se particiona por Taller, URL, consulta o encabezados enviados por el cliente. Al agotarse un presupuesto se devuelve HTTP 429, `Retry-After`, `Cache-Control: no-store` y el JSON público `{"message":"Has realizado demasiadas solicitudes. Espera un momento y vuelve a intentar."}`; el handler de dominio no se ejecuta. 401/403 conservan precedencia.

| Campo operacional | Tipo | Default | Validación / significado |
|---|---|---|---|
| RequestRateLimiting:WritesPerMinute | int | 120 | Positivo; escrituras API, excluidas cargas |
| RequestRateLimiting:UploadsPerMinute | int | 20 | Positivo; POST/PUT de rutas API terminadas en /contenido |
| RequestRateLimiting:ExportsPerMinute | int | 30 | Positivo; GET/HEAD de /contenido y /ics |
| Clave efímera de partición | string en memoria | categoría + issuer + subject | Datos de identidad usados internamente; no publicados ni persistidos |
| HTTP 429 message | string público | Mensaje anterior | Sin identidad, Taller ni detalle interno |
| Retry-After | entero de segundos | Calculado por ventana | Al menos 1 segundo; sólo al rechazar |

Ventana de 60 segundos y cola cero. Los valores son configuración técnica inicial ajustable, no norma institucional ni prueba de capacidad del entorno. El budget es local al proceso; reiniciar lo reinicia y réplicas no comparten contadores. Lecturas normales, health y OPTIONS no consumen estos presupuestos. Las exportaciones CSV en navegador no pasan por API; no se atribuye protección HTTP a ellas. La demo estática tampoco ejecuta este middleware. No confiar en X-Forwarded-For recibido para particionar.

Sin migración: no se alteran entidades, tablas, campos persistentes, PK/FK, relaciones, índices, retención, permisos, catálogos, cálculo de cuotas/caja, regularidad, ni respuestas exitosas de negocio. Se extiende sólo el contrato de rechazo operacional HTTP. El modelo y clasificación existentes se preservan; no se acredita schema instalado.

Pruebas: HTTP con autenticación sintética para rechazos, concurrencia, aislamiento por sujeto/issuer, rotación de rutas, categorías, 401 y lecturas; suite real del producto/smokes en CI; auditoría de dependencias con casos alta/crítica directas y transitivas, y fallos de fuente incompleta. Ver SHA/gates en recibo de PR y handoff. Main intacta; srv01/UAT pausados, despliegue QA pendiente.
