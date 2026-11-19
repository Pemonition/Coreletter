// Gerador de vídeos do Coreletter: roteiro (JSON) → composição HyperFrames (HTML + GSAP).
// Sem dependências: roda com o Node que já usamos no front (24+). Documentação: video/README.md.
//
// Por que um gerador e não escrever o HTML à mão a cada vídeo?
//  1. Marca única: cores, fonte, rodapé e animações ficam num lugar só (a função composicao).
//  2. Segurança: TODO texto passa por escapar(). O roteiro pode vir de IA (roteirista, #19);
//     sem escapar, um título com "<script>" viraria código dentro do vídeo.
//  3. O pipeline (auditor → roteirista → vídeo, docs/decisoes/0003) precisa gerar vídeo sem humano no meio.

import { createHash } from "node:crypto";

export const VERSAO_ROTEIRO = 1;
export const STATUS = ["concordante", "divergente", "unica_fonte", "evidencia_insuficiente"];
export const ROTULO_STATUS = {
  concordante: "Concordam",
  divergente: "Divergem",
  unica_fonte: "Só uma fonte",
  evidencia_insuficiente: "Sem evidência suficiente",
};
const TIPOS = ["abertura", "texto", "etapa", "comparacao", "codigo", "fechamento"];
const LIMITES = { titulo: 70, texto: 180, item: 70, itens: 4, afirmacoes: 4, fontes: 4, cenas: 20, total: 180, linhasCodigo: 16, colunasCodigo: 200, colunasVisiveis: 92 };
export const LINGUAGENS = ["csharp", "sql", "ts", "js", "yaml"];

// Marca (mesmos tokens de frontend/src/styles.css).
export const MARCA = {
  tinta: "#172c2a",
  fundo: "#f5f7f6",
  verde: "#16775e",
  verdeClaro: "#e2e9e4",
  cinza: "#667085",
  status: {
    concordante: ["#e3f4ea", "#1b7f4b"],
    divergente: ["#fbe5e6", "#b4232d"],
    unica_fonte: ["#fdf0dc", "#8a5000"],
    evidencia_insuficiente: ["#eef0f3", "#344054"],
  },
};

export function escapar(valor) {
  return String(valor)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

// Valida o roteiro e devolve TODOS os problemas de uma vez (mais fácil de corrigir do que um por vez).
export function validarRoteiro(r) {
  const erros = [];
  const texto = (v, campo, max, obrigatorio = true) => {
    if (v === undefined || v === null || v === "") {
      if (obrigatorio) erros.push(`${campo}: obrigatório`);
      return;
    }
    if (typeof v !== "string") return erros.push(`${campo}: deve ser texto`);
    if (v.trim() !== v || v.length === 0) erros.push(`${campo}: sem espaços nas pontas`);
    if (v.length > max) erros.push(`${campo}: até ${max} caracteres (tem ${v.length})`);
    if (/[\r\n]/.test(v)) erros.push(`${campo}: uma linha só (a quebra é automática)`);
  };
  if (!r || typeof r !== "object") return ["roteiro: deve ser um objeto JSON"];
  if (r.versao !== VERSAO_ROTEIRO) erros.push(`versao: deve ser ${VERSAO_ROTEIRO}`);
  if (typeof r.id !== "string" || !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(r.id) || r.id.length > 40)
    erros.push("id: minúsculas, números e hífen (ex.: como-funciona), até 40 caracteres");
  texto(r.titulo, "titulo", LIMITES.titulo);
  const f = r.formato ?? { largura: 1920, altura: 1080 };
  if (![[1920, 1080], [1080, 1920], [1080, 1080]].some(([l, a]) => f.largura === l && f.altura === a))
    erros.push("formato: use 1920x1080 (horizontal), 1080x1920 (vertical) ou 1080x1080 (quadrado)");
  if (!Array.isArray(r.cenas) || r.cenas.length === 0) return [...erros, "cenas: pelo menos uma"];
  if (r.cenas.length > LIMITES.cenas) erros.push(`cenas: até ${LIMITES.cenas}`);
  let total = 0;
  r.cenas.forEach((c, i) => {
    const p = `cenas[${i}]`;
    if (!TIPOS.includes(c?.tipo)) return erros.push(`${p}.tipo: um de ${TIPOS.join(", ")}`);
    if (typeof c.duracao !== "number" || !(c.duracao >= 2 && c.duracao <= 30))
      erros.push(`${p}.duracao: segundos entre 2 e 30`);
    else total += c.duracao;
    texto(c.titulo, `${p}.titulo`, LIMITES.titulo);
    texto(c.texto, `${p}.texto`, LIMITES.texto, c.tipo === "texto");
    if (c.tipo === "etapa" && !(Number.isInteger(c.numero) && c.numero >= 1 && c.numero <= 9))
      erros.push(`${p}.numero: inteiro de 1 a 9`);
    if (c.itens !== undefined) {
      if (!Array.isArray(c.itens) || c.itens.length > LIMITES.itens) erros.push(`${p}.itens: lista com até ${LIMITES.itens}`);
      else c.itens.forEach((it, j) => texto(it, `${p}.itens[${j}]`, LIMITES.item));
    }
    if (c.tipo === "codigo") {
      // Trecho real do repositório: o texto explica o porquê; o código é mostrado como está (só escapado).
      texto(c.texto, `${p}.texto`, LIMITES.texto);
      if (typeof c.arquivo !== "string" || !/^[\w./-]{3,100}$/.test(c.arquivo)) erros.push(`${p}.arquivo: caminho no repositório (ex.: backend/X/Y.cs)`);
      if (!(Number.isInteger(c.linha) && c.linha >= 1)) erros.push(`${p}.linha: número da primeira linha do trecho`);
      if (!LINGUAGENS.includes(c.linguagem)) erros.push(`${p}.linguagem: uma de ${LINGUAGENS.join(", ")}`);
      if (typeof c.codigo !== "string" || !c.codigo.trim()) erros.push(`${p}.codigo: obrigatório`);
      else {
        const linhas = c.codigo.split("\n");
        // Linha longa quebra na tela (com recuo), então o limite é de linhas VISUAIS: o trecho precisa caber.
        const visuais = linhas.reduce((n, l) => n + Math.max(1, Math.ceil(l.length / LIMITES.colunasVisiveis)), 0);
        if (visuais > LIMITES.linhasCodigo) erros.push(`${p}.codigo: até ${LIMITES.linhasCodigo} linhas na tela (tem ${visuais}); escolha o trecho essencial`);
        linhas.forEach((l, j) => {
          if (l.length > LIMITES.colunasCodigo) erros.push(`${p}.codigo linha ${j + 1}: até ${LIMITES.colunasCodigo} caracteres (tem ${l.length})`);
          if (/\t/.test(l)) erros.push(`${p}.codigo linha ${j + 1}: use espaços, não tabulação`);
        });
      }
    }
    if (c.tipo === "comparacao") {
      if (!Array.isArray(c.afirmacoes) || c.afirmacoes.length === 0 || c.afirmacoes.length > LIMITES.afirmacoes)
        erros.push(`${p}.afirmacoes: de 1 a ${LIMITES.afirmacoes}`);
      else
        c.afirmacoes.forEach((a, j) => {
          const q = `${p}.afirmacoes[${j}]`;
          if (!STATUS.includes(a?.status)) erros.push(`${q}.status: um de ${STATUS.join(", ")}`);
          texto(a?.texto, `${q}.texto`, LIMITES.texto);
          if (!Array.isArray(a?.fontes) || a.fontes.length === 0 || a.fontes.length > LIMITES.fontes)
            erros.push(`${q}.fontes: de 1 a ${LIMITES.fontes} nomes de veículo`);
          else a.fontes.forEach((fo, k) => texto(fo, `${q}.fontes[${k}]`, 40));
        });
    }
  });
  if (total > LIMITES.total) erros.push(`duração total: até ${LIMITES.total} s (tem ${total})`);
  return erros;
}

// Roteiro básico a partir de uma comparação (contrato v1, GET /api/stories/{id}/comparison → .comparison).
// É a ponte com o pipeline: o roteirista de verdade (#19) vai escrever texto melhor; este é o mínimo que funciona.
export function roteiroDeComparacao(comparacao, { id, titulo } = {}) {
  const nomes = new Map(comparacao.sources.map((s) => [s.itemId, s.sourceName]));
  const curto = (s, max) => (s.length <= max ? s : s.slice(0, max - 1).trimEnd() + "…");
  const umaLinha = (s) => String(s).replace(/\s+/g, " ").trim();
  const veiculos = [...new Set(comparacao.sources.map((s) => s.sourceName))];
  const tituloHistoria = curto(umaLinha(titulo ?? comparacao.sources[0]?.title ?? "Notícia do dia"), LIMITES.titulo);
  const afirmacoes = comparacao.claims
    .filter((c) => STATUS.includes(c.status))
    .slice(0, LIMITES.afirmacoes)
    .map((c) => ({
      status: c.status,
      texto: curto(umaLinha(c.text), LIMITES.texto),
      fontes: [...new Set([...c.supportedBy, ...c.evidence.map((e) => e.itemId)].map((i) => nomes.get(i)).filter(Boolean))].slice(0, LIMITES.fontes),
    }))
    .map((a) => (a.fontes.length ? a : { ...a, fontes: ["Fonte não identificada"] }));
  const cenas = [
    { tipo: "abertura", duracao: 5, titulo: tituloHistoria, texto: curto(`Contada por ${veiculos.length} veículos: ${veiculos.join(", ")}.`, LIMITES.texto) },
  ];
  if (afirmacoes.length) cenas.push({ tipo: "comparacao", duracao: Math.min(30, 4 + afirmacoes.length * 3), titulo: "O que cada fonte diz", afirmacoes });
  cenas.push({
    tipo: "fechamento",
    duracao: 5,
    titulo: "Leia as matérias completas",
    texto: comparacao.simulated ? "Comparação simulada, feita por regras. Links no Coreletter." : "Links de todas as fontes no Coreletter.",
  });
  return { versao: VERSAO_ROTEIRO, id: id ?? `historia-${String(comparacao.storyId).slice(0, 8)}`, titulo: tituloHistoria, cenas };
}

// Destaque de sintaxe mínimo: comentário, texto entre aspas, número e palavra-chave. Cada pedaço passa por escapar()
// (o trecho pode conter "<" e "&", como em C# genérico ou SQL). Não é um parser: é só para o vídeo ficar legível.
const PALAVRAS = {
  csharp: "public private static async await return var new if else foreach for while in is not and or null true false class record sealed interface using namespace throw try catch decimal int string bool void readonly const with switch case default partial",
  sql: "select from where and or not null insert into values update set on conflict do returning with as order by limit for skip locked is desc asc join",
  ts: "const let async await return if else try catch new function export import from this null true false",
  js: "const let async await return if else try catch new function export import from this null true false",
  yaml: "apiVersion kind metadata spec name",
};
export function destacar(linha, linguagem) {
  const palavras = new Set(PALAVRAS[linguagem].split(" "));
  const comentario = linguagem === "sql" ? "--.*$" : linguagem === "yaml" ? "#.*$" : "//.*$";
  const re = new RegExp(`(${comentario})|("(?:[^"\\\\]|\\\\.)*"|'(?:[^'\\\\]|\\\\.)*')|(\\b\\d[\\d.,]*\\b)|([A-Za-z_][\\w]*)`, "g");
  let html = "", ultimo = 0;
  for (const m of linha.matchAll(re)) {
    html += escapar(linha.slice(ultimo, m.index));
    const [t, com, str, num, pal] = m;
    const cls = com ? "c-com" : str && !(linguagem === "sql" && str.startsWith('"')) ? "c-str" : num ? "c-num" : pal && palavras.has(linguagem === "sql" ? pal.toLowerCase() : pal) ? "c-kw" : null;
    html += cls ? `<span class="${cls}">${escapar(t)}</span>` : escapar(t);
    ultimo = m.index + t.length;
  }
  return html + escapar(linha.slice(ultimo));
}

// Linha do tempo: cada cena começa quando a anterior termina.
export function tempos(roteiro) {
  let inicio = 0;
  return roteiro.cenas.map((c) => {
    const t = { inicio, duracao: c.duracao };
    inicio = Math.round((inicio + c.duracao) * 1000) / 1000;
    return t;
  });
}

function cenaHtml(c, i) {
  const e = escapar;
  const titulo = (cls = "") => `<h2 class="anima titulo ${cls}">${e(c.titulo)}</h2>`;
  const textoP = c.texto ? `<p class="anima texto">${e(c.texto)}</p>` : "";
  const itens = c.itens?.length ? `<ul class="itens">${c.itens.map((it) => `<li class="anima">${e(it)}</li>`).join("")}</ul>` : "";
  let corpo;
  switch (c.tipo) {
    case "abertura":
      corpo = `<div class="aneis"><span></span><span></span></div><div class="anima selo">Coreletter</div><h1 class="anima titulo grande">${e(c.titulo)}</h1>${textoP}`;
      break;
    case "etapa":
      corpo = `<div class="anima numero">${c.numero}</div><div class="coluna">${titulo()}${textoP}${itens}</div>`;
      break;
    case "comparacao":
      corpo = `${titulo()}<div class="afirmacoes">${c.afirmacoes
        .map(
          (a) =>
            `<div class="anima afirmacao ${a.status}"><span class="chip ${a.status}">${e(ROTULO_STATUS[a.status])}</span><p>${e(a.texto)}</p><small>${a.fontes.map(e).join(" · ")}</small></div>`,
        )
        .join("")}</div>`;
      break;
    case "codigo": {
      const linhas = c.codigo.split("\n");
      const largura = String(c.linha + linhas.length - 1).length;
      const recuo = (l) => l.length - l.trimStart().length;
      const pre = linhas
        .map((l, j) => `<div class="l" style="--recuo:${recuo(l)}ch"><span class="ln">${String(c.linha + j).padStart(largura, " ")}</span><span class="tx">${destacar(l.trimStart(), c.linguagem) || " "}</span></div>`)
        .join("");
      corpo = `<div class="anima arquivo">${e(c.arquivo)}<span>:${c.linha}</span></div>${titulo("menor")}${textoP}<div class="anima codigo">${pre}</div>`;
      break;
    }
    case "fechamento":
      corpo = `<div class="aneis"><span></span><span></span></div>${titulo("grande")}${textoP}<div class="anima selo">coreletter</div>`;
      break;
    default:
      corpo = `${titulo()}${textoP}${itens}`;
  }
  return { corpo, classe: `cena-${c.tipo}` };
}

// Composição HyperFrames (regras em video/README.md, seção "Regras do HyperFrames").
export function composicao(roteiro) {
  const erros = validarRoteiro(roteiro);
  if (erros.length) throw new Error("Roteiro inválido:\n- " + erros.join("\n- "));
  const { largura, altura } = roteiro.formato ?? { largura: 1920, altura: 1080 };
  const vertical = altura > largura;
  const ts = tempos(roteiro);
  const total = ts.at(-1).inicio + ts.at(-1).duracao;
  const id = roteiro.id;
  const cenas = roteiro.cenas
    .map((c, i) => {
      const { corpo, classe } = cenaHtml(c, i);
      return `    <section id="${id}-c${i}" class="clip cena ${classe}" data-start="${ts[i].inicio}" data-duration="${ts[i].duracao}" data-track-index="1">
      <div class="conteudo">${corpo}</div>
    </section>`;
    })
    .join("\n");
  // Animações: entrada dos filhos .anima em cascata e saída suave do .conteudo (nunca do .clip: regra do HyperFrames).
  const anima = ts
    .map(
      (t, i) => `${["abertura", "fechamento"].includes(roteiro.cenas[i].tipo) ? `      tl.fromTo("#${id}-c${i} .aneis", { rotation: -25, scale: 0.85 }, { rotation: 0, scale: 1, duration: ${t.duracao}, ease: "none" }, ${t.inicio});\n` : ""}      tl.fromTo("#${id}-c${i} .anima", { opacity: 0, y: 36 }, { opacity: 1, y: 0, duration: 0.6, stagger: 0.18, ease: "power2.out" }, ${t.inicio + 0.15});
      tl.fromTo("#${id}-c${i} .conteudo", { opacity: 1 }, { opacity: 0, duration: 0.35, ease: "power1.in" }, ${Math.max(t.inicio + 0.9, t.inicio + t.duracao - 0.4)});`,
    )
    .join("\n");
  const m = MARCA;
  const chips = Object.entries(m.status)
    .map(([s, [bg, fg]]) => `    .chip.${s} { background: ${bg}; color: ${fg}; }\n    .afirmacao.${s} { border-left-color: ${fg}; }`)
    .join("\n");
  const escala = vertical ? 1.1 : 1;
  // Estilo e fonte de código só entram quando o roteiro tem cena de código (os outros vídeos ficam idênticos).
  const temCodigo = roteiro.cenas.some((c) => c.tipo === "codigo");
  const fonteCodigo = temCodigo
    ? `\n    @font-face { font-family: "JetBrains Mono"; src: url("fontes/jetbrains-mono-latin-400-normal.woff2") format("woff2"); font-weight: 400; }\n    @font-face { font-family: "JetBrains Mono"; src: url("fontes/jetbrains-mono-latin-700-normal.woff2") format("woff2"); font-weight: 700; }`
    : "";
  const cssCodigo = temCodigo
    ? `
    .cena-codigo { padding: ${86 * escala}px 0 ${30 * escala}px; box-sizing: border-box; }
    .cena-codigo .conteudo { width: 84%; gap: ${16 * escala}px; }
    .titulo.menor { font-size: ${60 * escala}px; }
    .cena-codigo .texto { font-size: ${34 * escala}px; max-width: 46em; }
    .arquivo { align-self: flex-start; font-family: "JetBrains Mono", monospace; font-size: ${24 * escala}px; color: ${m.verde}; background: ${m.verdeClaro}; padding: 8px 16px; border-radius: 10px; }
    .arquivo span { color: ${m.cinza}; }
    .codigo { margin: 6px 0 0; background: #10231f; color: #e6efe9; border-radius: 22px; padding: ${28 * escala}px ${34 * escala}px; font-family: "JetBrains Mono", monospace; font-size: ${23 * escala}px; line-height: 1.5; overflow: hidden; font-variant-ligatures: none; font-feature-settings: "liga" 0, "calt" 0; box-shadow: 0 30px 60px -40px #0b1f1a; }
    .codigo .l { display: grid; grid-template-columns: auto 1fr; column-gap: 26px; }
    .codigo .ln { color: #5f7a72; white-space: pre; text-align: right; }
    .codigo .tx { white-space: pre-wrap; overflow-wrap: anywhere; padding-left: calc(var(--recuo) + 2ch); text-indent: -2ch; }
    .c-kw { color: #8fd6b0; font-weight: 700; }
    .c-str { color: #f2c48d; }
    .c-num { color: #b9a6ff; }
    .c-com { color: #7f978f; font-style: italic; }`
    : "";
  const html = `<!doctype html>
<!-- GERADO por video/gerador/gerar.mjs a partir de video/roteiros/${id}.json. Não edite aqui: edite o roteiro e gere de novo. -->
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <title>${escapar(roteiro.titulo)}</title>
  <style>
    @font-face { font-family: "DM Sans"; src: url("fontes/dm-sans-latin-400-normal.woff2") format("woff2"); font-weight: 400; }
    @font-face { font-family: "DM Sans"; src: url("fontes/dm-sans-latin-700-normal.woff2") format("woff2"); font-weight: 700; }${fonteCodigo}
    html, body { margin: 0; width: 100%; height: 100%; background: ${m.fundo}; }
    #${id} { position: relative; width: 100%; height: 100%; overflow: hidden; background: ${m.fundo}; color: ${m.tinta}; font-family: "DM Sans", sans-serif; }
    .cena { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; }
    .conteudo { width: ${vertical ? 82 : 76}%; display: flex; flex-direction: column; gap: ${28 * escala}px; }
    .titulo { margin: 0; font-size: ${84 * escala}px; line-height: 1.1; font-weight: 700; letter-spacing: -0.01em; }
    .titulo.grande { font-size: ${116 * escala}px; }
    .texto { margin: 0; font-size: ${48 * escala}px; line-height: 1.35; color: #3c4f4b; max-width: 26em; }
    .selo { align-self: flex-start; font-size: ${26 * escala}px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; color: ${m.verde}; background: ${m.verdeClaro}; padding: 10px 20px; border-radius: 999px; }
    .cena-etapa .conteudo { flex-direction: ${vertical ? "column" : "row"}; align-items: ${vertical ? "flex-start" : "center"}; gap: ${56 * escala}px; }
    .numero { flex: none; width: ${220 * escala}px; height: ${220 * escala}px; border-radius: 50%; background: ${m.verde}; color: #fff; font-size: ${120 * escala}px; font-weight: 700; display: flex; align-items: center; justify-content: center; }
    .coluna { display: flex; flex-direction: column; gap: ${22 * escala}px; }
    .itens { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: ${16 * escala}px; font-size: ${40 * escala}px; color: #3c4f4b; }
    .itens li::before { content: "— "; color: ${m.verde}; font-weight: 700; }
    .afirmacoes { display: flex; flex-direction: column; gap: ${20 * escala}px; }
    .afirmacao { background: #fff; border: 1px solid #e3e6ea; border-left: 8px solid #98a2b3; border-radius: 18px; padding: ${22 * escala}px ${30 * escala}px; display: flex; flex-direction: column; gap: 10px; }
    .afirmacao p { margin: 0; font-size: ${40 * escala}px; line-height: 1.3; }
    .afirmacao small { font-size: ${26 * escala}px; color: ${m.cinza}; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
    .chip { align-self: flex-start; font-size: ${26 * escala}px; font-weight: 700; padding: 6px 16px; border-radius: 999px; }
${chips}
    .conteudo > :not(.aneis) { position: relative; }
    .aneis { position: absolute; right: ${vertical ? -30 : -14}%; top: 50%; width: ${vertical ? 90 : 48}%; aspect-ratio: 1; margin-top: ${vertical ? -40 : -24}%; }
    .aneis span { position: absolute; inset: 0; border-radius: 50%; border: 3px solid ${m.verdeClaro}; }
    .aneis span + span { inset: 18%; background: ${m.verdeClaro}; opacity: 0.55; border: 0; }
    .rodape { position: absolute; left: 0; right: 0; bottom: 0; height: 10px; background: ${m.verdeClaro}; }
    .barra-progresso { width: 100%; height: 100%; background: ${m.verde}; transform-origin: 0 50%; }
    .marca { position: absolute; top: ${vertical ? 64 : 48}px; left: ${vertical ? 9 : 6}%; font-size: 30px; font-weight: 700; color: ${m.verde}; }${cssCodigo}
  </style>
</head>
<body>
  <div id="${id}" data-composition-id="${id}" data-start="0" data-duration="${total}" data-width="${largura}" data-height="${altura}">
    <div class="marca">coreletter</div>
${cenas}
    <div class="rodape"><div class="barra-progresso"></div></div>
  </div>
  <script src="assets/gsap.min.js"></script>
  <script>
    (function () {
      const tl = gsap.timeline({ paused: true });
      tl.fromTo(".barra-progresso", { scaleX: 0 }, { scaleX: 1, duration: ${total}, ease: "none" }, 0);
${anima}
      window.__timelines = window.__timelines || {};
      window.__timelines["${id}"] = tl;
    })();
  </script>
</body>
</html>
`;
  return `${html}<!-- assinatura:${assinatura(html)} -->\n`;
}

// Proteção contra perder edição feita no HyperFrames Studio (ideia da PR #31): o gerador grava a assinatura do
// HTML que produziu; se o arquivo em disco não bate mais com ela, alguém editou à mão e o gerador não sobrescreve.
function assinatura(html) {
  return createHash("sha256").update(html).digest("hex").slice(0, 16);
}
export function foiEditada(arquivo) {
  const m = arquivo.match(/<!-- assinatura:([0-9a-f]{16}) -->\n?$/);
  if (!m) return true;
  return assinatura(arquivo.slice(0, m.index)) !== m[1];
}
