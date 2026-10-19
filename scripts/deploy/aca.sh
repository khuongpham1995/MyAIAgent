#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 5 ]]; then
  echo 'Usage: aca.sh staging|production <gateway-image> <orchestrator-image> <kernel-memory-image> <mcp-image>' >&2
  exit 2
fi

stage=$1
if [[ "$stage" != staging && "$stage" != production ]]; then
  echo 'Stage must be staging or production.' >&2
  exit 2
fi

: "${RESOURCE_GROUP:?}"
: "${ACR_NAME:?}"
: "${ACR_RESOURCE_GROUP:?}"
: "${APP_ENVIRONMENT_NAME:?}"
: "${KEY_VAULT_NAME:?}"
: "${RUNTIME_IDENTITY_NAME:?}"
: "${ENTRA_TENANT_ID:?}"
: "${ENTRA_AUDIENCE:?}"
: "${AZURE_OPENAI_ENDPOINT:?}"
: "${AZURE_OPENAI_DEPLOYMENT:?}"
: "${AZURE_EMBEDDING_DEPLOYMENT:?}"
: "${AZURE_TEXT_DEPLOYMENT:?}"
: "${GOOGLE_SEARCH_ENGINE_ID:?}"
: "${SEARCH_ENDPOINT:?}"

az deployment group create \
  --only-show-errors \
  --name "myaiagent-${stage}-${GITHUB_RUN_ID:-local}" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/apps.bicep \
  --parameters \
    stage="$stage" \
    registryName="$ACR_NAME" \
    registryResourceGroup="$ACR_RESOURCE_GROUP" \
    environmentName="$APP_ENVIRONMENT_NAME" \
    vaultName="$KEY_VAULT_NAME" \
    identityName="$RUNTIME_IDENTITY_NAME" \
    tenantId="$ENTRA_TENANT_ID" \
    audience="$ENTRA_AUDIENCE" \
    azureOpenAiEndpoint="$AZURE_OPENAI_ENDPOINT" \
    azureOpenAiDeployment="$AZURE_OPENAI_DEPLOYMENT" \
    azureEmbeddingDeployment="$AZURE_EMBEDDING_DEPLOYMENT" \
    azureTextDeployment="$AZURE_TEXT_DEPLOYMENT" \
    googleSearchEngineId="$GOOGLE_SEARCH_ENGINE_ID" \
    searchEndpoint="$SEARCH_ENDPOINT" \
    publicChatIssuer="${PUBLIC_CHAT_ISSUER:-}" \
    publicChatAudience="${PUBLIC_CHAT_AUDIENCE:-}" \
    publicChatJwksUri="${PUBLIC_CHAT_JWKS_URI:-}" \
    publicChatJwtVaultEntry="${PUBLIC_CHAT_SIGNING_KEY_SECRET:-}" \
    publicChatAgentId="${PUBLIC_CHAT_AGENT_ID:-}" \
    publicChatAllowedOrigin="${PUBLIC_CHAT_ALLOWED_ORIGIN:-}" \
    imageGateway="$2" \
    imageOrchestrator="$3" \
    imageKernelMemory="$4" \
    imageMcp="$5" \
  --query properties.outputs.gatewayFqdn.value \
  --output tsv
