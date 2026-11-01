/**
 * MyAIAgent | Azure Infrastructure
 *
 * Deploys four Container Apps with separate identities, secret references, service configuration, and ingress boundaries.
 */

targetScope = 'resourceGroup'

@allowed(['staging', 'production'])
param stage string
param registryName string
param registryResourceGroup string
param environmentName string
param vaultName string
param identityNames object
param appConfigurationEndpoint string
param cosmosEndpoint string
param imageGateway string
param imageOrchestrator string
param imageKernelMemory string
param imageMcp string
param tenantId string
param audience string
param azureOpenAiEndpoint string
param azureOpenAiDeployment string
param azureEmbeddingDeployment string
param azureTextDeployment string
// Nonempty values override AAC; otherwise public chat can be configured through AAC.
param publicChatIssuer string = ''
param publicChatAudience string = ''
param publicChatJwksUri string = ''
// Name of a Key Vault secret holding an HS256 key; leave empty when publicChatJwksUri is used instead.
param publicChatJwtVaultEntry string = ''
param publicChatAgentId string = ''
param publicChatAllowedOrigin string = ''
// Endpoint of the Azure AI Search service from search.bicep (https://<name>.search.windows.net).
param searchEndpoint string
param location string = resourceGroup().location

var prefix = 'myaiagent-${stage}'
// Staging uses the test database and index, production the prod ones (see cosmos.bicep and search.bicep).
var dataName = stage == 'production' ? 'myaiagent-prod' : 'myaiagent-test'
var gatewayName = '${prefix}-gateway'
var orchestratorName = '${prefix}-orchestrator'
var kernelMemoryName = '${prefix}-kernelmemory'
var mcpName = '${prefix}-mcpserver'
var mcpBaseUrl = 'https://${mcpName}.internal.${environment.properties.defaultDomain}'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
  scope: resourceGroup(registryResourceGroup)
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: environmentName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: vaultName
}

var services = [
  {
    name: gatewayName
    identityName: identityNames.gateway
    configurationPrefix: 'Gateway:'
    image: imageGateway
    external: true
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: concat(
      [
        { name: 'kernel-memory-key', vaultSecret: 'kernel-memory-key' }
        { name: 'orchestrator-key', vaultSecret: 'orchestrator-key' }
        { name: 'mcp-key', vaultSecret: 'mcp-key' }
      ],
      empty(publicChatJwtVaultEntry) ? [] : [
        { name: 'public-chat-signing-key', vaultSecret: publicChatJwtVaultEntry }
      ]
    )
    env: concat([
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'Authentication__TenantId', value: tenantId }
      { name: 'Authentication__Audience', value: audience }
      { name: 'Orchestrator__BaseUrl', value: 'http://${orchestratorName}' }
      { name: 'KernelMemory__BaseUrl', value: 'http://${kernelMemoryName}' }
      { name: 'McpServer__AccessKey', secretRef: 'mcp-key' }
      { name: 'KernelMemory__AccessKey', secretRef: 'kernel-memory-key' }
      { name: 'Orchestrator__AccessKey', secretRef: 'orchestrator-key' }
      // The Container Apps ingress is the only way in, so its X-Forwarded-For entry identifies the client.
      { name: 'PublicChat__TrustForwardedHeaders', value: 'true' }
    ], filter([
      { name: 'PublicChat__Jwt__Issuer', value: publicChatIssuer }
      { name: 'PublicChat__Jwt__Audience', value: publicChatAudience }
      { name: 'PublicChat__Jwt__JwksUri', value: publicChatJwksUri }
      { name: 'PublicChat__AgentId', value: publicChatAgentId }
      { name: 'PublicChat__AllowedOrigins__0', value: publicChatAllowedOrigin }
    ], setting => !empty(setting.value)), empty(publicChatJwtVaultEntry) ? [] : [
      { name: 'PublicChat__Jwt__SigningKey', secretRef: 'public-chat-signing-key' }
    ])
  }
  {
    name: orchestratorName
    identityName: identityNames.orchestrator
    configurationPrefix: 'Orchestrator:'
    image: imageOrchestrator
    external: false
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: [
      { name: 'azure-openai-key', vaultSecret: 'azure-openai-key' }
      { name: 'orchestrator-key', vaultSecret: 'orchestrator-key' }
      { name: 'orchestrator-rotation-key', vaultSecret: 'orchestrator-rotation-key' }
      { name: 'mcp-key', vaultSecret: 'mcp-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'CosmosDb__Endpoint', value: cosmosEndpoint }
      { name: 'CosmosDb__DatabaseName', value: dataName }
      { name: 'ServiceAuthorization__Enabled', value: 'true' }
      { name: 'ServiceAuthorization__AccessKey1', secretRef: 'orchestrator-key' }
      { name: 'ServiceAuthorization__AccessKey2', secretRef: 'orchestrator-rotation-key' }
      { name: 'AzureOpenAI__Endpoint', value: azureOpenAiEndpoint }
      { name: 'AzureOpenAI__DeploymentName', value: azureOpenAiDeployment }
      { name: 'AzureOpenAI__ApiKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__BaseUrl', value: 'http://${kernelMemoryName}' }
      { name: 'McpServer__AccessKey', secretRef: 'mcp-key' }
    ]
  }
  {
    name: kernelMemoryName
    identityName: identityNames.kernelmemory
    configurationPrefix: 'KernelMemory:'
    image: imageKernelMemory
    external: false
    cpu: 1
    memory: '2Gi'
    maxReplicas: 1
    secrets: [
      { name: 'azure-openai-key', vaultSecret: 'azure-openai-key' }
      { name: 'blob-connection', vaultSecret: 'blob-connection' }
      { name: 'search-admin-key', vaultSecret: 'search-admin-key' }
      { name: 'kernel-memory-key', vaultSecret: 'kernel-memory-key' }
      { name: 'kernel-memory-rotation-key', vaultSecret: 'kernel-memory-rotation-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'CosmosDb__Endpoint', value: cosmosEndpoint }
      { name: 'KernelMemory__DocumentStorageType', value: 'AzureBlobs' }
      { name: 'KernelMemory__ContentStorageType', value: 'AzureBlobs' }
      { name: 'KernelMemory__Services__AzureBlobs__Auth', value: 'ConnectionString' }
      { name: 'KernelMemory__Services__AzureBlobs__ConnectionString', secretRef: 'blob-connection' }
      { name: 'CosmosDb__DatabaseName', value: dataName }
      { name: 'KernelMemory__DefaultIndexName', value: dataName }
      { name: 'KernelMemory__Services__AzureAISearch__Endpoint', value: searchEndpoint }
      { name: 'KernelMemory__Services__AzureAISearch__APIKey', secretRef: 'search-admin-key' }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__Endpoint', value: azureOpenAiEndpoint }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__Deployment', value: azureEmbeddingDeployment }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__APIKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__Services__AzureOpenAIText__Endpoint', value: azureOpenAiEndpoint }
      { name: 'KernelMemory__Services__AzureOpenAIText__Deployment', value: azureTextDeployment }
      { name: 'KernelMemory__Services__AzureOpenAIText__APIKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__ServiceAuthorization__AccessKey1', secretRef: 'kernel-memory-key' }
      { name: 'KernelMemory__ServiceAuthorization__AccessKey2', secretRef: 'kernel-memory-rotation-key' }
    ]
  }
  {
    name: mcpName
    identityName: identityNames.mcpserver
    configurationPrefix: 'McpServer:'
    image: imageMcp
    external: false
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: [
      { name: 'tavily-api-key', vaultSecret: 'tavily-api-key' }
      { name: 'mcp-key', vaultSecret: 'mcp-key' }
      { name: 'mcp-rotation-key', vaultSecret: 'mcp-rotation-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'Tavily__ApiKey', secretRef: 'tavily-api-key' }
      { name: 'ServiceAuthorization__Enabled', value: 'true' }
      { name: 'ServiceAuthorization__AccessKey1', secretRef: 'mcp-key' }
      { name: 'ServiceAuthorization__AccessKey2', secretRef: 'mcp-rotation-key' }
    ]
  }
]

resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [for service in services: {
  name: service.identityName
}]

resource apps 'Microsoft.App/containerApps@2024-03-01' = [for (service, index) in services: {
  name: service.name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identities[index].id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: service.external
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identities[index].id
        }
      ]
      secrets: [for secret in service.secrets: {
        name: secret.name
        keyVaultUrl: '${vault.properties.vaultUri}secrets/${secret.vaultSecret}'
        identity: identities[index].id
      }]
    }
    template: {
      containers: [
        {
          name: service.name
          image: service.image
          resources: {
            cpu: service.cpu
            memory: service.memory
          }
          env: concat(service.env, [
            { name: 'AzureIdentity__ClientId', value: identities[index].properties.clientId }
            { name: 'AppConfiguration__Endpoint', value: appConfigurationEndpoint }
            { name: 'AppConfiguration__KeyPrefix', value: service.configurationPrefix }
            { name: 'AppConfiguration__Label', value: stage }
          ], index < 2 ? [
            // Keep runtime environment properties out of the services array used to declare resource names.
            { name: 'McpServer__BaseUrl', value: mcpBaseUrl }
          ] : [])
          probes: [
            {
              type: 'Startup'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              periodSeconds: 5
              failureThreshold: 12
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: service.maxReplicas
      }
    }
  }
}]

output gatewayFqdn string = apps[0].properties.configuration.ingress.fqdn
