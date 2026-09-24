#!/usr/bin/env node

import { spawn } from 'node:child_process'
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'

const [browser, baseUrl, outputDir] = process.argv.slice(2)

if (!browser || !baseUrl || !outputDir) {
  console.error('Usage: capture-showcase-views.mjs <browser> <baseUrl> <outputDir>')
  process.exit(2)
}

const scenarios = [
  { slug: 'inicio', profile: 'brother', label: 'Inicio' },
  { slug: 'biblioteca', profile: 'brother', label: 'Biblioteca Virtual' },
  { slug: 'gestion-logial', profile: 'grandLodge', label: 'Gestión Logial' },
  {
    slug: 'tesoreria-taller',
    profile: 'lodgeTreasurer',
    label: 'Tesorería',
    requiredSidebar: ['Mi ficha', 'Mi calendario', 'Notificaciones', 'Insinuados publicados', 'Tesorería', 'Biblioteca Virtual'],
    forbiddenSidebar: ['Secretaría', 'Gestión Logial', 'Tenidas y actas', 'Carga de insinuados', 'Circuito de Iniciación', 'Fichas de miembros', 'Cuadro del Taller', 'Ficha de Taller', 'Retiros y traslados', 'Bandeja de pendientes', 'Gestor Documental'],
    requiredTabs: ['Resumen', 'Cuotas y Cobranzas', 'Ingresos y Egresos', 'Cuadro mensual', 'Configuraciones', 'Reportes'],
    treasuryCollection: true,
  },
  {
    slug: 'tesoreria-autorizacion-venerable',
    profile: 'lodge',
    label: 'Tesorería',
    requiredTabs: ['Egresos por autorizar'],
    forbiddenTabs: ['Resumen', 'Cuotas y Cobranzas', 'Cuadro mensual', 'Configuraciones', 'Reportes'],
  },
  { slug: 'gran-tesoreria', profile: 'grandLodge', label: 'Gran Tesorería', requiredTabs: ['Cuadros mensuales', 'Estado de Talleres', 'Tarifas y Orientes', 'Derechos ceremoniales'], grandTreasuryRights: true },
  { slug: 'gran-hospitalaria', profile: 'grandLodge', label: 'Gran Hospitalaria' },
  { slug: 'gran-secretaria', profile: 'grandLodge', label: 'Gran Secretaría' },
  { slug: 'gran-archivero', profile: 'grandLodge', label: 'Gran Archivero' },
]

const viewports = [
  { width: 360, height: 800, suffix: '360x800' },
  { width: 390, height: 844, suffix: '390x844' },
  { width: 768, height: 1024, suffix: '768x1024' },
  { width: 1440, height: 900, suffix: '1440x900' },
]

const debugPort = 9227
const userDataDir = await mkdtemp(path.join(tmpdir(), 'pmgm-chrome-'))
await mkdir(outputDir, { recursive: true })

const chrome = spawn(browser, [
  '--headless=new',
  '--disable-gpu',
  '--disable-dev-shm-usage',
  '--no-sandbox',
  '--hide-scrollbars',
  `--remote-debugging-port=${debugPort}`,
  '--remote-debugging-address=127.0.0.1',
  `--user-data-dir=${userDataDir}`,
  '--window-size=1440,900',
  baseUrl,
], { stdio: ['ignore', 'ignore', 'pipe'] })

let chromeError = ''
chrome.stderr.on('data', chunk => { chromeError += chunk.toString() })

const delay = ms => new Promise(resolve => setTimeout(resolve, ms))

async function waitForTarget() {
  for (let attempt = 1; attempt <= 40; attempt += 1) {
    try {
      const response = await fetch(`http://127.0.0.1:${debugPort}/json/list`)
      if (response.ok) {
        const targets = await response.json()
        const page = targets.find(target => target.type === 'page')
        if (page?.webSocketDebuggerUrl) return page
      }
    } catch {
      // Chrome is still starting.
    }
    await delay(250)
  }
  throw new Error(`Chrome DevTools did not become ready. ${chromeError.slice(-1200)}`)
}

const target = await waitForTarget()
const socket = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((resolve, reject) => {
  const timer = setTimeout(() => reject(new Error('Timed out connecting to Chrome DevTools.')), 5000)
  socket.addEventListener('open', () => { clearTimeout(timer); resolve() }, { once: true })
  socket.addEventListener('error', event => { clearTimeout(timer); reject(event.error ?? new Error('Chrome DevTools WebSocket error.')) }, { once: true })
})

let nextId = 1
const pending = new Map()
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data)
  if (!message.id) return
  const entry = pending.get(message.id)
  if (!entry) return
  pending.delete(message.id)
  if (message.error) entry.reject(new Error(message.error.message))
  else entry.resolve(message.result ?? {})
})

function cdp(method, params = {}) {
  const id = nextId++
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject })
    socket.send(JSON.stringify({ id, method, params }))
  })
}

async function evaluate(expression) {
  const result = await cdp('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true })
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text ?? 'Browser evaluation failed.')
  return result.result?.value
}

async function waitForExpression(expression, description, timeoutMs = 8000) {
  const started = Date.now()
  while (Date.now() - started < timeoutMs) {
    if (await evaluate(expression)) return
    await delay(120)
  }
  throw new Error(`Timed out waiting for ${description}.`)
}

async function resetPage(width, height) {
  await cdp('Emulation.setDeviceMetricsOverride', {
    width,
    height,
    deviceScaleFactor: 1,
    mobile: width <= 480,
    screenWidth: width,
    screenHeight: height,
  })
  await cdp('Page.navigate', { url: baseUrl })
  await waitForExpression("document.readyState === 'complete' && !!document.querySelector('nav.sidebar')", 'showcase shell')
  await waitForExpression("(() => { const logo = document.querySelector('.brand-logo'); const frame = logo?.closest('.brand-mark'); const brand = logo?.closest('.brand'); const meta = document.querySelector('.topbar-meta'); const motto = document.querySelector('.product-motto'); const topbar = document.querySelector('.topbar'); const noMobileOverflow = innerWidth > 480 || topbar?.scrollWidth <= topbar?.clientWidth; return logo?.complete && logo.naturalWidth > 0 && frame && getComputedStyle(frame).backgroundColor === 'rgb(255, 255, 255)' && getComputedStyle(logo).objectFit === 'contain' && brand && meta && brand.getBoundingClientRect().right <= meta.getBoundingClientRect().left && (getComputedStyle(motto).display === 'none' || brand.getBoundingClientRect().right <= motto.getBoundingClientRect().left) && noMobileOverflow; })()", 'official institutional logo and non-overlapping header')
  await delay(500)
}

async function selectProfile(profile) {
  if (profile === 'brother') return
  const changed = await evaluate(`(() => {
    const select = document.querySelector('select[aria-label="Seleccionar perfil de demostración"]');
    if (!select) return false;
    select.value = ${JSON.stringify(profile)};
    select.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  })()`)
  if (!changed) throw new Error('Demo profile selector was not found.')
  await waitForExpression(`document.querySelector('select[aria-label="Seleccionar perfil de demostración"]')?.value === ${JSON.stringify(profile)}`, `profile ${profile}`)
  await delay(350)
}

async function openModule(label) {
  const clicked = await evaluate(`(() => {
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    const button = [...document.querySelectorAll('nav.sidebar button')]
      .find(candidate => !candidate.disabled && normalize(candidate.textContent) === ${JSON.stringify(label)});
    if (!button) return false;
    button.click();
    return true;
  })()`)
  if (!clicked) throw new Error(`Navigation button not found or disabled: ${label}`)
  await waitForExpression(`(() => {
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    return [...document.querySelectorAll('nav.sidebar button.active')].some(button => normalize(button.textContent) === ${JSON.stringify(label)});
  })()`, `active module ${label}`)
  await delay(650)
}

async function assertNavigation(scenario) {
  const result = await evaluate(`(() => {
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    const labels = [...document.querySelectorAll('nav.sidebar button')].map(button => normalize(button.textContent));
    const tabs = [...document.querySelectorAll('nav.sidebar ~ main [role="tab"]')]
      .map(button => normalize(button.querySelector('span')?.textContent));
    const requiredSidebar = ${JSON.stringify(scenario.requiredSidebar ?? [])};
    const forbiddenSidebar = ${JSON.stringify(scenario.forbiddenSidebar ?? [])};
    const requiredTabs = ${JSON.stringify(scenario.requiredTabs ?? [])};
    const forbiddenTabs = ${JSON.stringify(scenario.forbiddenTabs ?? [])};
    return {
      missingSidebar: requiredSidebar.filter(label => !labels.includes(label)),
      forbiddenSidebar: forbiddenSidebar.filter(label => labels.includes(label)),
      missingTabs: requiredTabs.filter(label => !tabs.includes(label)),
      forbiddenTabs: forbiddenTabs.filter(label => tabs.includes(label)),
      labels,
      tabs,
    };
  })()`)
  if (!result) throw new Error(`Could not inspect role navigation for ${scenario.slug}.`)
  for (const [key, values] of Object.entries({
    missingSidebar: result.missingSidebar,
    forbiddenSidebar: result.forbiddenSidebar,
    missingTabs: result.missingTabs,
    forbiddenTabs: result.forbiddenTabs,
  })) {
    if (values.length) throw new Error(`${scenario.slug} navigation check failed (${key}: ${values.join(', ')}).`)
  }
}

async function openTreasuryCollection() {
  const clicked = await evaluate(`(() => {
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    const tab = [...document.querySelectorAll('nav.sidebar ~ main [role="tab"]')]
      .find(candidate => normalize(candidate.querySelector('span')?.textContent) === 'Cuotas y Cobranzas');
    if (!tab) return false;
    tab.click();
    return true;
  })()`)
  if (!clicked) throw new Error('Treasury tab not found: Cuotas y Cobranzas')
  await waitForExpression(`[...document.querySelectorAll('nav.sidebar ~ main [role="tab"]')].some(tab => tab.getAttribute('aria-selected') === 'true' && (tab.textContent || '').includes('Cuotas y Cobranzas'))`, 'active treasury collection tab')
  await waitForExpression(`!!document.querySelector('.treasury-collection-table .treasury-table')`, 'treasury collection table')
  await delay(300)
}

async function assertTreasuryPaymentAction(viewport) {
  const result = await evaluate(`(() => {
    const table = document.querySelector('.treasury-collection-table');
    const rows = [...(table?.querySelectorAll('tbody tr') || [])];
    const action = rows.flatMap(row => [...row.querySelectorAll('button')]).find(button => (button.textContent || '').trim() === 'Registrar pago');
    const page = document.documentElement;
    return {
      rows: rows.length,
      actionVisible: !!action && getComputedStyle(action).display !== 'none' && action.getBoundingClientRect().width > 0 && action.getBoundingClientRect().height > 0,
      touchSize: action ? action.getBoundingClientRect().height : 0,
      cardRight: rows[0]?.getBoundingClientRect().right ?? 0,
      visibleDataValues: rows[0] ? [...rows[0].querySelectorAll('.treasury-cell-value')].filter(value => {
        const style = getComputedStyle(value);
        const rect = value.getBoundingClientRect();
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.right <= innerWidth + 1 && (value.textContent || '').trim().length > 0;
      }).length : 0,
      visibleDataLabels: rows[0] ? [...rows[0].querySelectorAll('.treasury-mobile-label')].filter(label => {
        const style = getComputedStyle(label);
        const rect = label.getBoundingClientRect();
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.right <= innerWidth + 1 && (label.textContent || '').trim().length > 0;
      }).length : 0,
      tableHeaderVisible: (() => {
        const header = table?.querySelector('thead');
        const style = header ? getComputedStyle(header) : null;
        const rect = header?.getBoundingClientRect();
        return !!header && !!style && style.position !== 'absolute' && style.visibility !== 'hidden' && !!rect && rect.width > 1 && rect.height > 1;
      })(),
      viewportWidth: innerWidth,
      pageScrollWidth: page.scrollWidth,
      overflowElements: [...document.querySelectorAll('body *')]
        .map(element => { const rect = element.getBoundingClientRect(); return { tag: element.tagName, className: typeof element.className === 'string' ? element.className : '', right: Math.round(rect.right), width: Math.round(rect.width) }; })
        .filter(element => element.right > innerWidth + 2 && element.width > 0)
        .sort((left, right) => right.right - left.right)
        .slice(0, 8),
    };
  })()`)
  if (!result?.rows) throw new Error(`No synthetic treasury rows available at ${viewport}.`)
  if (!result.actionVisible) throw new Error(`Registrar pago is not visible in Treasury collection at ${viewport}.`)
  const compactLayout = result.viewportWidth <= 900
  const expectedLabels = compactLayout ? 6 : 0
  if (result.visibleDataValues !== 6 || result.visibleDataLabels !== expectedLabels || result.tableHeaderVisible === compactLayout) {
    throw new Error(`Treasury table layout is inconsistent at ${viewport}: values=${result.visibleDataValues}, labels=${result.visibleDataLabels}, header=${result.tableHeaderVisible}.`)
  }
  if (result.cardRight > result.viewportWidth + 1) throw new Error(`Treasury collection card is clipped at the right edge at ${viewport}: right=${result.cardRight}, width=${result.viewportWidth}.`)
  if (result.touchSize < 44) throw new Error(`Registrar pago is below 44px touch height at ${viewport}: ${result.touchSize}px.`)
  if (result.pageScrollWidth > result.viewportWidth + 1) throw new Error(`Treasury collection causes global horizontal overflow at ${viewport}: ${JSON.stringify(result.overflowElements)}`)
}

async function openGrandTreasuryRights() {
  const clicked = await evaluate(`(() => {
    const tab = [...document.querySelectorAll('nav.sidebar ~ main [role="tab"]')]
      .find(candidate => (candidate.textContent || '').includes('Derechos ceremoniales'));
    if (!tab) return false;
    tab.click();
    return true;
  })()`)
  if (!clicked) throw new Error('Grand Treasury tab not found: Derechos ceremoniales')
  await waitForExpression(`!!document.querySelector('.ceremony-right-card')`, 'ceremony rights ledger')
  await delay(250)
}

async function assertCeremonyRightPaymentAction(viewport) {
  const result = await evaluate(`(() => {
    const card = document.querySelector('.ceremony-right-card');
    const details = card?.querySelector('details');
    const summary = details?.querySelector('summary');
    if (details && summary && !details.open) summary.click();
    const button = card?.querySelector('form button');
    const rect = card?.getBoundingClientRect();
    return { card: !!card, button: !!button, touchSize: button?.getBoundingClientRect().height ?? 0,
      cardRight: rect?.right ?? 0, viewportWidth: innerWidth, labels: card ? [...card.querySelectorAll('form label span')].map(x => (x.textContent || '').trim()) : [] };
  })()`)
  if (!result?.card || !result.button) throw new Error(`Ceremony right payment action is missing at ${viewport}.`)
  if (result.touchSize < 44) throw new Error(`Ceremony right payment action is below 44px at ${viewport}.`)
  if (result.cardRight > result.viewportWidth + 1) throw new Error(`Ceremony right card is clipped at ${viewport}.`)
  if (result.labels.length !== 4) throw new Error(`Ceremony right form is incomplete at ${viewport}: ${JSON.stringify(result.labels)}.`)
}

async function assertNoGlobalHorizontalOverflow(label, viewport) {
  const metrics = await evaluate(`(() => {
    const root = document.documentElement;
    const body = document.body;
    const scrollWidth = Math.max(root?.scrollWidth || 0, body?.scrollWidth || 0);
    return { scrollWidth, innerWidth: window.innerWidth };
  })()`)
  if (!metrics || metrics.scrollWidth > metrics.innerWidth + 1) {
    throw new Error(`Global horizontal overflow in ${label} at ${viewport}: scrollWidth=${metrics?.scrollWidth ?? 'unknown'}, innerWidth=${metrics?.innerWidth ?? 'unknown'}`)
  }
}

async function capture(filePath, scrollSelector = null) {
  await evaluate(`(() => {
    window.scrollTo(0, 0);
    document.documentElement.scrollLeft = 0;
    document.body.scrollLeft = 0;
    const content = document.querySelector('main.content');
    if (content) { content.scrollTop = 0; content.scrollLeft = 0; }
    const sidebar = document.querySelector('nav.sidebar');
    if (sidebar) { sidebar.scrollTop = 0; sidebar.scrollLeft = 0; }
    const target = ${JSON.stringify(scrollSelector)} ? document.querySelector(${JSON.stringify(scrollSelector)}) : null;
    if (target) {
      target.scrollIntoView({ block: 'start', inline: 'nearest' });
      const sidebarBottom = innerWidth <= 980
        ? (document.querySelector('nav.sidebar')?.getBoundingClientRect().bottom || 0)
        : 0;
      const stickyBottom = Math.max(
        document.querySelector('header.topbar')?.getBoundingClientRect().bottom || 0,
        sidebarBottom
      );
      window.scrollBy(0, -(stickyBottom + 12));
      if (${JSON.stringify(scrollSelector)} && target) {
        const action = target.querySelector('button');
        const actionRect = action?.getBoundingClientRect();
        if (!actionRect || actionRect.top < stickyBottom || actionRect.bottom > innerHeight || actionRect.right > innerWidth) {
          throw new Error('Treasury payment action is outside the visible viewport after positioning.');
        }
      }
    }
  })()`)
  await delay(120)
  const screenshot = await cdp('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false })
  await writeFile(filePath, Buffer.from(screenshot.data, 'base64'))
}

async function stopChromeAndClean() {
  if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) socket.close()

  if (chrome.exitCode === null) {
    chrome.kill('SIGTERM')
    await Promise.race([
      new Promise(resolve => chrome.once('exit', resolve)),
      delay(2500),
    ])
  }

  if (chrome.exitCode === null) {
    chrome.kill('SIGKILL')
    await Promise.race([
      new Promise(resolve => chrome.once('exit', resolve)),
      delay(1000),
    ])
  }

  try {
    await rm(userDataDir, { recursive: true, force: true, maxRetries: 8, retryDelay: 120 })
  } catch (error) {
    console.warn(`Temporary Chrome data could not be removed cleanly: ${error instanceof Error ? error.message : String(error)}`)
  }
}

try {
  await cdp('Page.enable')
  await cdp('Runtime.enable')

  for (const viewport of viewports) {
    for (const scenario of scenarios) {
      await resetPage(viewport.width, viewport.height)
      await selectProfile(scenario.profile)
      await openModule(scenario.label)
      await assertNavigation(scenario)
      if (scenario.treasuryCollection) {
        await openTreasuryCollection()
        await assertTreasuryPaymentAction(viewport.suffix)
      }
      if (scenario.grandTreasuryRights) {
        await openGrandTreasuryRights()
        await assertCeremonyRightPaymentAction(viewport.suffix)
      }
      if (viewport.width <= 480) await assertNoGlobalHorizontalOverflow(scenario.label, viewport.suffix)
      const viewSlug = scenario.treasuryCollection ? 'tesoreria-taller-cuotas' : scenario.slug
      const filePath = path.join(outputDir, `${viewSlug}-${viewport.suffix}.png`)
      const evidenceTarget = scenario.treasuryCollection
        ? '.treasury-collection-table tbody tr:has(button)'
        : scenario.grandTreasuryRights
          ? '.ceremony-right-card form'
          : null
      await capture(filePath, evidenceTarget)
      console.log(`captured ${path.basename(filePath)}`)
    }
  }
} finally {
  await stopChromeAndClean()
}
