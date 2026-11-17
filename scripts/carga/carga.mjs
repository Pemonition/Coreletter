// Gerador de carga mínimo em Node (sem dependências), usado para medir a linha de base registrada em
// docs/decisoes/0004. Para a equipe, prefira o k6-feed.js.
//   node scripts/carga/carga.mjs http://localhost:5080 10,50,100 15
const [base = 'http://localhost:5080', levelsArg = '10,50,100', secondsArg = '15'] = process.argv.slice(2);
const paths = ['/api/stories?minSources=2', '/api/news?pageSize=20', '/api/stories?page=2', '/api/health/ready'];
const pct = (s, p) => s[Math.min(s.length - 1, Math.floor((p / 100) * s.length))];

for (const users of levelsArg.split(',').map(Number)) {
  const lat = []; let ok = 0, fail = 0, i = 0;
  const end = Date.now() + Number(secondsArg) * 1000;
  await Promise.all(Array.from({ length: users }, async () => {
    while (Date.now() < end) {
      const t = performance.now();
      try {
        const r = await fetch(base + paths[i++ % paths.length]);
        await r.arrayBuffer();
        r.status === 200 ? ok++ : fail++;
      } catch { fail++; }
      lat.push(performance.now() - t);
    }
  }));
  lat.sort((a, b) => a - b);
  console.log(JSON.stringify({ usuarios: users, requisicoes: ok + fail, rps: Math.round((ok + fail) / Number(secondsArg)),
    erros: fail, p50_ms: +pct(lat, 50).toFixed(1), p95_ms: +pct(lat, 95).toFixed(1), p99_ms: +pct(lat, 99).toFixed(1) }));
}
