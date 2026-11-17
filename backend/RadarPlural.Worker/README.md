# RadarPlural.Worker: processos em segundo plano

**Camada:** entrada (linha de comando) e composição: liga regras, casos de uso e adaptadores.
**Dono proposto:** Hugo.

## Modos
| Comando | O que faz | Onde roda |
|---|---|---|
| *(sem argumentos)* | Coleta as fontes, triagem, retriagem das últimas 72 h e agrupamento | GitHub Actions a cada 3 h; `CronJob` no Kubernetes |
| `--dry-run` | Mostra o que coletaria, descartaria e agruparia, sem banco | Local |
| `--fonte <id>` | Só uma fonte de `fontes.json` | Local |
| `--etapa <nome> [--continuo] [--lote N]` | Processa uma etapa do pipeline; pode rodar em N réplicas | `Deployment` no Kubernetes |

Variável obrigatória (menos no `--dry-run`): `ConnectionStrings__Default`. Imagem: `Dockerfile.worker`.

Códigos de saída: 0 ok · 1 todas as fontes falharam · 2 configuração · 3 banco · 4 etapa sem adaptador.

## Fontes (`fontes.json`)

Catálogo único e curado: 30 fontes em 7 áreas, 26 em português (decisão `docs/decisoes/0005-catalogo-unico-de-fontes.md`).

- Cada fonte declara `topic` (área), `language`, `kind` (tipo) e `reason` (por que entrou). Sem esses campos, o `SourceCatalog` recusa e o teste falha.
- **Antes de incluir uma fonte**, valide a partir de um data center (CI ou nuvem), onde o coletor roda de verdade:
  `dotnet run --project backend/RadarPlural.Worker -- --fontes <arquivo> --dry-run`
- Fonte que responde 403 a robô ou a data center fica fora. Não contornar (decisão 0002).

