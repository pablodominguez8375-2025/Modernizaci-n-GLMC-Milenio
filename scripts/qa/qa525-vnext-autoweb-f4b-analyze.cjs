#!/usr/bin/env node
// QA525 auto-web F4-B SHADOW analyzer (CI only, INACTIVE). Contract: docs/qa/QA525-AUTOWEB-F4B-SHADOW.md
//
// Reads JSON from stdin: [{"path": str, "old": str, "new": str}] (UTF-8 file contents, before/after)
// Writes JSON to stdout: [{"path", "imports_equal", "imports_added", "imports_removed",
//   "new_sinks": {kind: delta}, "new_api_refs": delta, "new_mutating_prop_calls": delta,
//   "added_lines", "parse_ok"}]
//
// Method: parse both versions with the TypeScript compiler (AST, not regex) and compare COUNTS
// per kind (a multiset). Any increase is a finding. Moving code around keeps counts equal; adding a
// new fetch/localStorage/API call increases them. This is a conservative gate for a SHADOW verdict;
// it proves nothing about data safety on its own (server-side authorization does that).
'use strict';
const path = require('path');
const ts = require(path.join(__dirname, '../../frontend/node_modules/typescript'));

const SINK_IDENTIFIERS = new Set(['fetch', 'XMLHttpRequest', 'WebSocket', 'EventSource', 'eval',
  'localStorage', 'sessionStorage', 'indexedDB', 'Worker', 'SharedWorker', 'importScripts']);
const SINK_PROPERTIES = new Set(['sendBeacon', 'postMessage', 'innerHTML', 'outerHTML',
  'insertAdjacentHTML', 'cookie', 'serviceWorker', 'localStorage', 'sessionStorage', 'indexedDB']);
const API_SPEC = /(^|\/)(api|auth|accounts|oidc|keycloak)(\/|$)|api(\.ts)?$/i;
const MUTATING_PROP = /^on(?=[A-Z])\w*?(Submit|Save|Delete|Approve|Confirm|Reject|Remove|Create|Update|Send|Pay|Close)$/;

function inc(map, key) { map[key] = (map[key] || 0) + 1; }

function analyzeTs(text, fileName) {
  const kind = fileName.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sf = ts.createSourceFile(fileName, text, ts.ScriptTarget.Latest, true, kind);
  const parseOk = !(sf.parseDiagnostics && sf.parseDiagnostics.length);
  const imports = new Set();
  const apiNames = new Set();
  const sinks = {};
  let apiRefs = 0;
  let mutatingCalls = 0;

  for (const st of sf.statements) {
    if ((ts.isImportDeclaration(st) || ts.isExportDeclaration(st)) && st.moduleSpecifier
        && ts.isStringLiteral(st.moduleSpecifier)) {
      const spec = st.moduleSpecifier.text;
      imports.add(spec);
      if (ts.isImportDeclaration(st) && API_SPEC.test(spec) && st.importClause) {
        const c = st.importClause;
        if (c.name) apiNames.add(c.name.text);
        if (c.namedBindings) {
          if (ts.isNamespaceImport(c.namedBindings)) apiNames.add(c.namedBindings.name.text);
          else for (const el of c.namedBindings.elements) apiNames.add(el.name.text);
        }
      }
    } else if (ts.isImportEqualsDeclaration(st)) {
      inc(sinks, 'import_equals');
    }
  }

  function visit(node, parent) {
    if (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) return;
    if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword) inc(sinks, 'dynamic_import');
    if (ts.isCallExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'require') inc(sinks, 'require');
    if (ts.isNewExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'Function') inc(sinks, 'new_Function');
    if (ts.isMetaProperty(node) && node.name.text === 'meta' && parent && ts.isPropertyAccessExpression(parent)
        && parent.name.text === 'env') inc(sinks, 'import_meta_env');
    if (ts.isJsxAttribute(node) && node.name.getText(sf) === 'dangerouslySetInnerHTML') inc(sinks, 'dangerouslySetInnerHTML');
    if (ts.isIdentifier(node)) {
      const name = node.text;
      const isPropName = parent && ts.isPropertyAccessExpression(parent) && parent.name === node;
      const isDecl = parent && (ts.isPropertyAssignment(parent) || ts.isBindingElement(parent)
        || ts.isParameter(parent) || ts.isVariableDeclaration(parent)) && parent.name === node;
      if (isPropName) {
        if (SINK_PROPERTIES.has(name)) inc(sinks, 'prop:' + name);
        if (name === 'open' && ts.isIdentifier(parent.expression) && parent.expression.text === 'window') inc(sinks, 'window.open');
        if (['href', 'assign', 'replace'].includes(name) && /location$/.test(parent.expression.getText(sf))) inc(sinks, 'location:' + name);
      } else if (!isDecl) {
        if (SINK_IDENTIFIERS.has(name)) inc(sinks, 'id:' + name);
        if (apiNames.has(name)) apiRefs += 1;
      }
    }
    if (ts.isCallExpression(node)) {
      const callee = node.expression;
      const last = ts.isPropertyAccessExpression(callee) ? callee.name.text : ts.isIdentifier(callee) ? callee.text : '';
      if (MUTATING_PROP.test(last)) mutatingCalls += 1;
    }
    ts.forEachChild(node, (child) => visit(child, node));
  }
  visit(sf, undefined);
  return { parseOk, imports, sinks, apiRefs, mutatingCalls };
}

function analyzeCss(text) {
  const sinks = {};
  const count = (re, key) => { const m = text.match(re); if (m) sinks[key] = m.length; };
  count(/@import/gi, 'css:@import');
  count(/url\(\s*['"]?\s*(https?:)?\/\//gi, 'css:url_external');
  count(/expression\s*\(/gi, 'css:expression');
  count(/behavior\s*:/gi, 'css:behavior');
  return { parseOk: true, imports: new Set(), sinks, apiRefs: 0, mutatingCalls: 0 };
}

function analyze(p, text) {
  if (p.endsWith('.css')) return analyzeCss(text);
  return analyzeTs(text, p);
}

function addedLines(oldText, newText) {
  const old = new Map();
  for (const line of oldText.split('\n')) old.set(line, (old.get(line) || 0) + 1);
  let added = 0;
  for (const line of newText.split('\n')) {
    const n = old.get(line) || 0;
    if (n > 0) old.set(line, n - 1); else added += 1;
  }
  return added;
}

function compare(entry) {
  if (!entry || typeof entry.path !== 'string' || typeof entry.old !== 'string' || typeof entry.new !== 'string') {
    throw new Error('F4B_INPUT_INVALID');
  }
  const a = analyze(entry.path, entry.old);
  const b = analyze(entry.path, entry.new);
  const newSinks = {};
  for (const [k, v] of Object.entries(b.sinks)) if (v > (a.sinks[k] || 0)) newSinks[k] = v - (a.sinks[k] || 0);
  const added = [...b.imports].filter((x) => !a.imports.has(x)).sort();
  const removed = [...a.imports].filter((x) => !b.imports.has(x)).sort();
  return {
    path: entry.path,
    parse_ok: a.parseOk && b.parseOk,
    imports_equal: added.length === 0 && removed.length === 0,
    imports_added: added,
    imports_removed: removed,
    new_sinks: newSinks,
    new_api_refs: Math.max(0, b.apiRefs - a.apiRefs),
    new_mutating_prop_calls: Math.max(0, b.mutatingCalls - a.mutatingCalls),
    added_lines: addedLines(entry.old, entry.new),
  };
}

if (require.main === module) {
  let raw = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (c) => { raw += c; });
  process.stdin.on('end', () => {
    try {
      const input = JSON.parse(raw);
      if (!Array.isArray(input) || input.length > 300) throw new Error('F4B_INPUT_INVALID');
      process.stdout.write(JSON.stringify(input.map(compare)) + '\n');
    } catch (e) {
      process.stdout.write(JSON.stringify({ error: 'F4B_ANALYZER_FAILED' }) + '\n');
      process.exit(1);
    }
  });
}

module.exports = { compare };
