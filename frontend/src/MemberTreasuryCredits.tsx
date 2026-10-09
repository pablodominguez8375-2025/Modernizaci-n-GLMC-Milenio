import type { MemberTreasuryCredit } from './api/membershipApi'

export default function MemberTreasuryCredits({ credits = [], onDownload, busy = false }: {
  credits?: MemberTreasuryCredit[]; onDownload?: (id: string) => void; busy?: boolean
}) {
  return <section aria-label="Crédito pendiente de imputación">
    <h3>Crédito pendiente de imputación</h3>
    <p>Importes recibidos todavía sin asignar a una cuota. Se muestran por moneda y no descuentan automáticamente el saldo de deuda.</p>
    {credits.length === 0 ? <p>Sin crédito pendiente de imputación.</p> :
      <div className="table-scroll"><table className="treasury-table">
        <thead><tr><th scope="col">Recibo</th><th scope="col">Fecha de recepción</th><th scope="col">Moneda</th><th scope="col">Disponible</th>{onDownload && <th scope="col">Comprobante</th>}</tr></thead>
        <tbody>{credits.map(credit => <tr key={credit.id}>
          <td>{credit.receiptNumber}</td>
          <td>{credit.paymentDate.split('-').reverse().join('/')}</td>
          <td>{credit.currency}</td>
          <td>{new Intl.NumberFormat('es-CL', { style: 'currency', currency: credit.currency, maximumFractionDigits: credit.currency === 'USD' ? 2 : 0 }).format(credit.amount)}</td>
          {onDownload && <td><button type="button" disabled={busy} onClick={() => onDownload(credit.id)} aria-label={`Descargar comprobante ${credit.receiptNumber}`}>Descargar</button></td>}
        </tr>)}</tbody>
      </table></div>}
  </section>
}
