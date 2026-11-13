#!/usr/bin/env bash
# Teste de fumaça do Coreletter num cluster Kubernetes local (kind ou Docker Desktop).
# Pré-requisito: overlay aplicado (kubectl apply -k k8s/overlays/local). Usado pelo job "kubernetes" da CI.
set -euo pipefail
NS=coreletter

echo "== Aguardando banco e API (2 réplicas)"
kubectl -n $NS rollout status statefulset/postgres --timeout=180s
kubectl -n $NS rollout status deployment/coreletter-api --timeout=300s
ready=$(kubectl -n $NS get deployment coreletter-api -o jsonpath='{.status.readyReplicas}')
[ "$ready" = "2" ] || { echo "Esperado 2 réplicas prontas, há $ready"; exit 1; }

echo "== initContainer de migração terminou com sucesso"
for pod in $(kubectl -n $NS get pods -l app.kubernetes.io/name=coreletter-api -o name); do
  code=$(kubectl -n $NS get "$pod" -o jsonpath='{.status.initContainerStatuses[0].state.terminated.exitCode}')
  [ "$code" = "0" ] || { echo "$pod: migrate saiu com $code"; kubectl -n $NS logs "$pod" -c migrate; exit 1; }
done

echo "== Service responde (port-forward)"
kubectl -n $NS port-forward svc/coreletter-api 18080:80 >/tmp/port-forward.log 2>&1 &
PF=$!
trap 'kill $PF 2>/dev/null || true' EXIT
for _ in $(seq 1 30); do curl -sf http://localhost:18080/api/health >/dev/null && break; sleep 1; done
curl -sf http://localhost:18080/api/health/ready | grep -q Healthy
curl -sf http://localhost:18080/ | grep -q "<app-root>"
curl -sf http://localhost:18080/openapi/v1.json | grep -q '"/api/posts"'
email="k8s-$(date +%s)@example.invalid"
status=$(curl -s -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email\",\"password\":\"senha-k8s-12345\",\"displayName\":\"K8s\"}" http://localhost:18080/api/auth/register)
[ "$status" = "200" ] || { echo "Cadastro devolveu $status"; exit 1; }

echo "== Atualização gradual (rolling update) conclui com as 2 réplicas prontas"
# maxUnavailable=0: o Kubernetes só derruba um pod antigo depois que o novo passa no readinessProbe.
kubectl -n $NS rollout restart deployment/coreletter-api
kubectl -n $NS rollout status deployment/coreletter-api --timeout=300s

echo "== Coletor (CronJob) executado sob demanda"
kubectl -n $NS create job worker-smoke --from=cronjob/coreletter-worker
kubectl -n $NS wait --for=condition=complete job/worker-smoke --timeout=600s || {
  kubectl -n $NS logs job/worker-smoke || true
  echo "Aviso: coletor não concluiu (sem rede externa ou todas as fontes falharam)."
}
kubectl -n $NS logs job/worker-smoke | tail -5 || true

echo "OK: Coreletter funcionando no Kubernetes."
