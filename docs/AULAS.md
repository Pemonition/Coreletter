# Notas das aulas aplicadas ao Coreletter

Resumo das aulas gravadas (transcritas do áudio) e **o que cada ponto muda no projeto**. Os minutos indicados são das gravações do Hugo.

## Kubernetes — aula 1 (09/11)
| Conceito (minuto) | O que o professor disse | No Coreletter |
|---|---|---|
| Cluster (00:41) | Conjunto de máquinas que trabalham juntas; **control plane** decide o que roda e onde; **nodes** executam | Ambiente local (Docker Desktop/kind) para estudo; Render continua sendo a demo pública |
| Componentes (03:44) | API server (tudo passa por ele), etcd (estado atual e desejado), scheduler (escolhe o node), controller manager (corrige diferença) | Explicar na banca com o desenho do pipeline |
| Ciclo de reconciliação (06:38) | Observa → compara com o desejado → cria/remove pods, sempre | Se o worker de auditoria cair, sobe outro sozinho |
| Pod (07:43) | O Kubernetes nunca roda container solto: põe numa "travessa"; containers do mesmo pod dividem rede e disco | Um pod por etapa; render de vídeo isolado (Chrome + FFmpeg pesados) |
| Deployment (10:07) | **Declarativo**: "quero 3 cópias da versão 2"; troca as cópias uma a uma sem parar | API com 2+ réplicas e atualização sem tirar do ar |
| Rollback (14:50) | `kubectl rollout undo` volta para a versão anterior | Plano B da apresentação |
| CI/CD (13:30) | Testes para saber o máximo/mínimo da aplicação + GitHub Actions + Docker → deploy automático | Já temos CI com Docker; falta teste de carga |

## Kubernetes — aula 2 (09/11)
| Conceito (minuto) | O que o professor disse | No Coreletter |
|---|---|---|
| Service (00:00) | Endereço fixo na frente dos pods (ClusterIP interno, NodePort, LoadBalancer público) | Service da API; workers sem Service (não recebem requisição) |
| YAML (02:47) | `kind`, `metadata`, `replicas`, `image` | Manifests em `deploy/k8s/` (a criar) |
| ConfigMap e Secret (03:45) | Configuração fora da imagem; **segredo nunca na imagem** | `ConnectionStrings__Default` e chave do Gemini como Secret (já não estão no Git) |
| Namespace (04:16) | Separar dev, teste e produção no mesmo cluster | `coreletter-dev` e `coreletter-demo` |
| Comandos (04:45) | `kubectl get pods`, `apply`, `logs`, `delete` | Roteiro da demo |
| Microsserviços (06:48) | Cada parte um deployment com réplicas diferentes (site 3, pedidos 2, pagamento 2, banco 1); se pagamento cai, o site continua | Mesma lógica: API, coletor, auditoria e render escalam separados; banco fora do cluster |
| Praticar de graça (11:25) | **Docker Desktop já tem Kubernetes**: ativar nas configurações | Primeiro passo de cada integrante |
| Quando vale a pena (12:51) | Muitos serviços, tráfego variável, alta disponibilidade, muitos deploys. **Exagero**: site simples, equipe sem tempo para operar | Avaliação abaixo |
| Dica para o TCC (13:38) | Mostrar que o app é para grande público: **teste de carga** (30, 50… até 15 mil usuários simultâneos) e **escala automática presa a limite de custo** | Vira a regra de rentabilidade de infraestrutura (abaixo) |
| Tarefa (17:48) | "Pensem se o aplicativo de vocês comporta ou não trabalhar com Kubernetes" | Avaliação abaixo, para o grupo discutir |

## Kubernetes — dica para o TCC (09/11, gravação curta)
Começar com um cluster **bem simples**, subir aquela API "hello world" da aula e pôr mais um pod no mesmo node para ver os dois se comunicando. Pode usar IA para chegar no produto final e depois fazer **engenharia reversa** para aprender. O professor ia mandar um PDF com os comandos.

## Arquitetura hexagonal e limpa — aula de C# (11/11)
| Conceito | O que o professor disse | No Coreletter |
|---|---|---|
| Domain | Entidades **com as regras de negócio** (ex.: `Product.Create` lança `RuleViolation`); não depende de nada | `RadarPlural.Ingestion` já é quase isso: `Triage`, `StoryClusterer`, `FeedDates` são regras puras, sem banco nem web |
| Portas | Interfaces: de saída (repositório, gateway de pagamento, notificador) e de entrada (casos de uso) | `IAuditor`, `IRoteirista`, `IGeradorVideo`, `IEntregador` (decisão 0003) |
| Adaptadores | Implementação concreta (EF, HTTP, em memória para teste); trocáveis via injeção de dependência | Auditor simulado × Gemini; `FeedItemStore` (Npgsql) é adaptador de saída |
| Limpa × hexagonal | Limpa = Domain, Application, Infra, WebApi; mais robusta, **pode ser exagero em projeto pequeno** | Usar hexagonal leve: não reorganizar tudo agora, aplicar nas partes novas (comparação, roteiro, vídeo) |
| Projetos ou pastas | Cada camada pode ser um projeto da solução ou só uma pasta | Já usamos projetos separados (Api, Ingestion, Worker, Tests) |

**Dívida reconhecida:** `RadarPlural.Ingestion` mistura regra pura (triagem, agrupamento) com adaptador (`SafeFeedFetcher`, que faz HTTP). Separar quando a comparação entrar; não vale refatorar só por isso agora.

---

## O Coreletter comporta Kubernetes? (para o grupo discutir)
| Critério do professor | Coreletter | Peso |
|---|---|---|
| Muitos serviços | API, coletor, triagem/agrupamento, auditoria, render de vídeo: cargas bem diferentes | A favor |
| Tráfego variável | Notícia grande gera pico de acesso; render de vídeo é pesado e em rajadas | A favor |
| Alta disponibilidade | Desejável; para o TCC, não crítica | Neutro |
| Muitos deploys | Sim, durante as 3 semanas | A favor |
| Equipe com tempo para operar | 4 pessoas, 3 semanas, núcleo ainda em construção | **Contra** |

**Proposta:**
- **Não** migrar a demo pública para Kubernetes; o Render continua.
- Fazer um **deploy alternativo local** em `deploy/k8s/`, para estudo e banca, rodando no Docker Desktop:
  - API como `Deployment` com 2 réplicas + `Service`;
  - coletor como `CronJob`;
  - Secret para a conexão, ConfigMap para a configuração;
  - `HorizontalPodAutoscaler` com mínimo e máximo de réplicas;
  - limites de CPU e memória por pod.
- **Teste de carga** (ex.: k6) mostrando a escala automática.

### Regra de rentabilidade de infraestrutura (dica do professor)
O Kubernetes não tem um limite de custo em reais embutido, mas o **teto é calculável**:

```
custo máximo mensal = réplicas máximas × custo por pod-hora × 730 h
```

Exemplo, com números hipotéticos: máximo de 5 réplicas × R$ 0,10 por pod-hora × 730 h = **R$ 365/mês de teto**. A regra testável:
- o `maxReplicas` do autoscaler nunca pode gerar teto acima de X% da receita prevista dos planos;
- o teste falha se alguém aumentar as réplicas sem rever o preço.

Combina com a regra do custo de vídeo (decisão 0003, seção 5).
