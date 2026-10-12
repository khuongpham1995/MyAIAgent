#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo 'Usage: smoke.sh <gateway-fqdn> <Entra-audience>' >&2
  exit 2
fi

gateway="https://$1"
audience=$2

for attempt in {1..30}; do
  code=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --max-time 10 "$gateway/health/ready" || true)
  if [[ "$code" == 200 ]]; then
    break
  fi
  if [[ "$attempt" == 30 ]]; then
    echo "Gateway readiness failed: HTTP $code" >&2
    exit 1
  fi
  sleep 10
done

anonymous_code=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
  --max-time 10 "$gateway/admin/agents")
if [[ "$anonymous_code" != 401 ]]; then
  echo "Anonymous admin access was not rejected: HTTP $anonymous_code" >&2
  exit 1
fi

token=$(az account get-access-token --resource "api://$audience" --query accessToken --output tsv)
for attempt in {1..30}; do
  response=$(mktemp)
  authenticated_code=$(curl --silent --show-error --output "$response" --write-out '%{http_code}' \
    --max-time 30 --header "Authorization: Bearer $token" "$gateway/admin/agents" || true)
  if [[ "$authenticated_code" == 200 ]] && jq --exit-status '.isSuccess == true' "$response" >/dev/null; then
    rm "$response"
    break
  fi
  rm "$response"
  if [[ "$attempt" == 30 ]]; then
    echo "Authenticated admin smoke test failed: HTTP $authenticated_code" >&2
    exit 1
  fi
  sleep 10
done

echo 'Gateway readiness and authorization smoke tests passed.'
