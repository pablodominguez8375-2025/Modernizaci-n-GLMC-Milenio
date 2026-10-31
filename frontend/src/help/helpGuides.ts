/* Centro de ayuda (etapa 1) · guías paso a paso en lenguaje simple.
   Cada guía solo se muestra a quien puede hacer lo que explica (`needs`). Sin datos personales ni de ningún Taller.
   Los nombres de menús y botones son los que se ven en pantalla. */

export type HelpAccessKey =
  | 'everyone' | 'member' | 'calendar' | 'notifications' | 'lodgeSecretariat' | 'grandSecretariat'
  | 'systemAdmin' | 'supportView' | 'library' | 'regimen'

export type HelpAccess = Record<HelpAccessKey, boolean>

export type HelpTarget =
  | 'dashboard' | 'memberPortal' | 'memberPayments' | 'memberAttendance' | 'calendar' | 'notifications'
  | 'privacy' | 'lodge' | 'secretariat' | 'system' | 'library' | 'regimen' | 'candidates'

export type HelpTopic = 'Primeros pasos' | 'Mi espacio' | 'Mi Taller' | 'Gran Logia' | 'Sistema'

export type HelpGuide = {
  id: string
  topic: HelpTopic
  title: string
  summary: string
  needs: HelpAccessKey
  steps: string[]
  tip?: string
  target?: { view: HelpTarget; label: string }
  keywords?: string
}

export const helpTopics: HelpTopic[] = ['Primeros pasos', 'Mi espacio', 'Mi Taller', 'Gran Logia', 'Sistema']

export const helpGuides: HelpGuide[] = [
  {
    id: 'buscar', topic: 'Primeros pasos', needs: 'everyone',
    title: 'Encontrar una pantalla con el buscador',
    summary: 'Si no sabes en qué menú está algo, escríbelo y el sistema te lleva.',
    steps: [
      'Arriba, en la barra azul, haz clic en el campo «Buscar módulo o hermano».',
      'Escribe una o dos palabras, por ejemplo «pagos», «agenda» o «templo».',
      'Elige el resultado de la lista. La pantalla se abre sola.',
    ],
    tip: 'Solo aparecen las pantallas que tu cargo puede abrir.',
    keywords: 'buscar lupa encontrar donde esta menu',
  },
  {
    id: 'letra', topic: 'Primeros pasos', needs: 'everyone',
    title: 'Hacer más grande la letra',
    summary: 'Cambia el tamaño del texto para leer con comodidad.',
    steps: [
      'En la barra azul de arriba busca el botón «Aa» («Tamaño de letra»).',
      'Haz clic y elige el tamaño que te acomode.',
      'El cambio se aplica de inmediato en todo el sistema.',
    ],
    keywords: 'letra tamano texto grande leer vista aa',
  },
  {
    id: 'celular', topic: 'Primeros pasos', needs: 'everyone',
    title: 'Usar el sistema desde el celular',
    summary: 'Abre el mismo sistema en el navegador del teléfono.',
    steps: [
      'Abre el navegador del celular e ingresa a la dirección del sistema que te entregó tu Taller.',
      'Ingresa con tu cuenta personal, igual que en el computador.',
      'Abajo verás atajos a las pantallas más usadas. El botón de menú abre el resto de las opciones.',
    ],
    keywords: 'celular movil telefono tablet',
  },
  {
    id: 'inicio', topic: 'Primeros pasos', needs: 'everyone',
    title: 'Volver a Inicio',
    summary: 'La pantalla de resumen, a un clic desde cualquier lugar.',
    steps: [
      'Haz clic en «Inicio», al comienzo del menú, o en el nombre «Proyecto Centenario» de la barra azul.',
      'Revisa las tarjetas: cada una resume un tema y se abre con un clic.',
    ],
    target: { view: 'dashboard', label: 'Ir a Inicio' },
    keywords: 'inicio resumen pendientes tareas volver',
  },
  {
    id: 'ficha-foto', topic: 'Mi espacio', needs: 'member',
    title: 'Ver mi ficha y cambiar mi foto',
    summary: 'Revisa tus datos y sube o quita tu foto de perfil.',
    steps: [
      'En el menú, dentro de «Mi espacio», abre «Mi ficha».',
      'Revisa tus datos. Tu RUT se ve completo solo aquí.',
      'Para la foto, pulsa «Subir foto» (o «Cambiar foto» si ya tienes una) y elige una imagen JPG o PNG.',
      'Si ya no quieres foto, pulsa «Quitar foto»: se muestra un círculo con tus iniciales.',
    ],
    tip: 'Si un dato está mal, pide la corrección a la Secretaría de tu Taller.',
    target: { view: 'memberPortal', label: 'Abrir Mi ficha' },
    keywords: 'ficha foto perfil datos rut cambiar quitar',
  },
  {
    id: 'pagos', topic: 'Mi espacio', needs: 'member',
    title: 'Ver mis pagos y mi cartola',
    summary: 'Consulta tus cuotas, lo que adeudas y los comprobantes.',
    steps: [
      'En «Mi espacio», abre «Mis pagos».',
      'Verás separadas la Tesorería y la Hospitalaria.',
      'Pulsa «Ver mi cartola completa» para ver todas tus cuotas mes a mes.',
      'En Hospitalaria, pulsa «Ver detalle y decretos» para ver cada reposición con su decreto.',
    ],
    tip: 'Aquí solo consultas. Los pagos los valida el Tesorero o el Hospitalario.',
    target: { view: 'memberPayments', label: 'Abrir Mis pagos' },
    keywords: 'pagos cuotas cartola deuda saldo tesoreria hospitalaria comprobante',
  },
  {
    id: 'asistencias', topic: 'Mi espacio', needs: 'member',
    title: 'Revisar mis asistencias',
    summary: 'Mira a qué tenidas e instrucciones asististe.',
    steps: [
      'En «Mi espacio», abre «Mis asistencias».',
      'Revisa la lista por fecha.',
    ],
    target: { view: 'memberAttendance', label: 'Abrir Mis asistencias' },
    keywords: 'asistencia tenidas instruccion justificada',
  },
  {
    id: 'agenda', topic: 'Mi espacio', needs: 'calendar',
    title: 'Mirar la Agenda',
    summary: 'Las tenidas, instrucciones y actividades que te corresponden.',
    steps: [
      'Abre «Agenda» desde el menú o desde el atajo de abajo en el celular.',
      'Verás lo de tu grado y los grados inferiores, más lo que es general.',
      'Haz clic en una actividad para ver el detalle.',
    ],
    target: { view: 'calendar', label: 'Abrir la Agenda' },
    keywords: 'agenda calendario tenida evento instruccion grado',
  },
  {
    id: 'avisos', topic: 'Mi espacio', needs: 'notifications',
    title: 'Leer mis avisos',
    summary: 'Los mensajes del sistema para ti, como las respuestas a tus solicitudes.',
    steps: [
      'Haz clic en la campana de la barra azul, o en «Avisos» del menú.',
      'La campana muestra cuántos avisos tienes sin leer.',
      'Ábrelo para leer el detalle.',
    ],
    target: { view: 'notifications', label: 'Abrir Avisos' },
    keywords: 'avisos campana notificaciones alertas',
  },
  {
    id: 'privacidad', topic: 'Mi espacio', needs: 'everyone',
    title: 'Mis datos y mis derechos (Ley 21.719)',
    summary: 'Qué datos guardamos, quién puede verlos y qué puedes pedir.',
    steps: [
      'En «Mi espacio», abre «Privacidad y mis datos».',
      'Lee cada tarjeta: qué guardamos, para qué, quién puede verlo y cómo lo cuidamos.',
      'Más abajo están tus derechos y el correo de contacto para pedir una copia, una corrección o la eliminación de un dato.',
    ],
    target: { view: 'privacy', label: 'Abrir Privacidad y mis datos' },
    keywords: 'privacidad datos personales derechos ley 21719 copia eliminar acceso de soporte',
  },
  {
    id: 'biblioteca', topic: 'Mi espacio', needs: 'library',
    title: 'Leer en la Biblioteca Virtual',
    summary: 'Libros, planchas, revistas y boletines según tu grado.',
    steps: [
      'Abre «Biblioteca» en el menú.',
      'Elige una categoría.',
      'Abre el documento para leerlo.',
    ],
    target: { view: 'library', label: 'Abrir la Biblioteca' },
    keywords: 'biblioteca libros planchas revistas boletines leer',
  },
  {
    id: 'pedir-espacio', topic: 'Mi Taller', needs: 'lodgeSecretariat',
    title: 'Pedir un templo o una sala a la Gran Secretaría',
    summary: 'Solo la Secretaría del Taller hace la solicitud. No se necesita la firma del Venerable Maestro.',
    steps: [
      'Abre «Secretaría del Taller» en el menú.',
      'En «Solicitudes de templo o sala», pulsa «Solicitar templo o sala».',
      'Indica el día y la hora, y para qué lo necesitas (tenida, docencia, ceremonia u otro).',
      'Confirma. La solicitud queda «En revisión» y se envía a la Gran Secretaría.',
      'Cuando responda, te llega un aviso. La reserva solo queda confirmada si dice «Aprobada».',
    ],
    tip: 'Mientras esté en revisión puedes pulsar «Cancelar solicitud». Si la rechazan, verás el motivo.',
    target: { view: 'lodge', label: 'Abrir Secretaría del Taller' },
    keywords: 'templo sala solicitar pedir reserva espacio secretaria taller',
  },
  {
    id: 'imprimir-ficha-insinuado', topic: 'Mi Taller', needs: 'lodgeSecretariat',
    title: 'Imprimir la ficha de un insinuado',
    summary: 'La Secretaría del Taller imprime la ficha del candidato en una hoja, con su foto y su firma.',
    steps: [
      'Abre «Insinuaciones e Iniciación» en el menú y entra a «Carga».',
      'En «Solicitudes de iniciación», elige al candidato.',
      'Revisa que la ficha esté guardada. Si hiciste cambios, pulsa «Guardar cambios y reenviar» primero: se imprime lo guardado.',
      'Arriba a la derecha, pulsa «Imprimir ficha».',
      'En la ventana de impresión elige tu impresora o «Guardar como PDF», y confirma.',
      'Firma la hoja en «Firma Secretaría del Taller».',
    ],
    tip: 'El botón se activa solo cuando la ficha está guardada. La hoja no incluye las observaciones internas ni el resumen de entrevista, y debe quedar bajo custodia de la Orden.',
    target: { view: 'candidates', label: 'Abrir Insinuaciones e Iniciación' },
    keywords: 'imprimir ficha insinuado candidato postulante hoja papel pdf secretaria taller',
  },
  {
    id: 'historia-hermano-secretaria', topic: 'Mi Taller', needs: 'lodgeSecretariat',
    title: 'Completar o corregir la historia de un hermano',
    summary: 'La Secretaría del Taller propone las fechas de iniciación, aumento de salario y exaltación y los cargos pasados de un hermano. Régimen Interior los aprueba.',
    steps: [
      'Abre «Fichas de miembros» en el menú y elige al hermano.',
      'Baja hasta «Historia del hermano» y pulsa «Completar o corregir historia».',
      'En «Fechas masónicas», escribe o corrige la fecha de iniciación, de aumento de salario o de exaltación.',
      'En «Cargos en este Taller», corrige un cargo ya registrado o pulsa «+ Agregar cargo» para sumar uno pasado, con su período y fechas.',
      'Anota la fuente o respaldo (acta, plancha o certificado) y el motivo del cambio.',
      'Pulsa «Enviar a Régimen Interior». La ficha cambia solo cuando lo aprueban.',
    ],
    tip: 'Si Régimen Interior observa o rechaza la propuesta, verás su motivo en la misma sección y podrás enviar una nueva. Mientras haya una propuesta pendiente no se puede enviar otra del mismo hermano.',
    keywords: 'historia hermano cargos pasados fecha iniciación aumento salario exaltación corregir completar ficha secretaria taller',
  },
  {
    id: 'historia-hermano-regimen', topic: 'Gran Logia', needs: 'regimen',
    title: 'Aprobar cambios en la historia de un hermano',
    summary: 'Régimen Interior revisa lo que propone la Secretaría del Taller sobre cargos y fechas masónicas y lo aprueba, observa o rechaza.',
    steps: [
      'Abre «Informes» y entra a «Control de miembros».',
      'En «Cambios de cargos y fechas por aprobar», lee cada propuesta: verás el valor anterior tachado y el nuevo, con el respaldo y el motivo.',
      'Si todo está correcto, pulsa «Aprobar y actualizar ficha» y confirma.',
      'Si algo no calza, pulsa «Observar o rechazar», escribe el motivo y elige «Observar» o «Rechazar».',
    ],
    tip: 'Nada cambia en la ficha del hermano hasta que apruebes. El motivo de una observación o rechazo es obligatorio y le llega a la Secretaría del Taller.',
    keywords: 'historia hermano aprobar observar rechazar cargos fechas iniciación exaltación régimen interior control de miembros',
  },
  {
    id: 'responder-espacio', topic: 'Gran Logia', needs: 'grandSecretariat',
    title: 'Aprobar o rechazar una solicitud de templo o sala',
    summary: 'Responde las peticiones de los Talleres desde «Templos y salas».',
    steps: [
      'Abre «Gran Secretaría» y entra a «Templos y salas».',
      'En «Solicitudes de espacio» verás las peticiones de los Talleres.',
      'Para aceptar, pulsa «Aprobar», elige el espacio y confirma con «Aprobar y reservar». Si hay choque de horario, el sistema te lo avisa.',
      'Para negar, pulsa «Rechazar», escribe el motivo y confirma con «Rechazar solicitud».',
      'El Taller recibe un aviso con tu respuesta.',
    ],
    target: { view: 'secretariat', label: 'Abrir Gran Secretaría' },
    keywords: 'aprobar rechazar solicitud templo sala gran secretaria reserva',
  },
  {
    id: 'padron', topic: 'Gran Logia', needs: 'regimen',
    title: 'Revisar el Padrón de la Gran Asamblea',
    summary: 'Qué falta para cerrar el padrón y dónde ver los listados.',
    steps: [
      'Abre «Régimen Interior» en el menú.',
      'Entra a «Informe y revisiones» para ver qué falta para cerrar el padrón.',
      'Entra a «Padrón de la Gran Asamblea» para ver los listados de asambleístas y de quienes pueden votar.',
    ],
    tip: 'Los saldos los registran Gran Tesorería y Gran Hospitalaria; Régimen Interior los evalúa y cierra el padrón.',
    target: { view: 'regimen', label: 'Abrir Régimen Interior' },
    keywords: 'regimen interior padron asamblea cerrar informe asambleistas votar saldo bloqueo',
  },
  {
    id: 'restablecer-acceso', topic: 'Sistema', needs: 'systemAdmin',
    title: 'Ayudar a alguien que no puede ingresar',
    summary: 'Restablecer el acceso de un usuario, sin conocer su clave.',
    steps: [
      'Abre «Parámetros del sistema» y entra a la sección de usuarios.',
      'Busca a la persona y pulsa «Restablecer acceso».',
      'Elige «Enviar enlace de acceso» (llega a su correo y sirve una vez) o «Generar clave temporal» (se muestra una sola vez).',
      'Confirma. Si usaste la clave temporal, cópiala y entrégala de forma segura.',
    ],
    tip: 'Nadie puede ver la clave de otra persona.',
    target: { view: 'system', label: 'Abrir Parámetros del sistema' },
    keywords: 'restablecer acceso clave olvide no puede ingresar usuario enlace temporal',
  },
  {
    id: 'ver-como', topic: 'Sistema', needs: 'supportView',
    title: 'Ver el sistema como un cargo (por perfil)',
    summary: 'Para revisar lo que ve un cargo. Solo lectura, 30 minutos, no pide motivo.',
    steps: [
      'En la barra azul de arriba pulsa «Ver como».',
      'Elige un perfil de la lista: «Perfiles de Taller» (por ejemplo Venerable Maestro o Secretaría) o «Perfiles de Gran Logia».',
      'Navega por las pantallas: solo puedes mirar, no cambiar nada. Una franja fija arriba muestra a quién ves y cuánto tiempo queda.',
      'Para terminar, pulsa «Salir de “Ver como”» en la franja, o «Volver a mi perfil» en el mismo selector. A los 30 minutos se cierra sola.',
    ],
    tip: 'Los perfiles de Taller usan el Taller 00 de soporte, sin datos de Hermanos reales. Por ahora no se pueden hacer descargas desde este modo.',
    keywords: 'ver como perfil soporte mirar cargo solo lectura master superusuario selector',
  },
  {
    id: 'ver-como-hermano', topic: 'Sistema', needs: 'systemAdmin',
    title: 'Ver el sistema como un Hermano concreto (soporte remoto)',
    summary: 'Para ayudar a una persona que no ve algo. Pide motivo y queda registrado.',
    steps: [
      'Abre «Parámetros del sistema» y entra a «Usuarios».',
      'En «Usuarios configurados» busca al Hermano. El botón solo aparece en cuentas de Hermanos.',
      'Pulsa «Ver como» en su fila.',
      'Escribe el motivo (de 10 a 500 caracteres), por ejemplo «El Hermano no ve su Taller en el menú», y pulsa «Ver como este Hermano».',
      'Verás la plataforma como la ve él, solo para mirar: no verás su RUT, contacto ni pagos, y no conocerás su clave. Para terminar usa «Salir de “Ver como”».',
    ],
    tip: 'Dura 30 minutos como máximo. El Hermano lo ve después en «Privacidad y mis datos», en la tarjeta «Acceso de soporte».',
    target: { view: 'system', label: 'Abrir Parámetros del sistema' },
    keywords: 'ver como hermano persona soporte remoto ayudar no ve menu motivo usuarios',
  },
  {
    id: 'corregir-contacto-soporte', topic: 'Sistema', needs: 'supportView',
    title: 'Corregir un dato de contacto o reenviar un aviso (soporte)',
    summary: 'Solo con el permiso vigente que da el Máster. Cada acción lleva motivo y queda registrada.',
    steps: [
      'Pide al Máster el «Permiso para corregir datos» (tiene vigencia; cuando vence deja de funcionar).',
      'Abre «Parámetros del sistema» → «Usuarios» y busca al Hermano.',
      'Para corregir, pulsa «Corregir contacto», elige el dato (correo, teléfono o domicilio), escribe el dato correcto y el motivo, y confirma.',
      'Para reenviar, pulsa «Reenviar un aviso», elige el aviso ya emitido y escribe el motivo. Sale al mismo correo registrado.',
    ],
    tip: 'No alcanza a Tesorería, Hospitalaria, firmas ni claves. El Hermano ve qué se corrigió en «Privacidad y mis datos».',
    target: { view: 'system', label: 'Abrir Parámetros del sistema' },
    keywords: 'corregir contacto correo telefono domicilio reenviar aviso soporte superusuario permiso',
  },
  {
    id: 'permiso-correccion', topic: 'Sistema', needs: 'systemAdmin',
    title: 'Dar a un Superusuario permiso para corregir datos',
    summary: 'Solo el Máster. El permiso es por un tiempo y se puede retirar.',
    steps: [
      'Abre «Parámetros del sistema» → «Usuarios» y busca la cuenta del Superusuario de soporte.',
      'Pulsa «Permiso para corregir datos».',
      'Elige la vigencia y escribe el motivo (de 10 a 500 caracteres). Confirma con «Dar permiso para corregir».',
      'Para retirarlo antes de que venza, pulsa «Retirar permiso» en la línea que aparece bajo su nombre.',
    ],
    tip: 'Cada corrección queda a nombre del Superusuario, con su motivo.',
    target: { view: 'system', label: 'Abrir Parámetros del sistema' },
    keywords: 'permiso corregir datos superusuario master vigencia retirar soporte',
  },
]

function normalize(value: string): string {
  return value.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim()
}

/** Solo las guías que la persona puede usar con su cargo. */
export function visibleGuides(access: HelpAccess, guides: HelpGuide[] = helpGuides): HelpGuide[] {
  return guides.filter(guide => guide.needs === 'everyone' || access[guide.needs])
}

/** Búsqueda simple sin tildes ni mayúsculas: todas las palabras deben aparecer. */
export function searchGuides(guides: HelpGuide[], query: string): HelpGuide[] {
  const words = normalize(query).split(/\s+/).filter(Boolean)
  if (words.length === 0) return guides
  return guides.filter(guide => {
    const haystack = normalize(`${guide.title} ${guide.summary} ${guide.keywords ?? ''} ${guide.steps.join(' ')}`)
    return words.every(word => haystack.includes(word))
  })
}
