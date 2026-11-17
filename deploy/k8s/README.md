# Kubernetes local (Docker Desktop)

Deploy **alternativo** para estudo e para a banca. A demo pública continua no Render. Decisões: `docs/decisoes/0004-escala-desde-o-mvp.md`; conceitos das aulas: `docs/AULAS.md`.

## O que sobe
| Arquivo | Objeto | Por quê |
|---|---|---|
| `00-namespace.yaml` | Namespace `coreletter` | Isola o projeto no cluster |
| `01-config.yaml` | ConfigMap | Configuração que não é segredo |
| `02-migrate-job.yaml` | Job | Migra o banco **uma vez**, antes das réplicas |
| `10-api.yaml` | Deployment + Service + HPA + PDB | API com 2–5 réplicas, sondas de vivo/pronto, troca gradual |
| `20-coletor-cronjob.yaml` | CronJob | Coleta, triagem e agrupamento a cada 3 h, uma execução por vez |
| `30-pipeline-auditoria.yaml` | Deployment | Auditoria em N réplicas (0 até existir o adaptador da B05) |
| `custo.json` | — | Preços e teto mensal usados pelo teste de teto de custo |

## Passo a passo
1. Docker Desktop → *Settings* → *Kubernetes* → **Enable Kubernetes**. Confira: `kubectl get nodes`.
2. Construa as imagens na raiz do repositório:
   ```powershell
   docker build -t coreletter-api:local .
   docker build -f Dockerfile.worker -t coreletter-worker:local .
   ```
3. Banco: use o PostgreSQL do `compose.yaml` (`docker compose up -d db`). De dentro do cluster ele é `host.docker.internal:5433`.
4. Namespace, configuração e **segredo** (nunca versionar o segredo):
   ```powershell
   kubectl apply -f deploy/k8s/00-namespace.yaml -f deploy/k8s/01-config.yaml
   kubectl -n coreletter create secret generic coreletter-secrets `
     --from-literal=ConnectionStrings__Default="Host=host.docker.internal;Port=5433;Database=radarplural;Username=radarplural;Password=<a do .env>"
   ```
5. Migração, esperando terminar:
   ```powershell
   kubectl apply -f deploy/k8s/02-migrate-job.yaml
   kubectl -n coreletter wait --for=condition=complete job/coreletter-migrate --timeout=120s
   ```
6. O resto: `kubectl apply -k deploy/k8s`
7. Acesse: `kubectl -n coreletter port-forward svc/coreletter-api 8080:80` → http://localhost:8080. O cookie de sessão é `Secure`, e o navegador aceita isso em `localhost`.

## Comandos da aula aplicados
```powershell
kubectl -n coreletter get pods                          # réplicas
kubectl -n coreletter logs deploy/coreletter-api        # logs
kubectl -n coreletter get hpa                           # escala automática
kubectl -n coreletter create job --from=cronjob/coreletter-coletor coleta-agora   # coletar já
kubectl -n coreletter set image deploy/coreletter-api api=coreletter-api:v2      # nova versão, troca gradual
kubectl -n coreletter rollout undo deploy/coreletter-api                         # voltar versão
```

## Ver a escala automática
O HPA precisa do *metrics-server* (Docker Desktop não traz por padrão): `kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml`. Se ele reclamar de certificado, adicione `--kubelet-insecure-tls` ao Deployment dele (só no cluster local). Depois rode carga (`scripts/carga/k6-feed.js`) e observe `kubectl -n coreletter get hpa -w`.

Para medir capacidade, aumente o limite por IP **só neste ambiente**: `kubectl -n coreletter set env deploy/coreletter-api RateLimit__PermitPerMinute=1000000`.

## Teto de custo
`backend/RadarPlural.Tests/Infra/CostCeilingTests.cs` lê estes arquivos e `custo.json`, calcula réplicas máximas × limites × preço e **falha se passar do teto**. Mudou réplicas ou limites? Rode `dotnet test` antes do PR. Os preços são hipótese: troque pelos do provedor escolhido.
