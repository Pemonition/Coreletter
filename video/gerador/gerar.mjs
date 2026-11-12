#!/usr/bin/env node
// Uso (na raiz do repositório):
//   node video/gerador/gerar.mjs video/roteiros/como-funciona.json
//   node video/gerador/gerar.mjs --de-comparacao comparacao.json [--id minha-historia]
// Saída: video/composicoes/<id>/index.html + fontes/. Depois: ver video/README.md (preview e render).
import { readFile, writeFile, mkdir, copyFile, readdir } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { composicao, roteiroDeComparacao, validarRoteiro } from "./lib.mjs";

const aqui = dirname(fileURLToPath(import.meta.url));
const raizVideo = resolve(aqui, "..");
const args = process.argv.slice(2);
const valor = (nome) => (args.includes(nome) ? args[args.indexOf(nome) + 1] : undefined);

async function main() {
  let roteiro;
  const deComparacao = valor("--de-comparacao");
  if (deComparacao) {
    const json = JSON.parse(await readFile(deComparacao, "utf8"));
    roteiro = roteiroDeComparacao(json.comparison ?? json, { id: valor("--id") });
    const destino = join(raizVideo, "roteiros", `${roteiro.id}.json`);
    await writeFile(destino, JSON.stringify(roteiro, null, 2) + "\n");
    console.log(`roteiro gerado: ${destino} (revise o texto antes de renderizar)`);
  } else {
    const arquivo = args.find((a) => !a.startsWith("--"));
    if (!arquivo) {
      console.error("Informe o roteiro: node video/gerador/gerar.mjs video/roteiros/<id>.json");
      return 2;
    }
    roteiro = JSON.parse(await readFile(arquivo, "utf8"));
  }
  const erros = validarRoteiro(roteiro);
  if (erros.length) {
    console.error("Roteiro inválido:\n- " + erros.join("\n- "));
    return 1;
  }
  const saida = join(raizVideo, "composicoes", roteiro.id);
  await mkdir(join(saida, "fontes"), { recursive: true });
  await writeFile(join(saida, "index.html"), composicao(roteiro));
  for (const f of await readdir(join(raizVideo, "marca", "fontes"))) await copyFile(join(raizVideo, "marca", "fontes", f), join(saida, "fontes", f));
  const total = roteiro.cenas.reduce((s, c) => s + c.duracao, 0);
  console.log(`composição: ${join(saida, "index.html")} (${roteiro.cenas.length} cenas, ${total} s)`);
  return 0;
}

process.exitCode = await main();
