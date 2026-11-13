# Coreletter no Kubernetes

Manifestos para rodar o Coreletter num cluster local: Kubernetes embutido do Docker Desktop (o caminho sugerido em aula) ou kind. A CI sobe tudo isso num cluster kind a cada push (job `kubernetes`), então os manifestos não são só teoria.

## O que sobe

```
Namespace coreletter
├── ConfigMap coreletter-config      configuração não secreta (ambiente, porta, flags)
├── Secret    coreletter-secrets     senha do banco e connection string (gerado do secrets.env)
├── StatefulSet postgres  + Service postgres (headless) + volume de 1 Gi
├── Deployment coreletter-api (2 réplicas) + Service coreletter-api (ClusterIP :80 → :8080)
│     initContainer "migrate"  aplica as migrações antes da API aceitar tráfego
│     container "api"          API + Angular na mesma origem
└── CronJob coreletter-worker        coletor de notícias a cada 3 h
```

| Conceito da aula | Onde aparece | Por quê |
|---|---|---|
| Pod | criado pelo Deployment, StatefulSet e CronJob | nunca criamos pod solto: quem cria é o controlador que garante o estado desejado |
| Deployment | `base/api.yaml` | "quero 2 cópias desta versão"; se uma cair, o Kubernetes recria |
| Rolling update | `strategy` em `api.yaml` | `maxUnavailable: 0` + `maxSurge: 1`: sobe uma cópia nova, espera ficar pronta, só então derruba uma antiga |
| Service | `api.yaml`, `postgres.yaml` | endereço fixo na frente de pods que mudam de IP; distribui entre as réplicas |
| ConfigMap | `base/configmap.yaml` | a mesma imagem roda em qualquer ambiente; muda só a configuração |
| Secret | `overlays/local` (secretGenerator) | senha fora da imagem e fora do Git |
| Namespace | `base/namespace.yaml` | isola o projeto dentro do cluster |

Além do visto em aula, e esperado em produção:

- **Probes.** `startupProbe` tolera a primeira subida; `livenessProbe` (`/api/health`) reinicia processo travado e **não** consulta o banco, de propósito (banco fora do ar não se resolve reiniciando a API); `readinessProbe` (`/api/health/ready`) tira do Service o pod que não alcança o banco.
- **Migração antes das réplicas.** O initContainer roda `--migrate` e a API sobe com `Database__MigrateOnStartup=false`. Assim a migração acontece uma vez por rollout, e se ela falhar, a versão nova não recebe tráfego.
- **Recursos e segurança.** `requests/limits` (o scheduler sabe onde cabe cada pod), usuário sem privilégio, sistema de arquivos só leitura, capabilities removidas.

## Passo a passo (PowerShell, Docker Desktop)

1. Docker Desktop → Settings → Kubernetes → **Enable Kubernetes** → Apply. Confira:

   ```powershell
   kubectl config use-context docker-desktop
   kubectl get nodes
   ```

2. Na raiz do repositório, construa as duas imagens. O Kubernetes do Docker Desktop enxerga as imagens locais; a tag `local` faz ele não tentar baixar de um registro.

   ```powershell
   docker build -t coreletter-api:local .
   docker build -t coreletter-worker:local -f Dockerfile.worker .
   ```

3. Crie o arquivo de segredos local (ignorado pelo Git) e troque a senha nas duas linhas:

   ```powershell
   Copy-Item k8s/overlays/local/secrets.env.example k8s/overlays/local/secrets.env
   notepad k8s/overlays/local/secrets.env
   ```

4. Aplique e acompanhe:

   ```powershell
   kubectl apply -k k8s/overlays/local
   kubectl -n coreletter get pods -w        # Ctrl+C quando os 2 pods da API estiverem 1/1 Running
   ```

5. Acesse pelo Service:

   ```powershell
   kubectl -n coreletter port-forward svc/coreletter-api 8080:80
   ```

   Abra http://localhost:8080 (aplicação) e http://localhost:8080/swagger (documentação da API).

### Experimentos para entender (e para a apresentação)

```powershell
kubectl -n coreletter delete pod -l app.kubernetes.io/name=coreletter-api --wait=false   # mata as réplicas
kubectl -n coreletter get pods -w                                                       # o Deployment recria
kubectl -n coreletter scale deployment/coreletter-api --replicas=3                      # mais cópias
kubectl -n coreletter rollout restart deployment/coreletter-api                         # atualização gradual
kubectl -n coreletter rollout history deployment/coreletter-api
kubectl -n coreletter logs -l app.kubernetes.io/name=coreletter-api --tail=20           # logs em JSON
kubectl -n coreletter logs <pod-da-api> -c migrate                                      # saída da migração
kubectl -n coreletter create job coleta-agora --from=cronjob/coreletter-worker          # coleta sob demanda
```

Para promover a conta de administração da demonstração: cadastre-a, depois edite `BootstrapAdminEmail` no ConfigMap (`kubectl -n coreletter edit configmap coreletter-config`) e rode `kubectl -n coreletter rollout restart deployment/coreletter-api`. O initContainer promove a conta na próxima subida.

Para remover tudo (inclusive o volume do banco): `kubectl delete namespace coreletter`.

## Com kind (Linux/macOS ou CI)

```bash
kind create cluster --name coreletter
kind load docker-image coreletter-api:local coreletter-worker:local --name coreletter
cp k8s/overlays/local/secrets.env.example k8s/overlays/local/secrets.env
kubectl apply -k k8s/overlays/local
./scripts/Test-K8s.sh
```

## Limites conhecidos

- **Cookie em HTTP.** O ConfigMap usa `Production`, que marca o cookie de sessão como `Secure`. Navegadores aceitam isso em `http://localhost` (port-forward), mas `scripts/Test-Api.ps1` não: o cliente HTTP do PowerShell não reenvia cookie `Secure` sem HTTPS. Para HTTPS de verdade, o próximo passo é um Ingress com TLS (ingress-nginx + cert-manager).
- **Limite de requisições por IP.** Atrás do Service, os pods podem ver o IP do nó em vez do cliente. Com Ingress, habilite `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (como no Render).
- **Banco dentro do cluster** é bom para estudo e demonstração. Em produção, prefira PostgreSQL gerenciado e troque só a connection string no Secret.
- **Sem autoscaling.** Um `HorizontalPodAutoscaler` exige o metrics-server; fica como evolução.
