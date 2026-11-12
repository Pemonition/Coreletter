// Gerador de vídeos do Coreletter: roteiro (JSON) → composição HyperFrames (HTML + GSAP).
// Sem dependências: roda com o Node que já usamos no front (24+). Documentação: video/README.md.
//
// Por que um gerador e não escrever o HTML à mão a cada vídeo?
//  1. Marca única: cores, fonte, rodapé e animações ficam num lugar só (a função composicao).
//  2. Segurança: TODO texto passa por escapar(). O roteiro pode vir de IA (roteirista, #19);
//     sem escapar, um título com "<script>" viraria código dentro do vídeo.
//  3. O pipeline (auditor → roteirista → vídeo, docs/decisoes/0003) precisa gerar vídeo sem humano no meio.

export const VERSAO_ROTEIRO = 1;
export const STATUS = ["concordante", "divergente", "unica_fonte", "evidencia_insuficiente"];
export const ROTULO_STATUS = {
  concordante: "Concordam",
  divergente: "Divergem",
  unica_fonte: "Só uma fonte",
  evidencia_insuficiente: "Sem evidência suficiente",
};
const TIPOS = ["abertura", "texto", "etapa", "comparacao", "fechamento"];
const LIMITES = { titulo: 70, texto: 180, item: 70, itens: 4, afirmacoes: 4, fontes: 4, cenas: 20, total: 180 };

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
  return `<!doctype html>
<!-- GERADO por video/gerador/gerar.mjs a partir de video/roteiros/${id}.json. Não edite aqui: edite o roteiro e gere de novo. -->
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <title>${escapar(roteiro.titulo)}</title>
  <style>
    @font-face { font-family: "DM Sans"; src: url("fontes/dm-sans-latin-400-normal.woff2") format("woff2"); font-weight: 400; }
    @font-face { font-family: "DM Sans"; src: url("fontes/dm-sans-latin-700-normal.woff2") format("woff2"); font-weight: 700; }
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
    .marca { position: absolute; top: ${vertical ? 64 : 48}px; left: ${vertical ? 9 : 6}%; font-size: 30px; font-weight: 700; color: ${m.verde}; }
  </style>
</head>
<body>
  <div id="${id}" data-composition-id="${id}" data-start="0" data-duration="${total}" data-width="${largura}" data-height="${altura}">
    <div class="marca">coreletter</div>
${cenas}
    <div class="rodape"><div class="barra-progresso"></div></div>
  </div>
  <script src="https://cdn.jsdelivr.net/npm/gsap@3.12.5/dist/gsap.min.js"></script>
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
}
