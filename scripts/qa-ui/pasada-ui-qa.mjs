#!/usr/bin/env node
/**
 * Pasada de interfaz del QA (PMGM-QA-004 §5–6), automatizada y de solo lectura.
 *
 * Uso (las credenciales SOLO por variables de entorno; nunca en archivos, Git ni chats):
 *   QA_BASE_URL=https://sistema.granlogiamixta.cl QA_USER=qa.xxx QA_PASSWORD=... \
 *   QA_LABEL="hermano sin cargo grado 1" node scripts/qa-ui/pasada-ui-qa.mjs
 *
 * Modo demostración (sin login; perfiles del selector de la demo / pantalla #acceso):
 *   QA_MODE=demo QA_BASE_URL=http://127.0.0.1:4174/Modernizaci-n-GLMC-Milenio/ node scripts/qa-ui/pasada-ui-qa.mjs
 *
 * Requiere `playwright` instalado fuera del repo (npm i playwright) y un Chromium
 * (CHROME_PATH opcional). Salida: QA_OUT (por defecto ./qa-ui-out) con informe.json e informe.md.
 * Capturas solo con QA_SCREENSHOTS=1 (pueden mostrar datos: usar solo cuentas y datos sintéticos).
 * No hace escrituras: solo navega y lee. No imprime ni guarda credenciales ni cookies.
 */
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const baseUrl = (process.env.QA_BASE_URL ?? '').replace(/\/?$/, '/')
const mode = process.env.QA_MODE === 'demo' ? 'demo' : 'real'
const label = process.env.QA_LABEL ?? (mode === 'demo' ? 'demo' : 'sin etiqueta')
const outDir = process.env.QA_OUT ?? 'qa-ui-out'
const shots = process.env.QA_SCREENSHOTS === '1'
const widths = [360, 390, 768, 1440]
if (!process.env.QA_BASE_URL) { console.error('Falta QA_BASE_URL'); process.exit(2) }
if (mode === 'real' && (!process.env.QA_USER || !process.env.QA_PASSWORD)) { console.error('Faltan QA_USER y QA_PASSWORD (variables de entorno)'); process.exit(2) }
if (mode === 'real' && !baseUrl.startsWith('https://') && !/^http:\/\/(127\.0\.0\.1|localhost)/.test(baseUrl)) { console.error('El QA real exige HTTPS'); process.exit(2) }

const results = []
const add = (check, width, status, detail = '') => { results.push({ check, width, status, detail }); if (status !== 'pass') console.log(`${status.toUpperCase()} [${width ?? '-'}] ${check} ${detail}`) }
const slug = text => text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || undefined })
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'es-CL', timezoneId: 'America/Santiago' })
const page = await context.newPage()
const shot = async name => { if (shots) { await mkdir(outDir, { recursive: true }); await page.screenshot({ path: path.join(outDir, `${name}.png`), fullPage: true }) } }
const noOverflow = async (check, width) => {
  const over = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  add(check, width, over <= 1 ? 'pass' : 'fail', over > 1 ? `desborde horizontal de ${over}px` : '')
}

try {
  // 1. Pantalla de ingreso (QA-004 §5.1)
  const entry = mode === 'demo' ? `${baseUrl}#acceso` : baseUrl
  for (const width of widths) {
    await context.clearCookies()
    await page.setViewportSize({ width, height: 900 })
    await page.goto(entry, { waitUntil: 'domcontentloaded' })
    const hasLogin = await page.locator('.access-shell').waitFor({ timeout: 15000 }).then(() => true, () => false)
    if (!hasLogin) { add('Pantalla de ingreso visible', width, 'fail', 'no apareció .access-shell (¿sesión previa o ruta distinta?)'); continue }
    const info = await page.evaluate(() => ({
      h1: document.querySelector('.access-card h1')?.textContent?.trim(),
      buttons: document.querySelectorAll('.access-card button').length,
      buttonHeight: document.querySelector('.access-button')?.getBoundingClientRect().height ?? 0,
      help: document.querySelector('.access-help a')?.getAttribute('href') ?? '',
      legal: document.querySelector('.access-legal')?.textContent?.includes('Uso restringido') ?? false,
    }))
    add('Ingreso: título «Ingresar» y un solo botón', width, info.h1 === 'Ingresar' && info.buttons === 1 ? 'pass' : 'fail', JSON.stringify({ h1: info.h1, buttons: info.buttons }))
    add('Ingreso: botón de al menos 48 px', width, info.buttonHeight >= 48 ? 'pass' : 'fail', `${Math.round(info.buttonHeight)}px`)
    add('Ingreso: ayuda a soporte@granlogiamixta.cl', width, info.help.startsWith('mailto:soporte@granlogiamixta.cl') ? 'pass' : 'fail', info.help)
    add('Ingreso: texto de uso restringido', width, info.legal ? 'pass' : 'fail')
    await noOverflow('Ingreso: sin desborde horizontal', width)
    if (width === 390) await shot('ingreso-390')
  }

  // 2. Inicio de sesión (solo QA real)
  await page.setViewportSize({ width: 1440, height: 900 })
  if (mode === 'real') {
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' })
    await page.locator('.access-button:not([disabled])').click({ timeout: 20000 })
    try {
      await page.locator('#username').fill(process.env.QA_USER, { timeout: 20000 })
      await page.locator('#password').fill(process.env.QA_PASSWORD)
      await page.locator('#kc-login').click()
    } catch { add('Keycloak: formulario estándar (#username/#password/#kc-login)', 1440, 'fail', 'no se encontró el formulario; tema corporativo (#502) puede haber cambiado los selectores') }
    const entered = await page.locator('nav.sidebar').waitFor({ timeout: 30000 }).then(() => true, () => false)
    add('Ingreso con la cuenta de prueba llega a la aplicación', 1440, entered ? 'pass' : 'fail', entered ? '' : 'no apareció el menú tras iniciar sesión')
    if (!entered) throw new Error('sin sesión')
  } else {
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' })
    await page.locator('nav.sidebar').waitFor({ timeout: 20000 })
  }

  // 3. Versión visible, «Tu situación», menú y títulos (§4, §5.2, §5.3)
  const version = await page.evaluate(() => document.body.innerText.match(/UI QA v\d+\.\d+|API v[\w.-]+/)?.[0] ?? null)
  add('Versión visible en el encabezado', 1440, version ? 'pass' : 'warn', version ?? 'no se encontró «UI QA vX.XXX» / «API vX»')
  const controls = /^(Contraer menú|Expandir menú|A\s*(Normal|Grande|Muy grande))$/i
  const menu = (await page.evaluate(() => [...document.querySelectorAll('nav.sidebar button')].map(b => b.innerText.trim().split('\n')[0]).filter(Boolean))).filter(m => !controls.test(m.replace(/\s+/g, ' ')))
  add('Menú con opciones', 1440, menu.length > 0 ? 'pass' : 'fail', menu.join(' | '))
  const duplicated = menu.filter((m, i) => menu.indexOf(m) !== i)
  add('Menú sin opciones repetidas', 1440, duplicated.length === 0 ? 'pass' : 'fail', duplicated.join(', '))
  await page.locator('ul.home-identity').waitFor({ timeout: 5000 }).catch(() => undefined)
  const situation = await page.evaluate(() => { const list = document.querySelector('ul.home-identity[aria-label="Tu situación en la Orden"]'); return list ? [...list.querySelectorAll('li')].map(li => li.textContent.trim()).join(' · ') : null })
  add('Inicio: «Tu situación» (Taller · grado · cargo), sin «Autenticado»', 1440, situation && !/Autenticado|secretary|Master Of/i.test(situation) ? 'pass' : 'warn', situation ?? 'no aparece')

  for (const entryLabel of menu) {
    await page.setViewportSize({ width: 1440, height: 900 })
    await page.evaluate(text => { [...document.querySelectorAll('nav.sidebar button')].find(b => b.innerText.trim().startsWith(text))?.click() }, entryLabel)
    await page.waitForTimeout(900)
    const view = await page.evaluate(() => ({
      h1: document.querySelector('main h1')?.textContent?.trim() ?? '',
      smallText: [...document.querySelectorAll('main *')].filter(el => el.childElementCount === 0 && el.textContent.trim() && parseFloat(getComputedStyle(el).fontSize) < 13).length,
      forms: [...document.querySelectorAll('main form')].filter(f => !f.closest('[role="dialog"], .action-drawer, aside[aria-modal]')).length,
    }))
    const sameTitle = entryLabel === 'Inicio' || view.h1.toLowerCase().includes(entryLabel.toLowerCase()) || entryLabel.toLowerCase().includes(view.h1.toLowerCase())
    add(`Título igual al menú: «${entryLabel}»`, 1440, sameTitle ? 'pass' : 'warn', `h1=«${view.h1}»`)
    add(`Sin textos menores de 13 px: «${entryLabel}»`, 1440, view.smallText === 0 ? 'pass' : 'warn', `${view.smallText} elementos`)
    add(`Sin formularios abiertos fuera de panel: «${entryLabel}»`, 1440, view.forms === 0 ? 'pass' : 'warn', `${view.forms} formularios`)
    await noOverflow(`Sin desborde horizontal: «${entryLabel}»`, 1440)
    for (const width of [360, 768]) {
      await page.setViewportSize({ width, height: 900 })
      await page.waitForTimeout(300)
      await noOverflow(`Sin desborde horizontal: «${entryLabel}»`, width)
    }
    await shot(`vista-${slug(entryLabel)}`)
  }

  // 4. Agenda por grado: informa lo que se ve (§5.8); la regla se contrasta contra la cuenta usada
  await page.setViewportSize({ width: 1440, height: 900 })
  const hasAgenda = menu.some(m => m.startsWith('Agenda'))
  if (hasAgenda) {
    await page.evaluate(() => { [...document.querySelectorAll('nav.sidebar button')].find(b => b.innerText.trim().startsWith('Agenda'))?.click() })
    await page.waitForTimeout(1200)
    const agenda = await page.evaluate(() => ({
      scope: document.querySelector('.calendar-scope')?.textContent?.trim() ?? null,
      grades: [...document.querySelectorAll('.calendar-event-card .event-grade')].map(e => e.textContent.trim()),
    }))
    add('Agenda: línea «Mostrando: …»', 1440, agenda.scope ? 'pass' : 'warn', agenda.scope ?? 'no aparece (administradores sin Taller pueden no tenerla)')
    const counts = agenda.grades.reduce((a, g) => ({ ...a, [g]: (a[g] ?? 0) + 1 }), {})
    add('Agenda: grados de los eventos visibles (contrastar con la cuenta: Aprendiz 1°; Compañero 2° y 1°; Maestro 3°, 2° y 1°; cargo vigente todos)', 1440, 'pass', JSON.stringify(counts))
  }
} catch (error) {
  add('La pasada terminó sin errores', null, 'fail', String(error.message ?? error))
} finally {
  await browser.close()
}

const summary = results.reduce((a, r) => ({ ...a, [r.status]: (a[r.status] ?? 0) + 1 }), {})
await mkdir(outDir, { recursive: true })
const header = { cuenta: label, modo: mode, base: baseUrl, fecha: new Date().toISOString(), resumen: summary }
await writeFile(path.join(outDir, 'informe.json'), JSON.stringify({ ...header, resultados: results }, null, 2))
const rows = results.filter(r => r.status !== 'pass').map(r => `| ${r.status} | ${r.width ?? '-'} | ${r.check} | ${String(r.detail).replace(/\|/g, '/')} |`)
await writeFile(path.join(outDir, 'informe.md'), `# Pasada de interfaz QA — ${label}\n\n- Modo: ${mode} · Base: ${baseUrl}\n- Fecha: ${header.fecha}\n- Resumen: ${JSON.stringify(summary)}\n\n## Hallazgos (warn / fail)\n\n${rows.length ? '| Estado | Ancho | Prueba | Detalle |\n| --- | --- | --- | --- |\n' + rows.join('\n') : 'Sin hallazgos.'}\n\nVer PMGM-QA-004 §7 para reportar cada hallazgo (un Issue por hallazgo, con SHA, cuenta y ancho).\n`)
console.log(`Resumen: ${JSON.stringify(summary)} · informe en ${outDir}/`)
process.exit(summary.fail ? 1 : 0)
