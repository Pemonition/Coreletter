# 0004 — Escalável desde o MVP

Data: 12/11/2026. Estado: proposta na PR `feat/escala-pipeline`. Complementa 0003 (pipeline) e `docs/AULAS.md` (Kubernetes e arquitetura limpa).

## Contexto
A equipe decidiu construir o MVP já pensando na versão final e em escala. Escalar aqui significa **rodar mais cópias de cada parte** (API, etapas do pipeline) sem mudar código, e saber **quanto isso custa no máximo**.

## Princípios
1. **Processos sem estado.** Nada importante fica na memória de uma réplica: sessão em cookie, chaves de proteção no banco, trabalho do pipeline no banco. Qualquer réplica atende qualquer requisição.
2. **Estado do trabalho no banco, com trava por linha.** Etapas do pipeline reservam histórias com `FOR UPDATE SKIP LOCKED` + *lease*. N réplicas processam em paralelo sem duplicar trabalho e sem broker externo (RabbitMQ/Kafka ficam para quando houver gargalo medido).
3. **Tarefas únicas continuam únicas.** Coleta e agrupamento rodam uma vez por vez (`CronJob` com `concurrencyPolicy: Forbid`); escalar isso não traz ganho e cria disputa.
4. **Migração é um passo, não efeito colateral.** Uma vez, antes das réplicas (`--migrate-only`, Job `coreletter-migrate`).
5. **Toda parte tem limite e teto.** Cada contêiner declara limites de CPU/memória; o autoscaler tem máximo; um teste calcula o custo máximo mensal e falha acima do teto.
6. **Arquitetura limpa nas partes novas.** Regras puras (`RadarPlural.Ingestion`) e casos de uso/portas (`RadarPlural.Pipeline`) não conhecem banco nem web; adaptadores em `RadarPlural.Infrastructure`. `ArchitectureTests` falha se a regra de dependência for quebrada.

## O que foi construído (com evidência)
| Peça | Evidência |
|---|---|
| `StageRunner` + `RetryPolicy` (1, 2, 4… min até 1 h; desiste após N tentativas) | 18 testes unitários (RED → GREEN) |
| `PgStoryQueue` (SKIP LOCKED, lease, versão) | 7 testes de integração com PostgreSQL real; **4 workers disputando 60 histórias, nenhuma repetida**, 5/5 execuções; **sem o `SKIP LOCKED` o teste falha 3/3** (mutação) |
| Versão da história | Agrupamento que muda as notícias sobe a versão e devolve a história ao início; trabalho sobre versão antiga não avança (`Stale`) e é liberado na hora |
| Worker `--etapa <nome> [--continuo] [--lote N]` | Encerra limpo com SIGTERM; nome da réplica = `HOSTNAME` do pod. Etapas sem adaptador saem com código 4 e mensagem clara |
| `/api/health` (vivo) × `/api/health/ready` (pronto) | Banco fora: vivo 200, pronto 503 em 0,03 s; banco de volta: pronto 200 |
| `--migrate-only` e `Database__MigrateOnStartup` | **Bug encontrado:** 2 instâncias subindo juntas, uma caiu com `column "Attempts" already exists` (a trava do EF é tomada depois de ler as pendentes). Com migração em passo único: **3 réplicas juntas, 0 erros** |
| `deploy/k8s` | Namespace, ConfigMap, Job de migração, API (2–5 réplicas, HPA, sondas, PDB, troca gradual), CronJob do coletor, Deployment da auditoria (0 réplicas até B05). Validado na CI com `kubeconform` |
| Teto de custo (`custo.json` + `CostCeilingTests`) | Máximo atual **R$ 693,25/mês** (API 5× R$ 428,88; auditoria 3× R$ 257,33; coletor R$ 7,05) ≤ teto R$ 800. Mutação `maxReplicas: 9` → R$ 1.036,35 e o teste falha. **Preços são hipótese** |
| `Dockerfile.worker` | Build reproduzido passo a passo; CI constrói e executa |
| Teste de carga | Ver abaixo |

## Linha de base de carga (12/11/2026)
API em Release, presa a **1 núcleo** de CPU, PostgreSQL local com 202 notícias e 131 histórias, gerador próprio (`scripts/carga/carga.mjs`) em outro núcleo, leituras públicas (`/api/stories`, `/api/news`, `/api/health/ready`), limite de requisições desligado só para medir:

| Usuários simultâneos | Req/s | Erros | p50 | p95 | p99 |
|---|---|---|---|---|---|
| 10 | 468 | 0 | 20 ms | 32 ms | 42 ms |
| 50 | 486 | 0 | 101 ms | 149 ms | 180 ms |
| 100 | 452 | 0 | 218 ms | 338 ms | 392 ms |

Leitura: **1 núcleo satura em ~480 req/s**; acima disso cresce a fila (latência), não o erro. O limite de cada pod em `deploy/k8s` é 0,5 núcleo, então ~240 req/s por réplica e ~1.200 req/s com 5 réplicas, **se o banco acompanhar** (não medido; o banco gratuito do Render é o gargalo provável). Máquina de teste com 2 núcleos: o gerador disputa com o sistema; números servem de ordem de grandeza. Para a banca, repetir com `scripts/carga/k6-feed.js` no cluster local.

## Pontos que ainda NÃO escalam (e o que fazer)
| Ponto | Problema | Proposta | Dono |
|---|---|---|---|
| `UsageBudget` (quota/custo de IA) | Fica na memória do processo: cada réplica conta a sua (limite vira N×) e zera ao reiniciar | Tabela de uso por usuário/mês e reserva atômica: `UPDATE ... SET used = used + 1 WHERE user = $1 AND used < quota RETURNING ...` + chave de idempotência única | B07 (Davidy) |
| Limite de requisições | Memória de cada réplica: limite efetivo até N× | Aceitável no TCC; produção: limite no Ingress ou contador compartilhado | Documentado |
| IP do usuário atrás do proxy | Sem cabeçalhos encaminhados, todos os usuários dividem o mesmo limite | `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` no ConfigMap (Service é ClusterIP, só o proxy alcança a API) | Feito |
| Banco único | Gargalo final de leitura | Índices já cobrem as consultas; réplica de leitura/cache só com medição | Futuro |
| Base antiga da API (`Domain.cs`) | Entidades e regras dentro do projeto web | Extrair para `RadarPlural.Domain` depois do merge de #11/#24/#25 | Issue |

## Limites desta entrega
- Testes de arquitetura foram escritos depois do código (são trava de proteção, não TDD); a mutação mostra que pegam violação.
- `kubeconform` e `k6` não puderam rodar no ambiente de desenvolvimento (binário externo bloqueado); `kubeconform` roda na CI, `k6` fica para a equipe.
- Imagens Docker não foram construídas localmente (sem daemon); a CI constrói as duas.
