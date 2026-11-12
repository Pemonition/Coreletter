// node --test video/gerador/*.test.mjs   (roda na CI, job "video")
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { composicao, escapar, roteiroDeComparacao, tempos, validarRoteiro } from "./lib.mjs";

const base = () => ({
  versao: 1,
  id: "teste",
  titulo: "Teste",
  cenas: [
    { tipo: "abertura", duracao: 5, titulo: "Olá" },
    { tipo: "etapa", duracao: 4, numero: 1, titulo: "Coleta", texto: "Feeds", itens: ["a", "b"] },
    { tipo: "comparacao", duracao: 6, titulo: "Fontes", afirmacoes: [{ status: "divergente", texto: "Datas", fontes: ["A", "B"] }] },
  ],
});

test("roteiro do vídeo institucional é válido", async () => {
  const r = JSON.parse(await readFile(new URL("../roteiros/como-funciona.json", import.meta.url), "utf8"));
  assert.deepEqual(validarRoteiro(r), []);
});

test("composição gerada no repositório está em dia com o roteiro", async () => {
  const r = JSON.parse(await readFile(new URL("../roteiros/como-funciona.json", import.meta.url), "utf8"));
  const gerada = await readFile(new URL("../composicoes/como-funciona/index.html", import.meta.url), "utf8");
  assert.equal(gerada, composicao(r), "rode: node video/gerador/gerar.mjs video/roteiros/como-funciona.json");
});

test("todos os erros vêm de uma vez", () => {
  const r = base();
  r.id = "Com Espaço";
  r.cenas[0].duracao = 1;
  r.cenas[1].numero = 0;
  r.cenas[2].afirmacoes[0].status = "verdadeiro";
  const erros = validarRoteiro(r);
  assert.equal(erros.length, 4, erros.join("\n"));
  assert.ok(erros.some((e) => e.startsWith("id:")));
  assert.ok(erros.some((e) => e.includes("status")));
});

test("rejeita texto longo, quebra de linha, tipo desconhecido e duração total acima de 3 min", () => {
  const r = base();
  r.cenas[0].titulo = "x".repeat(71);
  r.cenas[1].texto = "linha 1\nlinha 2";
  r.cenas.push({ tipo: "video-externo", duracao: 5, titulo: "?" });
  for (let i = 0; i < 6; i++) r.cenas.push({ tipo: "texto", duracao: 30, titulo: "t", texto: "x" });
  const erros = validarRoteiro(r).join("\n");
  assert.match(erros, /titulo: até 70/);
  assert.match(erros, /uma linha só/);
  assert.match(erros, /tipo: um de/);
  assert.match(erros, /duração total/);
});

test("cenas começam quando a anterior termina e a duração da raiz é a soma", () => {
  const r = base();
  assert.deepEqual(tempos(r).map((t) => t.inicio), [0, 5, 9]);
  const html = composicao(r);
  assert.match(html, /data-composition-id="teste" data-start="0" data-duration="15"/);
  assert.match(html, /id="teste-c2" class="clip cena cena-comparacao" data-start="9" data-duration="6"/);
});

test("regras do HyperFrames: raiz sem <template>, timeline com a mesma chave, sem <br>, fonte com @font-face, .clip não é animado", () => {
  const html = composicao(base());
  assert.doesNotMatch(html, /<template/);
  assert.match(html, /window\.__timelines\["teste"\] = tl/);
  assert.match(html, /gsap\.timeline\(\{ paused: true \}\)/);
  assert.doesNotMatch(html, /<br/i);
  assert.match(html, /@font-face \{ font-family: "DM Sans"/);
  assert.doesNotMatch(html, /tl\.\w+\("[^"]*\.clip/);
  assert.doesNotMatch(html, /autoAlpha|visibility|Math\.random|Date\.now/);
  // Cada cena termina antes ou no fim da raiz (lint clip_ends_past_root_duration).
  const raiz = Number(html.match(/data-start="0" data-duration="([\d.]+)"/)[1]);
  for (const [, ini, dur] of html.matchAll(/class="clip[^"]*" data-start="([\d.]+)" data-duration="([\d.]+)"/g))
    assert.ok(Number(ini) + Number(dur) <= raiz);
});

test("texto do roteiro nunca vira HTML (roteiro pode vir de IA)", () => {
  const r = base();
  r.cenas[0].titulo = '<script>alert(1)</script> & "aspas"';
  r.cenas[2].afirmacoes[0].fontes = ["<img src=x onerror=alert(1)>"];
  const html = composicao(r);
  assert.doesNotMatch(html, /<script>alert/);
  assert.doesNotMatch(html, /<img src=x/);
  assert.match(html, /&lt;script&gt;alert\(1\)&lt;\/script&gt; &amp; &quot;aspas&quot;/);
  assert.equal(escapar("a'b"), "a&#39;b");
});

test("composição recusa roteiro inválido", () => {
  const r = base();
  r.cenas = [];
  assert.throws(() => composicao(r), /Roteiro inválido/);
});

test("roteiro a partir de uma comparação (contrato v1) usa os nomes dos veículos e passa na validação", () => {
  const a = "11111111-1111-1111-1111-111111111111";
  const b = "22222222-2222-2222-2222-222222222222";
  const comparacao = {
    storyId: "abcdef12-0000-0000-0000-000000000000",
    simulated: true,
    sources: [
      { itemId: a, sourceName: "Tecnoblog", title: "Caixa confirma vazamento\nde dados" },
      { itemId: b, sourceName: "TecMundo", title: "Vazamento na Caixa" },
    ],
    claims: [
      { id: "c1", text: "2 fontes noticiam o mesmo acontecimento.", status: "concordante", supportedBy: [a, b], contradictedBy: [], evidence: [] },
      { id: "c2", text: "Só o Tecnoblog cita o número.", status: "unica_fonte", supportedBy: [], contradictedBy: [], evidence: [{ itemId: a, quote: "7,6 milhões" }] },
      { id: "c3", text: "Status desconhecido é ignorado", status: "verdadeiro", supportedBy: [a], contradictedBy: [], evidence: [] },
    ],
  };
  const r = roteiroDeComparacao(comparacao);
  assert.deepEqual(validarRoteiro(r), []);
  assert.equal(r.id, "historia-abcdef12");
  assert.equal(r.cenas[0].titulo, "Caixa confirma vazamento de dados");
  const comp = r.cenas.find((c) => c.tipo === "comparacao");
  assert.equal(comp.afirmacoes.length, 2);
  assert.deepEqual(comp.afirmacoes[0].fontes, ["Tecnoblog", "TecMundo"]);
  assert.deepEqual(comp.afirmacoes[1].fontes, ["Tecnoblog"]);
  assert.match(r.cenas.at(-1).texto, /simulada/);
});
