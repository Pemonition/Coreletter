# Padrões de projeto e polimorfismo no Coreletter

Guia para a banca: cada padrão aparece **no nosso código**, com o porquê. Na apresentação, qualquer integrante pode ser perguntado sobre qualquer parte (Regulamento, seção 10).

## Polimorfismo
**O que é:** o mesmo código chama um método sem saber qual implementação concreta vai rodar.

| Onde | Como |
|---|---|
| `IStageHandler` (`RadarPlural.Pipeline/Stages.cs`) | O `StageRunner` chama `HandleAsync` sem saber se é auditoria, roteiro ou vídeo. Uma etapa nova é uma classe nova, e o executor não muda |
| `IStoryQueue` | O `StageRunner` usa a fila real (`PgStoryQueue`) em produção e uma fila falsa (`FakeQueue`) nos testes, com o mesmo código |
| `StageOutcome` (`Done` / `Retry`) | Resultado como tipos diferentes; o `switch` por tipo trata cada caso (polimorfismo com *pattern matching* do C#) |
| `HttpMessageHandler` nos testes do coletor | `SafeFeedFetcher` recebe um `HttpClient`; nos testes, um handler falso responde sem internet |

## Padrões de projeto (GoF e de arquitetura)
| Padrão | Onde | Por quê |
|---|---|---|
| **Strategy** | `IAuditor`: auditor simulado (B05) × Gemini (B09) | Trocar o provedor de IA sem mexer no resto; a demo roda sem chave |
| **Adapter** (portas e adaptadores) | `PgStoryQueue` implementa `IStoryQueue`; `FeedItemStore` grava no PostgreSQL | O núcleo não conhece banco; trocar o banco = trocar o adaptador |
| **Repository** | `FeedItemStore`, `StoryStore`, `AppDb` | Acesso a dados num lugar só |
| **Template Method** (na forma de composição) | `StageRunner.RunOnceAsync`: reservar → processar → concluir/falhar é sempre igual; só o "processar" varia | Regra de tentativas e travas escrita uma vez |
| **Dependency Injection** | `Program.cs` da API; construtores primários (`StageRunner(queue, handler, workerId, clock)`) | Testável: o relógio (`TimeProvider`) e a fila são injetados |
| **State machine** | Estados da história: `agrupada → auditada → roteirizada → video_pronto` (`StoryStatus`) | Cada etapa só pega o estado de entrada dela; falha não perde trabalho |
| **Retry com backoff exponencial** | `RetryPolicy` (1, 2, 4… min até 1 h) | Provedor fora do ar não vira avalanche de chamadas |
| **Lease + concorrência otimista** | `PgStoryQueue` (`LeaseUntil`, `Version`) | Várias réplicas sem pegar a mesma história; trabalho em versão velha não avança |
| **Union-Find** (algoritmo) | `StoryClusterer` | Junta manchetes por ligação transitiva (A~B e B~C ⇒ A, B, C) |
| **Guard / validação na borda** | `UrlGuard`, `Triage`, validadores do contrato B04 | Dado externo é não confiável: valida antes de entrar |

## Princípios SOLID onde aparecem
- **S** (responsabilidade única): `Triage` só decide descarte; `StoryClusterer` só agrupa; `StoryReconciler` só mantém ids.
- **O** (aberto/fechado): etapa nova = nova implementação de `IStageHandler`, sem editar o `StageRunner`.
- **L** (substituição de Liskov): a `FakeQueue` dos testes substitui a `PgStoryQueue` sem o executor perceber.
- **I** (segregação de interfaces): `IStoryReader`, `IAuditor`, `IAuditStore` separados, em vez de um "serviço de IA" gigante.
- **D** (inversão de dependência): o `Pipeline` define as interfaces e a `Infrastructure` implementa. `ArchitectureTests` garante isso.
