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
  { slug: 'gran-tesoreria', profile: 'grandLodge', label: 'Gran Tesorería' },
  { slug: 'gran-hospitalaria', profile: 'grandLodge', label: 'Gran Hospitalaria' },
  { slug: 'gran-secretaria', profile: 'grandLodge', label: 'Gran Secretaría' },
  { slug: 'gran-archivero', profile: 'grandLodge', label: 'Gran Archivero' },
]

const viewports = [
  { width: 390, height: 844, suffix: '390x844' },
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

async function capture(filePath) {
  await evaluate("window.scrollTo(0, 0); document.documentElement.scrollLeft = 0; document.body.scrollLeft = 0;")
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
      await assertNoGlobalHorizontalOverflow(scenario.label, viewport.suffix)
      const filePath = path.join(outputDir, `${scenario.slug}-${viewport.suffix}.png`)
      await capture(filePath)
      console.log(`captured ${path.basename(filePath)}`)
    }
  }
} finally {
  await stopChromeAndClean()
}
