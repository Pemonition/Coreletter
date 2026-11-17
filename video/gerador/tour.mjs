#!/usr/bin/env node
// Tour pelo código: gera o roteiro do vídeo E o docs/TOUR.md a partir dos MESMOS trechos reais do repositório.
// Por que um script e não copiar à mão: copiar à mão desatualiza. Aqui o trecho é lido do arquivo na hora,
// e um teste (lib.test.mjs) avisa se o código mudou e o roteiro ficou para trás.
//
// Uso (na raiz do repositório):
//   node video/gerador/tour.mjs                      lê os arquivos deste repositório
//   node video/gerador/tour.mjs --raiz ../outra-copia  lê de outra cópia (ex.: outra branch); cai para esta se faltar
//   --docs docs/TOUR.md                               também escreve a versão em texto
import { readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { validarRoteiro, VERSAO_ROTEIRO } from "./lib.mjs";

const aqui = dirname(fileURLToPath(import.meta.url));
const raizRepo = resolve(aqui, "..", "..");

// Copia as linhas [a, b] (contando de 1) e tira o recuo comum, sem mudar mais nada.
export function trecho(conteudo, [a, b]) {
  const linhas = conteudo.replace(/\r\n/g, "\n").split("\n").slice(a - 1, b);
  if (linhas.length !== b - a + 1) throw new Error(`linhas ${a}-${b} não existem (o arquivo tem ${conteudo.split("\n").length})`);
  const recuo = Math.min(...linhas.filter((l) => l.trim()).map((l) => l.length - l.trimStart().length));
  return linhas.map((l) => l.slice(Math.min(recuo, l.length - l.trimStart().length)).trimEnd()).join("\n");
}

export function montarRoteiro(fonte, lerArquivo) {
  const d = fonte.duracoes;
  const cenas = [
    { tipo: "abertura", duracao: d.abertura, ...fonte.abertura },
    { tipo: "texto", duracao: d.mapa, ...fonte.mapa },
    ...fonte.partes.map((p) => ({
      tipo: "codigo",
      duracao: d.parte,
      titulo: p.titulo,
      texto: p.texto,
      arquivo: p.arquivo,
      linha: p.linhas[0],
      linguagem: p.linguagem,
      codigo: trecho(lerArquivo(p.arquivo), p.linhas),
    })),
    { tipo: "fechamento", duracao: d.fechamento, ...fonte.fechamento },
  ];
  return { versao: VERSAO_ROTEIRO, id: fonte.id, titulo: fonte.titulo, formato: { largura: 1920, altura: 1080 }, cenas };
}

const CERCA = { csharp: "csharp", sql: "sql", ts: "ts", js: "js", yaml: "yaml" };
export function montarDoc(roteiro, fonte) {
  const partes = roteiro.cenas.filter((c) => c.tipo === "codigo");
  return `# Tour pelo código

<!-- GERADO por video/gerador/tour.mjs a partir de video/roteiros/tour-do-codigo.fonte.json. Edite a fonte e gere de novo. -->

${fonte.mapa.texto} Cada seção mostra **onde** está o código, **por que** ele é assim e o trecho real.

O vídeo "${roteiro.titulo}" (\`video/roteiros/${roteiro.id}.json\`) usa exatamente estes trechos.

| # | Parte | Arquivo |
|---|---|---|
${partes.map((c, i) => `| ${i + 1} | ${c.titulo} | \`${c.arquivo}\` |`).join("\n")}

${partes
  .map(
    (c, i) => `## ${i + 1}. ${c.titulo}

${c.texto}

\`${c.arquivo}\`, linhas ${c.linha}–${c.linha + c.codigo.split("\n").length - 1}:

\`\`\`${CERCA[c.linguagem]}
${c.codigo}
\`\`\`
`,
  )
  .join("\n")}`;
}

async function main() {
  const args = process.argv.slice(2);
  const valor = (n) => (args.includes(n) ? args[args.indexOf(n) + 1] : undefined);
  const outra = valor("--raiz") ? resolve(valor("--raiz")) : null;
  const fonte = JSON.parse(await readFile(join(aqui, "..", "roteiros", "tour-do-codigo.fonte.json"), "utf8"));
  const conteudos = new Map();
  for (const p of fonte.partes) {
    const candidatos = [outra && join(outra, p.arquivo), join(raizRepo, p.arquivo)].filter(Boolean);
    const achado = candidatos.find((c) => existsSync(c));
    if (!achado) throw new Error(`${p.arquivo} não encontrado (procurei em: ${candidatos.join(", ")})`);
    conteudos.set(p.arquivo, await readFile(achado, "utf8"));
  }
  const roteiro = montarRoteiro(fonte, (a) => conteudos.get(a));
  const erros = validarRoteiro(roteiro);
  if (erros.length) {
    console.error("Roteiro do tour inválido:\n- " + erros.join("\n- "));
    return 1;
  }
  const destino = join(aqui, "..", "roteiros", `${roteiro.id}.json`);
  await writeFile(destino, JSON.stringify(roteiro, null, 2) + "\n");
  console.log(`roteiro: ${destino} (${roteiro.cenas.length} cenas, ${roteiro.cenas.reduce((s, c) => s + c.duracao, 0)} s)`);
  if (valor("--docs")) {
    await writeFile(resolve(valor("--docs")), montarDoc(roteiro, fonte));
    console.log(`documento: ${resolve(valor("--docs"))}`);
  }
  return 0;
}

if (import.meta.url === `file://${process.argv[1]}`) process.exitCode = await main();
