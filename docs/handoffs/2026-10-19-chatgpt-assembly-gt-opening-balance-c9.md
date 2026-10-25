# #533 — C9 acreditar saldo histórico de apertura GT y evitar falso cero

Inicio 2026-10-19; base `dev@ed5c71538a4186aa1162a37ada68e78d017845cc`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegida. QA físico por el otro hilo.

Hallazgo: `AssemblyGrandTreasuryLedgerPolicy.Project` exigía meses consecutivos desde un `CoverageFrom` declarado, pero no exigía un saldo de apertura certificado a la víspera de esa cobertura. Una deuda anterior podía omitirse y producir ficticiamente saldo cero. Endurecer fail-closed para exigir fecha, saldo no negativo, referencia de fuente y certificación formal del saldo de apertura. La deuda acumulada = saldo apertura + diferencias no negativas de meses certificados, sin convertir monedas. Exigir `CoverageFrom` primer día del mes para evitar periodos parciales del mes de apertura.

Se mantiene API del record con campos opcionales al final (compatibilidad binaria de fuentes que construyen 8 argumentos), pero cualquier construcción antigua sin evidencia de apertura devuelve null y no permite voto. Pruebas actualizadas y nuevas: saldo cero certificado, apertura con deuda positiva, falta de certificado/referencia/fecha, cobertura parcial y montos inválidos. Sin integración de fuentes externas ni certificado institucional automático; sourceauthenticated/completenesscertified son hechos que SOLO puede suministrar backend con adaptador autenticado, nunca interfaz. Aun cuando `Project` devuelve balance, falta servicio de certificación oficial y revisión final RI antes de sufragio en producción.

PR draft hasta 3/3 gates exact HEAD. No tocar QA, main, srv01, credenciales ni datos reales.

## Reconciliación del 19-10
C8 PR #575 fue integrada a `dev@37419a5d48635ad2d1fa4c62d8d762dc05ca6568`, con 3/3 checks SUCCESS exact-head. C9 PR #577 se ha rebasado mediante CAS/lease de GitHub sobre dicho `dev`, sin archivos superpuestos. La nueva revisión deberá pasar PMGM CI/Showcase/Installable otra vez; no reutilizar checks del HEAD anterior. Esta corrección no genera por sí sola un certificado GT ni conecta fuentes oficiales; se mantiene pendiente el control de atestación bancaria/autorizaciones y etapa RI.
