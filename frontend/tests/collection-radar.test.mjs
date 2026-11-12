import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import ts from "typescript";

// Exercise the real component state with Angular signals and a controlled HTTP boundary.
// This is not a browser rendering test; Angular's production build checks templates.
const require = createRequire(import.meta.url);
await import("@angular/compiler");
const source = await readFile(new URL("../src/collection-radar.ts", import.meta.url), "utf8");
let compiled = ts.transpileModule(source, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022,
  experimentalDecorators: true, useDefineForClassFields: false,
} }).outputText;
compiled = compiled.replace(/from "(@angular\/[^\"]+)"/g,
  (_, name) => `from "${pathToFileURL(require.resolve(name)).href}"`);
const { CollectionRadar } = await import("data:text/javascript;base64," + Buffer.from(compiled).toString("base64"));

const saved = {
  topic: "Tecnologia", keywords: [".NET"], excludedKeywords: ["publicidade"],
  sourceIds: ["dotnet"], intervalHours: 24, maxItems: 10, enabled: true,
  nextRunAt: "2026-10-28T00:00:00Z", lastRunAt: null,
  lastStatus: "pending", lastError: null,
};
const response = data => ({ ok: true, status: 200, json: async () => data, text: async () => JSON.stringify(data) });
const settle = () => new Promise(resolve => setImmediate(resolve));
const standardFetch = async url => response(url.endsWith("/sources")
  ? [{ id: "dotnet", name: ".NET Blog", language: "en" }]
  : url.endsWith("/profile") ? structuredClone(saved) : []);

test("polling refresh preserves unsaved questionnaire edits", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  globalThis.fetch = standardFetch;
  try {
    radar.memberId = "first";
    radar.ngOnChanges();
    await settle();
    assert.equal(radar.topic, "Tecnologia");
    radar.topic = "Minha edição ainda não salva";
    radar.keywords = "Angular";
    await radar.refresh();
    assert.equal(radar.topic, "Minha edição ainda não salva");
    assert.equal(radar.keywords, "Angular");
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});

test("pausing uses saved preferences instead of unfinished form edits", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  let submitted;
  globalThis.fetch = async (url, init) => {
    if (init.method === "PUT") { submitted = JSON.parse(init.body); return response({}); }
    return standardFetch(url);
  };
  try {
    radar.memberId = "first";
    radar.ngOnChanges();
    await settle();
    radar.topic = "Não salvar esta edição";
    await radar.pause();
    assert.equal(submitted.enabled, false);
    assert.equal(submitted.topic, saved.topic);
    assert.deepEqual(submitted.keywords, saved.keywords);
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});

test("late responses cannot restore the previous user's private results", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  const waiting = [];
  let oldSignal;
  globalThis.fetch = (url, init) => {
    oldSignal = init.signal;
    return new Promise(resolve => waiting.push(() => resolve(response(url.endsWith("/sources") ? []
      : url.endsWith("/profile") ? saved : [{ id: "old-private-item" }]))));
  };
  try {
    radar.memberId = "first";
    radar.ngOnChanges();
    const firstSignal = oldSignal;
    globalThis.fetch = standardFetch;
    radar.memberId = "second";
    radar.ngOnChanges();
    assert.equal(firstSignal.aborted, true);
    await settle();
    waiting.forEach(resolve => resolve());
    await settle();
    assert.deepEqual(radar.items(), []);
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});

test("destroying the radar aborts outstanding requests", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  const signals = [];
  globalThis.fetch = async (url, init) => { signals.push(init.signal); return standardFetch(url); };
  try {
    radar.memberId = "first";
    radar.ngOnChanges();
    await settle();
    radar.ngOnDestroy();
    assert.equal(signals.length, 3);
    assert.ok(signals.every(signal => signal.aborted));
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});

test("retry after initial connection failure restores saved questionnaire", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  globalThis.fetch = async () => { throw new Error("Conexão indisponível"); };
  try {
    radar.memberId = "first";
    radar.ngOnChanges();
    await settle();
    assert.match(radar.error(), /Conexão/);
    globalThis.fetch = standardFetch;
    await radar.refresh();
    assert.equal(radar.topic, saved.topic);
    assert.equal(radar.keywords, ".NET");
    assert.equal(radar.error(), "");
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});

test("a new account can configure a radar when the profile response has no body", async () => {
  const original = globalThis.fetch;
  const radar = new CollectionRadar();
  globalThis.fetch = async url => url.endsWith("/profile")
    ? { ok: true, status: 200, text: async () => "" } : standardFetch(url);
  try {
    radar.memberId = "new-account";
    radar.ngOnChanges();
    await settle();
    assert.equal(radar.profile(), null);
    assert.equal(radar.sources().length, 1);
    assert.equal(radar.loading(), false);
    assert.equal(radar.error(), "");
  } finally { radar.ngOnDestroy(); globalThis.fetch = original; }
});
