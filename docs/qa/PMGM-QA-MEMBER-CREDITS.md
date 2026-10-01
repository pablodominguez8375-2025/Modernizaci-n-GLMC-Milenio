# Consulta de crédito propio en Mi ficha

Issue #254, remanente acotado de #58/#36/#191. La API productiva ya entrega `treasuryAccount.unappliedCredits`: importe original más ajustes de caja menos imputaciones netas. Esta intervención conserva esa fórmula y muestra el contrato vigente.

| Caso | Resultado requerido |
|---|---|
| Recibo parcialmente imputado y corrección que libera parte | Disponible actualizado por recibo; deuda permanece sin compensación automática |
| Anulación confirmada | El recibo no aparece como crédito disponible |
| Recibo totalmente imputado | No aparece como crédito pendiente |
| Crédito sin cargos de esa moneda, incluso de Taller anterior | Se muestra por su moneda original; no se pierde por ausencia de cargos |
| CLP y USD | Filas y montos separados, sin conversión ni total combinado |
| Usuario suministra otro MemberId en query | La identidad vinculada sigue determinando la ficha; no aparecen recibos de otro Hermano |
| Privacidad | Respuesta propia `private, no-store`; sin identificador documental. Vista no muestra IDs internos ni referencia bancaria |
| Cuenta sin crédito/campo opcional ausente | Mensaje de estado vacío, sin error |
| Demo | Dos créditos ficticios CLP/USD sobre contrato real; deuda y cuotas pagadas existentes intactas |

Pruebas: `MemberSelfTreasuryCreditsPostgreSqlTests` (dos casos HTTP CLP/USD con corrección y anulación reales, requiere PMGM_TEST_POSTGRES); `MemberTreasuryCredits.test.tsx` (render real, estado vacío/compatibilidad, demo y privacidad); contratos existentes de `membershipApi.self`/`MemberPortalPage`. Una ejecución sin PostgreSQL no acredita esta cobertura backend. Ver TRX y logs CI del SHA correspondiente.

No se decide devolución real ni nueva tarifa, no se crea ledger alternativo, no se cambia backend productivo/migración. #58 mantiene cargos genéricos/vencimientos, comprobante documental/foto y demás remanentes; #191 presentación financiera institucional y aceptación operacional. La nueva consulta de crédito no acredita aceptación integral de esos issues.

Verificación visual requerida en demo: abrir Mi ficha → Ver cartola y comprobar dos filas de crédito, encabezados legibles y desplazamiento contenido en la tabla, en móvil/escritorio. Capturas automatizadas no equivalen a UAT. `main` congelado; **srv01 pausado, despliegue QA pendiente**, sin instalación, QA física ni UAT.
