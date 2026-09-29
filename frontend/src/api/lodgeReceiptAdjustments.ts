import type { LodgeMemberReceipt, LodgeReceiptAdjustment, LodgeReceiptAdjustmentRequest, LodgeTreasuryCharge } from './pmgmApi'

export function adjustMockReceipt(receipt:LodgeMemberReceipt, request:LodgeReceiptAdjustmentRequest, charges:Map<string,LodgeTreasuryCharge>, dateIsClosed:(date:string)=>boolean):LodgeReceiptAdjustment {
  const reason=request.reason.trim(), key=request.idempotencyKey.trim()
  const payload=JSON.stringify({...request,reason,idempotencyKey:key,allocations:[...request.allocations].sort((a,b)=>a.chargeId.localeCompare(b.chargeId)),allocationId:request.allocationId??null,amount:request.amount??null})
  const replay=receipt.adjustments?.find(a=>a.idempotencyKey===key)
  if(replay){if(replay.requestPayload!==payload)throw new Error('La clave ya fue utilizada con datos distintos.');return {...replay}}
  if(!['void','correction'].includes(request.kind)||!reason||reason.length>1000||!key||key.length>100||!/^\d{4}-\d{2}-\d{2}$/.test(request.effectiveDate)||request.effectiveDate<receipt.paymentDate||dateIsClosed(request.effectiveDate))throw new Error('El ajuste requiere fecha y motivo válidos en un ejercicio abierto.')
  if(receipt.adjustments?.some(a=>a.kind==='void'))throw new Error('El recibo ya está anulado.')
  if(receipt.allocations.some(a=>(a.effectiveDate??receipt.paymentDate)>request.effectiveDate)||receipt.adjustments?.some(a=>a.effectiveDate>request.effectiveDate))throw new Error('La fecha no puede preceder las imputaciones o ajustes existentes.')
  if(new Set(request.allocations.map(a=>a.chargeId)).size!==request.allocations.length||request.allocations.some(a=>a.amount<=0))throw new Error('Indique destinos válidos y sin repeticiones.')
  const adjustment:LodgeReceiptAdjustment={id:crypto.randomUUID(),kind:request.kind,effectiveDate:request.effectiveDate,reason,cashAmount:request.kind==='void'?-receipt.amount:0,recordedBySubject:'tesoreria-demo',recordedAtUtc:new Date().toISOString(),idempotencyKey:key,requestPayload:payload}
  const deltas:LodgeMemberReceipt['allocations']=[]
  const remaining=(a:LodgeMemberReceipt['allocations'][number])=>a.amount+receipt.allocations.filter(x=>x.reversesAllocationId===a.id).reduce((sum,x)=>sum+x.amount,0)
  const reverse=(a:LodgeMemberReceipt['allocations'][number],amount:number)=>deltas.push({...a,id:crypto.randomUUID(),amount:-amount,reversesAllocationId:a.id,adjustmentId:adjustment.id,effectiveDate:request.effectiveDate})
  if(request.kind==='void'){
    if(request.allocations.length||request.allocationId||request.amount!=null)throw new Error('La anulación del registro es total; no es una devolución.')
    receipt.allocations.filter(a=>a.amount>0).forEach(a=>{const amount=remaining(a);if(amount>0)reverse(a,amount)})
  }else{
    const source=receipt.allocations.find(a=>a.id===request.allocationId&&a.amount>0)
    if(!source||!request.amount||request.amount<=0||request.amount>remaining(source)||request.allocations.reduce((s,a)=>s+a.amount,0)>request.amount)throw new Error('La corrección supera la imputación disponible.')
    reverse(source,request.amount)
    for(const item of request.allocations){const target=charges.get(item.chargeId);if(!target||target.memberId!==receipt.memberId)throw new Error('El destino debe pertenecer al mismo hermano, Taller y moneda.');const balance=target.balance+(source.chargeId===target.id?request.amount:0);if(item.amount>balance)throw new Error('La corrección supera el saldo del destino.');const period=target.periods?.find(p=>p.chargeId===item.chargeId);if(period?.currency&&period.currency!==receipt.currency)throw new Error('La moneda del destino no coincide.');deltas.push({id:crypto.randomUUID(),...item,periodYear:period?.periodYear??0,periodMonth:period?.periodMonth??0,effectiveDate:request.effectiveDate,adjustmentId:adjustment.id})}
  }
  for(const delta of deltas){if(!charges.has(delta.chargeId))throw new Error('El cargo de origen no existe.')}
  for(const delta of deltas){const charge=charges.get(delta.chargeId)!;charge.payments.push({id:receipt.id,receiptNumber:receipt.receiptNumber,amount:delta.amount,paymentMethod:receipt.paymentMethod,paymentDate:request.effectiveDate,reference:reason});charge.paidAmount+=delta.amount;charge.balance-=delta.amount;charge.status=charge.balance===0?'paid':charge.paidAmount>0?'partial':'pending'}
  receipt.allocations.push(...deltas);(receipt.adjustments??=[]).push(adjustment)
  receipt.allocatedAmount=receipt.allocations.reduce((s,a)=>s+a.amount,0)
  receipt.unappliedBalance=receipt.amount+receipt.adjustments.reduce((s,a)=>s+a.cashAmount,0)-receipt.allocatedAmount
  return {...adjustment}
}
