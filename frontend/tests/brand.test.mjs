import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import ts from "typescript";

// Nome exibido centralizado em src/brand.ts (a equipe ainda decide entre Coreletter e Notiffy).
const require = createRequire(import.meta.url);
await import("@angular/compiler");
const source = await readFile(new URL("../src/brand.ts", import.meta.url), "utf8");
let compiled = ts.transpileModule(source, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022,
  experimentalDecorators: true, useDefineForClassFields: false,
} }).outputText;
compiled = compiled.replace(/from "(@angular\/[^\"]+)"/g,
  (_, name) => `from "${pathToFileURL(require.resolve(name)).href}"`);
const { BRAND, partesDoLogo } = await import("data:text/javascript;base64," + Buffer.from(compiled).toString("base64"));

test("logo divide o nome em duas cores pelo destaque", () => {
  assert.deepEqual(partesDoLogo("Coreletter", "letter"), ["Core", "letter"]);
  assert.deepEqual(partesDoLogo("Notiffy", "ffy"), ["Noti", "ffy"]);
});

test("destaque vazio, fora do fim ou igual ao nome mostra o nome inteiro numa cor", () => {
  assert.deepEqual(partesDoLogo("Notiffy", ""), ["Notiffy", ""]);
  assert.deepEqual(partesDoLogo("Notiffy", "Noti"), ["Notiffy", ""]);
  assert.deepEqual(partesDoLogo("Paralaxe", "Paralaxe"), ["Paralaxe", ""]);
});

test("o nome configurado gera um logo válido", () => {
  const [a, b] = partesDoLogo(BRAND.nome, BRAND.destaque);
  assert.equal(a + b, BRAND.nome);
});

test("templates não repetem o nome fixo: a troca é só em src/brand.ts", async () => {
  for (const f of ["app.html", "hero.ts"]) {
    const t = await readFile(new URL(`../src/${f}`, import.meta.url), "utf8");
    assert.doesNotMatch(t, new RegExp(BRAND.nome, "i"), `${f} tem o nome fixo`);
  }
});
