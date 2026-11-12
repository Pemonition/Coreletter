import { readFile, mkdir, writeFile, copyFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const escape = value => value.replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
function text(value, limit) {
  if (typeof value !== 'string' || !value.trim() || value.length > limit) throw new Error('Texto ausente ou excede o limite');
  return escape(value);
}
export function compose(data) {
  if (data?.version !== 1 || data.simulated !== true) throw new Error('Somente roteiro demonstrativo v1 aceito');
  if (!Array.isArray(data.scenes) || data.scenes.length < 1 || data.scenes.length > 6) throw new Error('Use de 1 a 6 cenas');
  const title = text(data.title, 100);
  let start = 0;
  const scenes = data.scenes.map((scene, i) => {
    const heading = text(scene.heading, 100), body = text(scene.body, 400);
    if (!Number.isInteger(scene.seconds) || scene.seconds < 3 || scene.seconds > 15) throw new Error('Duração inválida');
    if (!Array.isArray(scene.sources) || scene.sources.length > 3) throw new Error('Fontes inválidas');
    const sources = scene.sources.map(source => {
      const url = new URL(source.url);
      if (url.protocol !== 'https:' || url.username || url.password) throw new Error('Fonte deve usar HTTPS sem credenciais');
      return `<p>${text(source.name, 80)} · ${text(source.url, 220)}</p>`;
    }).join('');
    const html = `<section id="scene-${i}" class="clip" data-start="${start}" data-duration="${scene.seconds}" data-track-index="${i}"><div class="content"><small>CORELETTER / ${i + 1}</small><h1>${heading}</h1><p class="body">${body}</p><footer>${sources}</footer></div></section>`;
    start += scene.seconds;
    return html;
  }).join('');
  return `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>${title}</title><style>
  *{box-sizing:border-box}body{margin:0;background:#101827;color:#f5f7fa;font-family:Arial,sans-serif}#root{width:1920px;height:1080px;position:relative;overflow:hidden}.clip{position:absolute;inset:0;background:#101827}.content{padding:130px 200px}small{font-size:30px;color:#6ee7b7;letter-spacing:5px}h1{font-size:86px;line-height:1.1;margin:40px 0;max-width:1480px}.body{font-size:42px;line-height:1.4;max-width:1450px}footer{font-size:30px;color:#a9bdd2;overflow-wrap:anywhere}footer p{margin:12px 0}.label{position:absolute;bottom:65px;left:200px;font-size:30px;color:#6ee7b7;z-index:20}
  </style></head><body><main id="root" data-composition-id="coreletter" data-start="0" data-duration="${start}" data-width="1920" data-height="1080">${scenes}<div class="label">DEMONSTRAÇÃO · conteúdo didático · sem auditoria real de IA</div></main><script src="assets/gsap.min.js"></script><script>window.__timelines = window.__timelines || {}; const tl = gsap.timeline({paused:true}); document.querySelectorAll('.clip').forEach(scene => { tl.from(scene.querySelector('.content'), {opacity:0, y:24, duration:0.5}, Number(scene.dataset.start)); }); tl.to({}, {duration:0.001}, ${start - 0.001}); window.__timelines['coreletter'] = tl;</script></body></html>`;
}
export async function generate(input, output) {
  const html = compose(JSON.parse(await readFile(input, 'utf8')));
  await mkdir(output, {recursive:true});
  // Refuse to overwrite an existing composition edited in HyperFrames.
  await writeFile(resolve(output, 'index.html'), html, {flag:'wx'});
  await mkdir(resolve(output, 'assets'), {recursive:true});
  await copyFile(createRequire(import.meta.url).resolve('gsap/dist/gsap.min.js'), resolve(output, 'assets/gsap.min.js'));
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  await generate(process.argv[2] ?? 'demo.json', process.argv[3] ?? 'output');
}
