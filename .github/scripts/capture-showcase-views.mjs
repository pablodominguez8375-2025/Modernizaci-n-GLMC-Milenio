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
  { slug: 'iniciacion-publicados-hermano', profile: 'brother', label: 'Insinuaciones e Iniciación', initiationTabs: [] },
  { slug: 'iniciacion-venerable', profile: 'lodge', label: 'Insinuaciones e Iniciación', initiationTabs: ['Publicados', 'Carga', 'Circuito de Iniciación'], sidebarCount: 11 },
  { slug: 'iniciacion-secretaria-taller', profile: 'lodgeSecretary', label: 'Insinuaciones e Iniciación', initiationTabs: ['Publicados', 'Carga', 'Circuito de Iniciación'] },
  { slug: 'iniciacion-gran-secretaria', profile: 'secretariat', label: 'Insinuaciones e Iniciación', initiationTabs: ['Publicados', 'Revisión', 'Circuito de Iniciación'] },
  { slug: 'iniciacion-regimen', profile: 'regimen', label: 'Insinuaciones e Iniciación', initiationTabs: ['Publicados', 'Circuito de Iniciación'] },
  { slug: 'afiliacion-incorporacion-tramitacion', profile: 'lodgeSecretary', label: 'Secretaría', admissions: true },
  { slug: 'biblioteca', profile: 'brother', label: 'Biblioteca Virtual' },
  { slug: 'gestion-logial', profile: 'grandLodge', label: 'Gestión Logial' },
  {
    slug: 'tesoreria-taller',
    profile: 'lodgeTreasurer',
    label: 'Tesorería',
    requiredSidebar: ['Mi ficha', 'Agenda', 'Avisos', 'Insinuaciones e Iniciación', 'Tesorería', 'Biblioteca Virtual'],
    forbiddenSidebar: ['Secretaría', 'Gestión Logial', 'Tenidas y actas', 'Insinuados publicados', 'Carga de insinuados', 'Revisión de insinuados', 'Circuito de Iniciación', 'Fichas de miembros', 'Cuadro del Taller', 'Ficha de Taller', 'Retiros y traslados', 'Bandeja de pendientes', 'Gestor Documental'],
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
  { slug: 'gran-archivero', profile: 'grandArchivist', label: 'Gran Archivero', requiredSidebar: ['Gran Archivero'], forbiddenSidebar: ['Parámetros del sistema', 'Configuración inicial', 'Gran Secretaría', 'Gran Tesorería', 'Gran Hospitalaria', 'Gestión Logial'] },
]

const viewports = [
  { width: 360, height: 800, suffix: '360x800' },
  { width: 390, height: 844, suffix: '390x844' },
  { width: 768, height: 1024, suffix: '768x1024' },
  { width: 820, height: 1180, suffix: '820x1180' },
  { width: 1024, height: 768, suffix: '1024x768' },
  { width: 1366, height: 768, suffix: '1366x768' },
  { width: 1440, height: 900, suffix: '1440x900' },
  { width: 1920, height: 1080, suffix: '1920x1080' },
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
  try {
    new Function(`return (${expression})`)
  } catch (error) {
    throw new Error(`Invalid browser expression: ${error instanceof Error ? error.message : String(error)}`)
  }
  const result = await cdp('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true })
  if (result.exceptionDetails) {
    const exception = result.exceptionDetails.exception
    throw new Error(`${result.exceptionDetails.text ?? 'Browser evaluation failed.'} ${exception?.description ?? exception?.value ?? ''}`.trim())
  }
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

async function openAdmissionProcedure() {
  const clicked = await evaluate(`(() => {
    const button = [...document.querySelectorAll('.secretariat-role-tabs button')]
      .find(candidate => candidate.querySelector('strong')?.textContent.trim() === 'Afiliación e incorporación');
    if (!button) return false;
    button.click(); return true;
  })()`)
  if (!clicked) throw new Error('Admission tab not found for Lodge Secretary.')
  await waitForExpression(`!!document.querySelector('.admissions-documents')`, 'admission evidence list')
  await evaluate(`(() => { const button = [...document.querySelectorAll('.admissions-documents button')].find(x => x.textContent.trim() === 'Cargar expediente'); if (!button || button.disabled) throw new Error('No synthetic admission case selected.'); button.click(); })()`)
  await waitForExpression(`!!document.querySelector('[aria-labelledby="admission-procedure-title"] li')`, 'admission procedure requirements')
  const result = await evaluate(`(() => {
    const panel = document.querySelector('[aria-labelledby="admission-procedure-title"]');
    const visibleForms = [...document.querySelectorAll('.admissions-page form')].filter(x => x.getClientRects().length);
    const ceremony = [...panel.querySelectorAll('button')].find(x => x.textContent.trim() === 'Solicitar ceremonia' && x.type === 'button');
    const forbidden = [...panel.querySelectorAll('option')].some(x => /Gran Maestría|art. 2.3/.test(x.textContent));
    return { visibleForms: visibleForms.length, ceremonyDisabled: ceremony?.disabled === true, forbidden, alert: !!panel.querySelector('[role="alert"]') };
  })()`)
  if (result.visibleForms || !result.ceremonyDisabled || result.forbidden || result.alert) throw new Error(`Admission procedure check failed: ${JSON.stringify(result)}`)
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

async function assertInitiationNavigation(scenario, viewport) {
  const open = async tab => {
    const clicked = await evaluate(`(() => {
      const nav = document.querySelector('nav[aria-label="Secciones de Insinuaciones e Iniciación"]');
      const label = button => [...button.childNodes].filter(node => node.nodeType === Node.TEXT_NODE).map(node => node.textContent).join('').trim();
      const button = [...(nav?.querySelectorAll('[role="tab"]') || [])].find(button => label(button) === ${JSON.stringify(tab)});
      if (!button || button.disabled) return false;
      button.click(); return true;
    })()`);
    if (!clicked) throw new Error(`Initiation tab missing for ${scenario.profile}: ${tab}`);
    await delay(350);
  };
  const read = () => evaluate(`(() => {
    const label = button => [...button.childNodes].filter(node => node.nodeType === Node.TEXT_NODE).map(node => node.textContent).join('').trim();
    const nav = document.querySelector('nav[aria-label="Secciones de Insinuaciones e Iniciación"]');
    const sidebar = [...document.querySelectorAll('nav.sidebar .nav-item')];
    return { tabs: [...(nav?.querySelectorAll('[role="tab"]') || [])].map(label),
      activeTabs: [...(nav?.querySelectorAll('[aria-selected="true"]') || [])].map(label),
      activeSidebar: sidebar.filter(button => button.classList.contains('active')).map(button => button.querySelector('.nav-text')?.textContent.trim()),
      count: sidebar.length };
  })()`);
  const initial = await read();
  if (JSON.stringify(initial.tabs) !== JSON.stringify(scenario.initiationTabs)) throw new Error(`Wrong initiation tabs for ${scenario.profile}: ${JSON.stringify(initial)}`);
  if (scenario.sidebarCount && initial.count !== scenario.sidebarCount) throw new Error(`Expected ${scenario.sidebarCount} sidebar entries for ${scenario.profile}, got ${initial.count}`);
  for (const tab of scenario.initiationTabs) {
    await open(tab);
    const state = await read();
    if (JSON.stringify(state.activeTabs) !== JSON.stringify([tab]) || JSON.stringify(state.activeSidebar) !== JSON.stringify(['Insinuaciones e Iniciación'])) throw new Error(`Wrong initiation selection for ${scenario.profile}/${tab}: ${JSON.stringify(state)}`);
    await assertNoGlobalHorizontalOverflow(`${scenario.profile}/${tab}`, viewport.suffix);
  }
  if (scenario.initiationTabs.length) await open('Publicados');
  console.log(`initiation navigation ${scenario.profile} ${viewport.suffix}: ${JSON.stringify(initial)}`);
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
  const compactLayout = result.viewportWidth <= 1180
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
  // PMGM-UX vista operativa (03-10-2026): «Registrar abono» se abre en panel (ActionDrawer) desde la tarjeta.
  await evaluate(`(() => {
    const card = document.querySelector('.ceremony-right-card');
    const trigger = card?.querySelector('.action-trigger');
    if (trigger && trigger.getAttribute('aria-expanded') !== 'true') trigger.click();
    return true;
  })()`)
  await new Promise(resolve => setTimeout(resolve, 350))
  const result = await evaluate(`(() => {
    const card = document.querySelector('.ceremony-right-card');
    const trigger = card?.querySelector('.action-trigger');
    const drawer = card?.querySelector('.action-drawer');
    const button = drawer?.querySelector('form button[type="submit"], form button:not([type])');
    const rect = card?.getBoundingClientRect();
    const drawerRect = drawer?.getBoundingClientRect();
    const labels = drawer ? [...drawer.querySelectorAll('form label span')].map(x => (x.textContent || '').trim()) : [];
    const close = drawer?.querySelector('.action-drawer-close, button[aria-label^="Cerrar"]');
    if (close) close.click();
    return { card: !!card, button: !!button, triggerSize: trigger?.getBoundingClientRect().height ?? 0, touchSize: button?.getBoundingClientRect().height ?? 0,
      cardRight: rect?.right ?? 0, drawerRight: drawerRect?.right ?? 0, viewportWidth: innerWidth, labels };
  })()`)
  await new Promise(resolve => setTimeout(resolve, 200))
  if (!result?.card || !result.button) throw new Error(`Ceremony right payment action is missing at ${viewport}.`)
  if (result.touchSize < 44 || result.triggerSize < 44) throw new Error(`Ceremony right payment action is below 44px at ${viewport}: ${JSON.stringify(result)}.`)
  if (result.cardRight > result.viewportWidth + 1 || result.drawerRight > result.viewportWidth + 1) throw new Error(`Ceremony right card or drawer is clipped at ${viewport}.`)
  if (result.labels.length !== 4) throw new Error(`Ceremony right form is incomplete at ${viewport}: ${JSON.stringify(result.labels)}.`)
}

async function assertNoGlobalHorizontalOverflow(label, viewport) {
  const metrics = await evaluate(`(() => {
    const root = document.documentElement;
    const body = document.body;
    const scrollWidth = Math.max(root?.scrollWidth || 0, body?.scrollWidth || 0);
    const offenders = [...document.querySelectorAll('body *')]
      .map(element => {
        const rect = element.getBoundingClientRect();
        return {
          tag: element.tagName.toLowerCase(),
          className: typeof element.className === 'string' ? element.className : '',
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          width: Math.round(rect.width),
          scrollWidth: element.scrollWidth,
          clientWidth: element.clientWidth,
        };
      })
      .filter(element => element.right > innerWidth + 1 && element.width > 0)
      .sort((left, right) => right.right - left.right)
      .slice(0, 12);
    return { scrollWidth, innerWidth: window.innerWidth, offenders };
  })()`)
  if (!metrics || metrics.scrollWidth > metrics.innerWidth + 1) {
    throw new Error(`Global horizontal overflow in ${label} at ${viewport}: scrollWidth=${metrics?.scrollWidth ?? 'unknown'}, innerWidth=${metrics?.innerWidth ?? 'unknown'}, offenders=${JSON.stringify(metrics?.offenders ?? [])}`)
  }
}

async function assertDashboardMetricLayout(viewport) {
  const result = await evaluate(`(() => {
    const grid = document.querySelector('.metric-grid');
    if (!grid) return null;
    return {
      columns: getComputedStyle(grid).gridTemplateColumns.trim().split(/\\s+/).length,
      cardCount: grid.children.length,
    };
  })()`)
  // PMGM-UX-002: en móvil los 4 indicadores de Inicio se muestran en 2 columnas (decisión aprobada por el PO, 01-10-2026).
  // PMGM-UX menús sin repetir (03-10-2026): 3 accesos en Inicio; 1 columna en celular, 3 desde 721 px.
  const expectedColumns = viewport.width <= 720 ? 1 : 3
  if (!result || result.cardCount !== 3 || result.columns !== expectedColumns) {
    throw new Error(`Dashboard metrics layout is inconsistent at ${viewport.suffix}: expected ${expectedColumns} columns for 3 cards, got ${JSON.stringify(result)}.`)
  }
}

const slugify = value => value
  .normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '')
  .toLowerCase()
  .replace(/[^a-z0-9]+/g, '-')
  .replace(/^-|-$/g, '')

async function setMobileMenu(open) {
  // PMGM-UX-002: en ≤720 px el menú completo vive en una hoja que se abre desde la barra inferior.
  const state = await evaluate(`(() => {
    const tabbar = document.querySelector('.mobile-tabbar');
    if (!tabbar || getComputedStyle(tabbar).display === 'none') return 'absent';
    const nav = document.querySelector('nav.sidebar');
    const isOpen = !!nav && nav.classList.contains('is-open');
    if (isOpen === ${JSON.stringify(open)}) return 'ready';
    tabbar.querySelector('[data-tab="menu"]')?.click();
    return 'toggled';
  })()`)
  if (state === 'toggled') await waitForExpression(`document.querySelector('nav.sidebar')?.classList.contains('is-open') === ${JSON.stringify(open)}`, open ? 'mobile menu sheet open' : 'mobile menu sheet closed')
}

async function inspectAllVisibleMediaAndNavigation(profile) {
  await setMobileMenu(true)
  try {
    return await inspectAllVisibleMediaAndNavigationWithMenuOpen(profile)
  } finally {
    await setMobileMenu(false)
  }
}

async function inspectAllVisibleMediaAndNavigationWithMenuOpen(profile) {
  const result = await evaluate(`(() => {
    const nav = document.querySelector('nav.sidebar');
    const main = document.querySelector('main.content');
    const buttons = [...(nav?.querySelectorAll('button:not(.text-size-control button)') || [])].filter(button => !button.disabled && getComputedStyle(button).display !== 'none'); // PMGM-UX-004: el control «Tamaño de letra» no es un módulo
    const navRect = nav?.getBoundingClientRect();
    const media = [...document.querySelectorAll('img, video, canvas, svg')].filter(element => {
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
    }).map(element => {
      const rect = element.getBoundingClientRect();
      const container = element.closest('.panel, .member-card, .metric-card, figure, .brand-mark, main.content') || element.parentElement;
      const box = container?.getBoundingClientRect();
      return { src: element.getAttribute('src') || element.tagName.toLowerCase(), left: Math.round(rect.left), right: Math.round(rect.right),
        containerLeft: box ? Math.round(box.left) : null, containerRight: box ? Math.round(box.right) : null };
    });
    const tabs = [...document.querySelectorAll('main [role="tab"], main .secretariat-role-tabs button, main .system-tabs button, main .library-category-rail button, main .segmented button, main .segmented-control button')]
      .filter(button => !button.disabled && getComputedStyle(button).display !== 'none')
      .map((button, index) => ({ index, label: (button.innerText || button.textContent || '').replace(/\\s+/g, ' ').trim() }))
      .filter(item => item.label);
    const filters = [...document.querySelectorAll('main .system-filter select')].flatMap(select => {
      const fieldLabel = select.closest('label')?.querySelector('span')?.textContent?.trim() || 'Filtro';
      return [...select.options].filter(option => option.value !== select.value).map(option => ({ fieldLabel, value: option.value, label: option.textContent?.trim() || option.value }));
    });
    return {
      nav: nav && navRect ? { width: Math.round(navRect.width), clientWidth: nav.clientWidth, scrollWidth: nav.scrollWidth,
        clientHeight: nav.clientHeight, scrollHeight: nav.scrollHeight, overflowY: getComputedStyle(nav).overflowY,
        buttons: buttons.map(button => { const r = button.getBoundingClientRect(); return { label: (button.innerText || button.textContent || '').replace(/\\s+/g, ' ').trim(),
          height: Math.round(r.height), left: Math.round(r.left), right: Math.round(r.right), disabled: button.disabled }; }) } : null,
      viewportWidth: innerWidth,
      documentWidth: Math.max(document.documentElement.scrollWidth, document.body.scrollWidth),
      mainWidth: main?.getBoundingClientRect().width ?? 0,
      media,
      tabs,
      filters,
    };
  })()`)
  if (!result?.nav || !result.nav.buttons.length) throw new Error(`Mobile navigation is missing for ${profile}.`)
  if (result.documentWidth > result.viewportWidth + 1) throw new Error(`Global horizontal overflow for ${profile}: ${result.documentWidth}px > ${result.viewportWidth}px.`)
  if (result.nav.buttons.some(button => button.height < 44)) throw new Error(`A mobile navigation target is below 44px for ${profile}: ${JSON.stringify(result.nav.buttons.filter(button => button.height < 44))}`)
  if (result.nav.buttons.some(button => button.left < -1 || button.right > result.viewportWidth + 1)) throw new Error(`A mobile navigation item exceeds the viewport for ${profile}: ${JSON.stringify(result.nav.buttons.filter(button => button.left < -1 || button.right > result.viewportWidth + 1))}`)
  if (result.nav.scrollHeight > result.nav.clientHeight && !['auto', 'scroll'].includes(result.nav.overflowY)) throw new Error(`Long mobile navigation is not vertically scrollable for ${profile}.`)
  const overflowingMedia = result.media.filter(item => item.left < -1 || item.right > result.viewportWidth + 1 || (item.containerLeft !== null && item.left < item.containerLeft - 1) || (item.containerRight !== null && item.right > item.containerRight + 1))
  if (overflowingMedia.length) throw new Error(`Media extends beyond its visible container for ${profile}: ${JSON.stringify(overflowingMedia)}`)
  return result
}

async function openModuleFromMobileNavigation(label) {
  await setMobileMenu(true)
  const opened = await evaluate(`(() => {
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    const nav = document.querySelector('nav.sidebar');
    const button = [...(nav?.querySelectorAll('button') || [])].find(candidate => !candidate.disabled && normalize(candidate.innerText || candidate.textContent) === ${JSON.stringify(label)});
    if (!button || !nav) return false;
    const navRect = nav.getBoundingClientRect();
    const buttonRect = button.getBoundingClientRect();
    if (buttonRect.top < navRect.top) nav.scrollTop -= navRect.top - buttonRect.top + 4;
    else if (buttonRect.bottom > navRect.bottom) nav.scrollTop += buttonRect.bottom - navRect.bottom + 4;
    const visibleRect = button.getBoundingClientRect();
    if (visibleRect.top < nav.getBoundingClientRect().top - 1 || visibleRect.bottom > nav.getBoundingClientRect().bottom + 1) return false;
    button.click();
    return true;
  })()`)
  if (!opened) throw new Error(`Mobile navigation item cannot be scrolled into view: ${label}`)
  await waitForExpression(`(() => { const normalize = value => (value || '').replace(/\\s+/g, ' ').trim(); return [...document.querySelectorAll('nav.sidebar button.active')].some(button => normalize(button.innerText || button.textContent) === ${JSON.stringify(label)}); })()`, `mobile module ${label}`)
  await delay(500)
}

async function openMobileSubview(label, { optional = false } = {}) {
  const clicked = await evaluate(`(() => {
    const selectors = 'main [role="tab"], main .secretariat-role-tabs button, main .system-tabs button, main .library-category-rail button, main .segmented button, main .segmented-control button';
    const normalize = value => (value || '').replace(/\\s+/g, ' ').trim();
    const button = [...document.querySelectorAll(selectors)].find(candidate => !candidate.disabled && normalize(candidate.innerText || candidate.textContent) === ${JSON.stringify(label)});
    if (!button) return false;
    button.click();
    return true;
  })()`)
  if (!clicked) {
    if (optional) return false
    throw new Error(`Mobile subview control disappeared: ${label}`)
  }
  await delay(250)
  return true
}

async function selectMobileFilter(fieldLabel, value) {
  const selected = await evaluate(`(() => {
    const normalize = text => (text || '').replace(/\\s+/g, ' ').trim();
    const select = [...document.querySelectorAll('main .system-filter select')].find(element => normalize(element.closest('label')?.querySelector('span')?.textContent) === ${JSON.stringify(fieldLabel)});
    if (!select || ![...select.options].some(option => option.value === ${JSON.stringify(value)})) return false;
    select.value = ${JSON.stringify(value)};
    select.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  })()`)
  if (!selected) throw new Error(`Mobile filter option disappeared: ${fieldLabel}/${value}`)
  await delay(250)
}

async function auditEveryMobileMenuView() {
  const mobileViewport = { width: 360, height: 800, suffix: '360x800' }
  await resetPage(mobileViewport.width, mobileViewport.height)
  const profiles = await evaluate(`(() => [...document.querySelectorAll('select[aria-label="Seleccionar perfil de demostración"] option')].map(option => option.value).filter(Boolean))()`)
  if (!Array.isArray(profiles) || profiles.length < 10) throw new Error(`Expected the complete demo profile selector; found ${JSON.stringify(profiles)}.`)
  let auditedViews = 0

  for (const profile of profiles) {
    await resetPage(mobileViewport.width, mobileViewport.height)
    await selectProfile(profile)
    const menuLabels = await evaluate(`(() => {
      const buttons = [...document.querySelectorAll('nav.sidebar button:not(.text-size-control button)')].filter(button => !button.disabled && getComputedStyle(button).display !== 'none')
      return [...new Set(buttons.map(button => (button.innerText || button.textContent || '').replace(/\\s+/g, ' ').trim()).filter(Boolean))]
    })()`)
    if (!menuLabels?.length) throw new Error(`No active menu items found for demo profile ${profile}.`)
    const navigation = await inspectAllVisibleMediaAndNavigation(profile)
    await setMobileMenu(true)
    const canReachMenuEnd = await evaluate(`(() => {
      const nav = document.querySelector('nav.sidebar');
      if (!nav) return false;
      nav.scrollTop = nav.scrollHeight;
      const buttons = [...nav.querySelectorAll('button')].filter(button => !button.disabled && getComputedStyle(button).display !== 'none');
      const last = buttons.at(-1)?.getBoundingClientRect();
      const rect = nav.getBoundingClientRect();
      const reachable = !!last && last.bottom <= rect.bottom + 1 && last.top >= rect.top - 1;
      nav.scrollTop = 0;
      return reachable;
    })()`)
    await setMobileMenu(false)
    if (!canReachMenuEnd) throw new Error(`Last mobile menu item cannot be reached by vertical scrolling for ${profile}.`)
    console.log(`mobile navigation ${profile}: ${navigation.nav.buttons.length} items`)

    for (const menuLabel of menuLabels) {
      await openModuleFromMobileNavigation(menuLabel)
      await assertNoGlobalHorizontalOverflow(`${profile}/${menuLabel}`, mobileViewport.suffix)
      const defaults = await inspectAllVisibleMediaAndNavigation(`${profile}/${menuLabel}`)
      const baseSlug = `all-${slugify(profile)}-${slugify(menuLabel)}`
      await capture(path.join(outputDir, `${baseSlug}-${mobileViewport.suffix}.png`))
      auditedViews += 1

      for (const { label, index } of defaults.tabs) {
        // PMGM-UX-003: las pestañas de un espacio (p. ej. Gestión Logial) desaparecen al cambiar de función por cargo;
        // si el control ya no está, se vuelve al estado inicial del módulo y se reintenta una vez.
        if (!(await openMobileSubview(label, { optional: true }))) {
          await openModuleFromMobileNavigation(menuLabel)
          await openMobileSubview(label)
        }
        await assertNoGlobalHorizontalOverflow(`${profile}/${menuLabel}/${label}`, mobileViewport.suffix)
        await inspectAllVisibleMediaAndNavigation(`${profile}/${menuLabel}/${label}`)
        await capture(path.join(outputDir, `${baseSlug}-${String(index + 1).padStart(2, '0')}-${slugify(label)}-${mobileViewport.suffix}.png`))
        auditedViews += 1
      }

      for (const { fieldLabel, value, label } of defaults.filters) {
        await selectMobileFilter(fieldLabel, value)
        await assertNoGlobalHorizontalOverflow(`${profile}/${menuLabel}/${fieldLabel}/${label}`, mobileViewport.suffix)
        await inspectAllVisibleMediaAndNavigation(`${profile}/${menuLabel}/${fieldLabel}/${label}`)
        await capture(path.join(outputDir, `${baseSlug}-filter-${slugify(fieldLabel)}-${slugify(label)}-${mobileViewport.suffix}.png`))
        auditedViews += 1
      }
    }
  }
  if (auditedViews < 100) throw new Error(`Mobile navigation/view audit coverage is unexpectedly low: ${auditedViews} states.`)
  console.log(`MOBILE ALL-MENU VIEW AUDIT OK: ${profiles.length} profiles, ${auditedViews} module/subview states at ${mobileViewport.suffix}`)
}

async function capture(filePath, scrollSelector = null) {
  await evaluate(`(() => {
    window.scrollTo(0, 0);
    document.documentElement.scrollLeft = 0;
    document.body.scrollLeft = 0;
    const content = document.querySelector('main.content');
    if (content) { content.scrollTop = 0; content.scrollLeft = 0; }
    const sidebar = document.querySelector('nav.sidebar');
    if (sidebar) {
      sidebar.scrollTop = 0;
      sidebar.scrollLeft = 0;
      const active = sidebar.querySelector('button.active');
      const sidebarRect = sidebar.getBoundingClientRect();
      const activeRect = active?.getBoundingClientRect();
      if (activeRect && activeRect.top < sidebarRect.top) sidebar.scrollTop -= sidebarRect.top - activeRect.top + 4;
      else if (activeRect && activeRect.bottom > sidebarRect.bottom) sidebar.scrollTop += activeRect.bottom - sidebarRect.bottom + 4;
    }
    const target = ${JSON.stringify(scrollSelector)} ? document.querySelector(${JSON.stringify(scrollSelector)}) : null;
    if (target) {
      target.scrollIntoView({ block: 'start', inline: 'nearest' });
      const sidebarBottom = innerWidth <= 980
        ? (() => {
            const rect = document.querySelector('nav.sidebar')?.getBoundingClientRect();
            return rect && rect.top < innerHeight / 2 ? rect.bottom : 0;
          })()
        : 0;
      const stickyBottom = Math.max(
        document.querySelector('header.topbar')?.getBoundingClientRect().bottom || 0,
        sidebarBottom
      );
      window.scrollBy(0, -(stickyBottom + 12));
      if (${JSON.stringify(scrollSelector)} && target) {
        const action = target.querySelector('button');
        let actionRect = action?.getBoundingClientRect();
        if (action && (!actionRect || actionRect.top < stickyBottom || actionRect.bottom > innerHeight || actionRect.right > innerWidth)) {
          action.scrollIntoView({ block: 'center', inline: 'nearest' });
          actionRect = action.getBoundingClientRect();
          if (actionRect.top < stickyBottom + 12) window.scrollBy(0, -(stickyBottom + 12 - actionRect.top));
          else if (actionRect.bottom > innerHeight - 12) window.scrollBy(0, actionRect.bottom - innerHeight + 12);
          actionRect = action.getBoundingClientRect();
        }
        if (!actionRect || actionRect.top < stickyBottom || actionRect.bottom > innerHeight || actionRect.right > innerWidth) {
          throw new Error('Treasury payment action is outside the visible viewport after positioning: ' + JSON.stringify({ action: actionRect && { top: actionRect.top, bottom: actionRect.bottom, right: actionRect.right }, stickyBottom, innerWidth, innerHeight, main: document.querySelector('main.content')?.getBoundingClientRect().toJSON(), sidebar: document.querySelector('nav.sidebar')?.getBoundingClientRect().toJSON() }));
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
      if (scenario.initiationTabs) await assertInitiationNavigation(scenario, viewport)
      if (scenario.admissions) await openAdmissionProcedure()
      if (scenario.slug === 'inicio') await assertDashboardMetricLayout(viewport)
      if (scenario.treasuryCollection) {
        await openTreasuryCollection()
        await assertTreasuryPaymentAction(viewport.suffix)
      }
      if (scenario.grandTreasuryRights) {
        await openGrandTreasuryRights()
        await assertCeremonyRightPaymentAction(viewport.suffix)
      }
      await assertNoGlobalHorizontalOverflow(scenario.label, viewport.suffix)
      const viewSlug = scenario.treasuryCollection ? 'tesoreria-taller-cuotas' : scenario.slug
      const filePath = path.join(outputDir, `${viewSlug}-${viewport.suffix}.png`)
      const evidenceTarget = scenario.treasuryCollection
        ? '.treasury-collection-table tbody tr:has(button)'
        : scenario.grandTreasuryRights
          ? '.ceremony-right-card' // PMGM-UX: el formulario vive en el panel; se encuadra la tarjeta con su botón «Registrar abono»
          : scenario.admissions
            ? '[aria-labelledby="admission-procedure-title"]'
            : null
      await capture(filePath, evidenceTarget)
      console.log(`captured ${path.basename(filePath)}`)
    }
  }
  await auditEveryMobileMenuView()
} finally {
  await stopChromeAndClean()
}
