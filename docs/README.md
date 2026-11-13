# Documentação do Coreletter

Comece por aqui. Cada projeto da solução tem um README com o que faz, em que camada está e quem cuida, para dividir o trabalho e a explicação na banca.

## Mapa da solução (`backend/RadarPlural.slnx` + `frontend/`)
| Projeto | Camada (arquitetura limpa) | Dono proposto | README |
|---|---|---|---|
| `RadarPlural.Domain` | Domínio (regras de negócio: rentabilidade) | Davidy | `backend/RadarPlural.Domain/README.md` |
| `RadarPlural.Ingestion` | Domínio (regras da coleta) | Hugo | `backend/RadarPlural.Ingestion/README.md` |
| `RadarPlural.Pipeline` | Aplicação (casos de uso e portas) | Hugo · Davidy (auditor) | `backend/RadarPlural.Pipeline/README.md` |
| `RadarPlural.Infrastructure` | Infraestrutura (PostgreSQL) | Hugo | `backend/RadarPlural.Infrastructure/README.md` |
| `RadarPlural.Api` | Entrada web (WebApi) | Davidy · Henrique | `backend/RadarPlural.Api/README.md` |
| `RadarPlural.Worker` | Entrada (linha de comando) | Hugo | `backend/RadarPlural.Worker/README.md` |
| `RadarPlural.Tests` | Testes | Todos | `backend/RadarPlural.Tests/README.md` |
| `frontend/` | Interface (Angular) | Lindomar | `frontend/README.md` |
| `video/` | Vídeos (HyperFrames) | Hugo · Davidy (etapa no Worker) | `video/README.md` |
| `deploy/k8s/` | Implantação | Hugo (todos praticam) | `deploy/k8s/README.md` |

Regra de dependência: domínio e aplicação não conhecem banco nem web. Quem implementa as portas é a infraestrutura. `ArchitectureTests` falha se isso for quebrado.

## Comece por aqui
| Documento | Para quê |
|---|---|
| `PROPOSTA.md` | **A proposta "de pessoas para pessoas, sem algoritmo"** conferida contra o código: o que cumpre, o que falta e o plano |
| `COMO-MOSTRAR.md` | Onde ver o projeto (demo, mapa, vídeos) e como apresentar na banca |
| `TOUR.md` | Tour pelo código: cada parte com o trecho real (gerado pelo mesmo script do vídeo) |
| `../video/README.md` | Vídeos com HyperFrames: como gerar e renderizar |

## Para entender o projeto
| Documento | Para quê |
|---|---|
| `ARQUITETURA.md` | Visão geral e limites do produto |
| `PIPELINE.md` | Como cada etapa funciona e por quê (PR #24) |
| `RENTABILIDADE.md` | Regras RN01–RN04 com números (Ficha, Seção 3.3) |
| `PADROES.md` | Padrões de projeto e polimorfismo no nosso código |
| `AULAS.md` | Aulas de Kubernetes e arquitetura aplicadas ao projeto (PR #24) |
| `BACKLOG.md`, `EQUIPE.md` | Tarefas B01–B10 e combinados |
| `HANDOFF.md` | Estado atual e próxima sessão |
| `APRESENTACAO.md` | Roteiro da demonstração (PR #24) |

## Decisões (o porquê de cada escolha)
| # | Decisão |
|---|---|
| 0001 | Deploy gratuito no Render e PostgreSQL (não MongoDB) |
| 0002 | Política de ingestão: só RSS/Atom de fontes aprovadas, travas de segurança |
| 0003 | Pipeline: triagem por regras, auditor separado do roteirista, vídeo por história (PR #24) |
| 0004 | Escala desde o MVP: sem estado, fila com `SKIP LOCKED`, migração única, teto de custo |

## Contratos
`backend/API.md` (HTTP) e `contratos/comparacao-v1.md` (comparação B04, PR #24).
