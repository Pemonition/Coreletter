# RadarPlural.Pipeline: casos de uso e portas (Application)

**Camada:** aplicação. Define **o que** o pipeline faz e **as interfaces (portas)** que os adaptadores implementam. Sem dependência de banco, web ou IA (`ArchitectureTests` garante).
**Dono proposto:** Hugo (executor) e Davidy (porta do auditor, B05).

## O que tem aqui
| Arquivo | Conteúdo |
|---|---|
| `Stages.cs` | Estados da história, definição das etapas (auditoria, roteiro, vídeo), portas `IStoryQueue` e `IStageHandler` |
| `StageRunner.cs` | Executa um lote de uma etapa; `RetryPolicy` (1, 2, 4… min até 1 h) |
| `Ports.cs` | Portas: `IStoryReader`, `IAuditor`, `IAuditStore` |
| `AuditStage.cs` | Etapa de auditoria (lê, chama o auditor, valida pelo contrato, grava) |
| `Comparison/Contract.cs` | Tipos do contrato v1 e `ContractValidator` (citação literal, sem veredito, ids conhecidos) |
| `Comparison/CompareStory.cs` | Caso de uso "comparar agora": cache 24 h, reserva de quota (RN03), devolução se o provedor falhar |

## Como adicionar uma etapa (ex.: auditor simulado da B05)
1. Implemente `IAuditor` num adaptador (simulado: respostas de `docs/contratos/exemplos/`).
2. Crie um `IStageHandler` para `Stages.Auditoria` que lê a história (`IStoryReader`), chama o auditor e grava (`IAuditStore`).
3. Registre o handler no `RunStageAsync` do Worker.
4. Rode `--etapa auditoria`. Para escalar no Kubernetes, suba as réplicas do `coreletter-auditoria`.

O `StageRunner` não muda: é o princípio aberto/fechado (`docs/PADROES.md`).

## Por quê
`docs/decisoes/0003` (etapas separadas, estado no banco) e `0004` (escala com `SKIP LOCKED`).
