// Teste de carga das leituras públicas (feed de histórias e notícias) com k6: https://k6.io
// Uso (na máquina de cada integrante ou contra o Service do Kubernetes via port-forward):
//   k6 run -e BASE_URL=http://localhost:5080 scripts/carga/k6-feed.js
// IMPORTANTE: a API limita requisições por IP (60/min). Para medir capacidade, suba a API com
// RateLimit__PermitPerMinute=1000000 num ambiente de teste — nunca em produção.
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5080';

export const options = {
  // Rampa: 10 → 50 → 100 usuários simultâneos (dica do professor: mostrar dezenas a milhares).
  stages: [
    { duration: '20s', target: 10 },
    { duration: '30s', target: 50 },
    { duration: '30s', target: 100 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],     // menos de 1% de erro
    http_req_duration: ['p(95)<500'],   // 95% das respostas abaixo de 500 ms
  },
};

const paths = ['/api/stories?minSources=2', '/api/news?pageSize=20', '/api/stories?page=2', '/api/health/ready'];

export default function () {
  const res = http.get(`${BASE}${paths[Math.floor(Math.random() * paths.length)]}`);
  check(res, { 'status 200': (r) => r.status === 200 });
  sleep(Math.random() * 0.5); // usuário real não clica sem parar
}
