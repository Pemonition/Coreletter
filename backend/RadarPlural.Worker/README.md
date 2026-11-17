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
