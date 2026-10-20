import { gzipSync } from 'node:zlib'
import { fileURLToPath } from 'node:url'
import { build } from 'vite'
import { expect, it, vi } from 'vitest'

it('keeps internal views outside the initial static import graph and within the public-entry budget', async () => {
  const result = await (async () => {
    // Measure production React, not Vitest's development NODE_ENV.
    vi.stubEnv('NODE_ENV', 'production')
    try {
      return await build({
        root: fileURLToPath(new URL('../', import.meta.url)),
        configFile: fileURLToPath(new URL('../vite.config.ts', import.meta.url)),
        mode: 'production',
        logLevel: 'silent',
        build: { write: false, manifest: false },
      })
    } finally {
      vi.unstubAllEnvs()
    }
  })()
  const outputs = Array.isArray(result) ? result : [result]
  const styles = outputs.flatMap(output => 'output' in output ? output.output : [])
    .filter(output => output.type === 'asset' && output.fileName.endsWith('.css'))
  // Existing component rules stay in the initial global cascade, not a later
  // stylesheet that could override the institutional theme after sign-in.
  expect(styles).toHaveLength(1)
  const chunks = outputs.flatMap(output => 'output' in output ? output.output : [])
    .filter(output => output.type === 'chunk')
  const byFile = new Map(chunks.map(chunk => [chunk.fileName, chunk]))
  const initial = new Set<string>()
  const visit = (file: string) => {
    if (initial.has(file)) return
    initial.add(file)
    const chunk = byFile.get(file)
    expect(chunk).toBeDefined()
    for (const imported of chunk!.imports) visit(imported)
  }
  const entries = chunks.filter(chunk => chunk.isEntry)
  expect(entries).toHaveLength(1)
  entries.forEach(chunk => visit(chunk.fileName))

  const application = chunks.find(chunk => Object.keys(chunk.modules)
    .some(id => id.replaceAll('\\', '/').endsWith('/src/App.tsx')))
  expect(application).toBeDefined()
  expect(initial.has(application!.fileName)).toBe(false)
  expect(chunks.some(chunk => chunk.dynamicImports.includes(application!.fileName))).toBe(true)
  const compressedBytes = [...initial].reduce((total, file) => total + gzipSync(byFile.get(file)!.code).length, 0)
  // Baseline was 338591 bytes; this includes every initial static dependency.
  expect(compressedBytes).toBeLessThan(180_000)
}, 30_000)
