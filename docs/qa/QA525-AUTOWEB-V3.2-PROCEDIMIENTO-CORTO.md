# QA525 — Actualizador autónomo V3.2: procedimiento corto

Estado: **DISEÑO/IMPLEMENTACIÓN INACTIVA**. No instala, habilita ni ejecuta nada en `glmdch`.

## Objetivo operativo

Después del bootstrap supervisado inicial, una actualización WEB elegible no debe pedir comandos, GO ni confirmaciones al Sponsor. El Sponsor interviene únicamente para:

1. firmar o renovar una política de hasta 7 días y 5 secuencias;
2. revocarla o resolver un bloqueo crítico.

Las migraciones, API, Compose, identidad, secretos y cambios no clasificados siguen por el carril manual. Reducir pasos no significa omitir firma, rollback o salud: significa ejecutarlos una sola vez, en el orden correcto y sin trasladarlos a una persona.

## Flujo normal automático

1. **CI produce un candidato.** Tras los gates exactos de `dev`, clasifica el diff. Si no es `auto-web`, termina `MANUAL_NO_ACTION`. Si lo es, construye una sola imagen WEB, genera SBOM/sobre, fija digests y la atesta.
2. **El host prepara sin gastar.** El timer descarga a caché privada, verifica firma, política, secuencia y SHA, carga la imagen y coteja su forma/capas. Un fallo aquí es `NO_OP` o `REJECT`; no consume nonce ni cupo y no requiere al Sponsor.
3. **Un preflight consolidado bajo lock.** Toma una sola instantánea del journal y del runtime, adquiere los cinco locks + lease, verifica que API/Compose/identidad/Silo no derivaron y persiste el intent. Solo aquí se consume la secuencia.
4. **Cambio WEB y decisión automática.** Recrea únicamente WEB por digest, mide WEB/OIDC/versión y:
   - PASS: deja recibo `ACTIVE` y termina;
   - FAIL: ejecuta un único rollback al baseline y lo verifica;
   - resultado ambiguo o rollback no restaurado: `BLOCKED critical=true`, sin reintento.
5. **Recibo único.** Conserva corte anterior/actual, intento, salud, secuencia y próxima acción. GitHub/Drive reciben una copia asíncrona; una falla de reporte no revierte ni repite el cambio.

## Validaciones consolidadas

| Se valida | Frecuencia | No repetir |
|---|---:|---|
| Identidad del workflow, clasificador, bases OCI y build args | al cargar/renovar política | por cada subpaso |
| Firma y digest del sobre/asset | una vez por candidato | antes y después de cada lectura |
| Secuencia, nonce, residuos y estado instalado | una instantánea bajo lock | inventarios manuales separados |
| Imagen cargada y capas | una vez antes de gastar | reconstrucción en el host |
| Locks/lease | justo antes y después del único efecto | ceremonias por comando |
| WEB/OIDC/versión | después de apply y, si corresponde, rollback | toda la matriz CI en el host |

Se eliminan como pasos humanos: copiar SHA/digests, descargar ZIP, ejecutar preflight, instalar GO por release, comprobar health a mano, escribir adendas PASS y decidir el rollback. Los controles se mantienen dentro del software y producen códigos fijos.

## Caché segura

- La política verificada fija invariantes estables y se carga una vez por generación.
- Los assets son inmutables y se identifican por digest; una descarga válida no se repite.
- Un `NO_OP` anterior al intent puede reintentarse en el siguiente tick con el mismo asset.
- Después de gastar la secuencia no hay reintento del mismo release, aunque el proceso se interrumpa.

## Intervención manual mínima

| Situación | Acción humana |
|---|---|
| Release elegible y PASS | ninguna |
| Release manual/no elegible | usar el carril manual cuando corresponda |
| Sin red, CI incompleto o candidato aún no vigente | ninguna; próximo tick |
| Política vencida o cupos agotados | firmar una política nueva |
| Rollback verificado al baseline | revisar el defecto antes de una política/release posterior; no repetir el candidato |
| Rollback fallido, lease perdido durante el efecto o estado indeterminado | intervención técnica; host permanece bloqueado |

## Meta de tiempo y pasos

- Sponsor por release elegible: **0 pasos**.
- Sponsor por ventana: **1 firma de política**; revocación solo si es necesaria.
- Tiempo automático objetivo después de CI: descarga/carga + un apply + health, normalmente **5–15 minutos**, sin espera de chat.
- Ruta crítica del host: **un preflight, un efecto WEB, una verificación**; rollback solo ante fallo.

## Gates antes de activar

Contrato, política, journal/terminal, runner WEB, orquestador, recuperación por crash y timer deben quedar 0 P0/P1; F2 sombra debe demostrar que no gasta nada; F3 supervisada debe probar apply y rollback reales. La unidad y el timer nacen apagados. `main`, producción, API, BD e identidad quedan fuera.
