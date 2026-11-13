# 0004 — Manifestos Kubernetes e coletor como CronJob

Data: 12/11/2026. Estado: proposta na PR `feat/arquitetura-hexagonal-k8s`, aguardando revisão.

## Contexto
A disciplina cobriu Kubernetes (cluster, control plane, nodes, pods, Deployment, Service, ConfigMap, Secret, Namespace) e o professor sugeriu, como dica para o TCC, subir a API num cluster local. A demonstração pública continua no Render (0001); Kubernetes aqui é o caminho de estudo, de apresentação e de evolução.

## Decisão
- `k8s/base` com Kustomize (embutido no `kubectl`, sem instalar Helm): Namespace, ConfigMap, StatefulSet do PostgreSQL com volume, Deployment da API com 2 réplicas e Service, CronJob do coletor.
- `k8s/overlays/local` para Docker Desktop e kind: imagens com tag `local` e Secret gerado de `secrets.env` (ignorado pelo Git, com modelo `.example`).
- **Migração no initContainer** com `--migrate`; a API sobe com `Database__MigrateOnStartup=false`. O padrão `true` continua valendo para Render e para quem roda local.
- **Probes separados:** liveness sem banco (`/api/health`), readiness com banco (`/api/health/ready`), startup para a primeira subida.
- **Hardening padrão:** usuário sem privilégio, raiz só leitura com `/tmp` em `emptyDir`, capabilities removidas, `requests/limits`.
- **Coletor como CronJob** (`Dockerfile.worker`), mesmo horário do workflow `ingestao.yml`. Os dois podem coexistir: a escrita é idempotente (`ON CONFLICT DO NOTHING`).
- **Prova na CI:** job `kubernetes` sobe um cluster kind, aplica o overlay e roda `scripts/Test-K8s.sh` (rollout das 2 réplicas, migração, Service, cadastro, rolling update, coleta sob demanda).

## Alternativas consideradas
| Opção | Por que não agora |
|---|---|
| Helm chart | templating e mais uma ferramenta; Kustomize resolve dois ambientes com YAML puro |
| Migrar na inicialização de cada réplica | duas réplicas competem pela migração e uma falha de migração derruba pods que estavam bons |
| Banco gerenciado fora do cluster | é o certo em produção; para estudo local, ter tudo no cluster facilita o `kubectl apply` único |
| Ingress com TLS | exige controlador e certificado; documentado como próximo passo no `k8s/README.md` |

## Consequências e limites
- Cookie `Secure` em Production: navegador aceita em `http://localhost` via port-forward, mas o `Test-Api.ps1` não. Por isso o teste do cluster usa `curl`/`Test-K8s.sh`.
- Sem HPA (exige metrics-server) e sem NetworkPolicy; evoluções possíveis.
- A validação em cluster real é o job `kubernetes` da CI (kind). Ele já pegou um erro que a validação de esquema não pega: o Secret gerado ficava fora do namespace. Mantê-lo obrigatório antes do merge.
