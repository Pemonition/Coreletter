# 0003 — Backend em arquitetura hexagonal (portas e adapters)

Data: 12/11/2026. Estado: proposta na PR `feat/arquitetura-hexagonal-k8s`, aguardando revisão.

## Contexto
Toda a API estava em dois arquivos: `Program.cs` (255 linhas, todos os endpoints com regra, validação e consulta ao banco misturadas) e `Domain.cs` (entidades, DbContext, regras e cálculos financeiros juntos). Funcionava, mas:
- regra de negócio só podia ser testada subindo HTTP + PostgreSQL; os testes unitários existentes cobriam só funções puras;
- quatro pessoas iam editar o mesmo `Program.cs` ao mesmo tempo (conflito de merge garantido nas próximas tarefas: planos, comparação, quotas);
- a disciplina apresentou arquitetura hexagonal como forma de organizar o que já fazíamos.

## Decisão
Quatro projetos, dependência sempre apontando para o centro:

```
RadarPlural.Api ──► RadarPlural.Application ──► RadarPlural.Domain
       │                                            ▲
       └──────► RadarPlural.Infrastructure ─────────┘
```

| Projeto | Papel na hexagonal | Contém |
|---|---|---|
| `Domain` | núcleo | entidades, regras (`LinkRules`, `PostTypes`, limites), `Billing/` (RN01), **portas** (`IPostRepository`, `IMemberRepository`, `IPasswordHasher`...) |
| `Application` | casos de uso | `AuthService`, `PostService`, `NewsService`, `ModerationService`, DTOs, `Result<T>` |
| `Infrastructure` | adapters de saída | `AppDb`, repositórios EF Core, migrações, hash de senha (Identity), health check do banco |
| `Api` | adapter de entrada | endpoints por recurso (`Endpoints/`), cookie de sessão, ProblemDetails, middlewares; `Program.cs` só compõe |

Regras adotadas:
- **Erro esperado é valor, não exceção.** Caso de uso devolve `Result<T>` com `ErrorKind` (Validation, Unauthorized, Forbidden, NotFound); a Api traduz para HTTP em um só lugar (`ResultMapping`). Exceção fica para o inesperado e cai no handler global (500 sem stack trace para o cliente).
- **Identidade vem da sessão.** O caso de uso recebe `memberId` do adapter web, que lê do cookie assinado. Nunca do corpo.
- **Contrato HTTP não mudou.** Rotas, status, DTOs e o campo `error` continuam iguais; o corpo de erro ganhou campos do ProblemDetails (`type`, `title`, `status`, `traceId`) sem remover nada.

## Por que hexagonal e não Clean Architecture completa
Clean Architecture acrescentaria camadas (interactors, presenters, request/response models por caso de uso). A própria aula alertou que pode ser excesso para projeto pequeno. Hexagonal entrega o que precisamos agora (domínio testável sem banco, adapters trocáveis, arquivos separados por responsabilidade) com quatro projetos que a equipe consegue explicar.

## Alternativas consideradas
| Opção | Por que não |
|---|---|
| Manter tudo em `Program.cs` | conflito de merge entre 4 pessoas; regra só testável com banco |
| Só separar em pastas no mesmo projeto | nada impede a regra de usar EF diretamente; o compilador não protege a fronteira |
| Controllers MVC | minimal APIs com `MapGroup` já agrupam por recurso; trocar exigiria reescrever sem ganho |
| MediatR / CQRS | dependência e indireção extra para 4 serviços pequenos |
| AutoMapper | mapeamento manual (`PostDto.From`) é explícito, rápido e sem configuração escondida |

## Consequências
- Testes de caso de uso rodam com **adapters em memória** (`RadarPlural.Tests/Application`), sem banco, em milissegundos. O comportamento real com PostgreSQL fica em `RadarPlural.IntegrationTests`.
- Nova funcionalidade segue o caminho: entidade/porta no Domain → caso de uso na Application → repositório na Infrastructure → endpoint na Api. Exemplo: planos (RN01) entram como `Domain/Billing` + `Application/Plans` + `PlanEndpoints`.
- Migrações agora ficam em `backend/RadarPlural.Infrastructure/Persistence/Migrations`. Comando:
  `dotnet ef migrations add Nome --project backend/RadarPlural.Infrastructure --startup-project backend/RadarPlural.Api`
- Os nomes técnicos continuam `RadarPlural.*` (ver HANDOFF). Renomear para `Coreletter.*` é mudança mecânica separada, de preferência quando não houver PR aberta.
