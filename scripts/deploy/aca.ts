import { appendFileSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runProcess, type ProcessRunner } from '../security/process.ts';
import { gatewayHostname, requiredEnvironment, UsageError } from './cli.ts';

const requiredSettings = {
  registryName: 'ACR_NAME', registryResourceGroup: 'ACR_RESOURCE_GROUP', environmentName: 'APP_ENVIRONMENT_NAME',
  vaultName: 'KEY_VAULT_NAME', appConfigurationEndpoint: 'APP_CONFIG_ENDPOINT', cosmosEndpoint: 'COSMOS_ENDPOINT',
  tenantId: 'ENTRA_TENANT_ID', audience: 'ENTRA_AUDIENCE', azureOpenAiEndpoint: 'AZURE_OPENAI_ENDPOINT',
  azureOpenAiDeployment: 'AZURE_OPENAI_DEPLOYMENT', azureEmbeddingDeployment: 'AZURE_EMBEDDING_DEPLOYMENT',
  azureTextDeployment: 'AZURE_TEXT_DEPLOYMENT', googleSearchEngineId: 'GOOGLE_SEARCH_ENGINE_ID', searchEndpoint: 'SEARCH_ENDPOINT',
} as const;
const optionalSettings = {
  publicChatIssuer: 'PUBLIC_CHAT_ISSUER', publicChatAudience: 'PUBLIC_CHAT_AUDIENCE', publicChatJwksUri: 'PUBLIC_CHAT_JWKS_URI',
  publicChatJwtVaultEntry: 'PUBLIC_CHAT_SIGNING_KEY_SECRET', publicChatAgentId: 'PUBLIC_CHAT_AGENT_ID',
  publicChatAllowedOrigin: 'PUBLIC_CHAT_ALLOWED_ORIGIN',
} as const;

export function deployContainerApps(args: string[], { env = process.env, run = runProcess }:
  { env?: NodeJS.ProcessEnv; run?: ProcessRunner } = {}): string {
  const [stage, gateway, orchestrator, memory, mcp] = args;
  if (args.length !== 5 || !['staging', 'production'].includes(stage) || args.some(value => !value)) {
    throw new UsageError('Usage: npm run deploy:aca -- staging|production <gateway-image> <orchestrator-image> <kernel-memory-image> <mcp-image>');
  }
  const resourceGroup = requiredEnvironment(env, 'RESOURCE_GROUP');
  const parameters: Record<string, { value: string | Record<string, string> }> = {
    stage: { value: stage }, imageGateway: { value: gateway }, imageOrchestrator: { value: orchestrator },
    imageKernelMemory: { value: memory }, imageMcp: { value: mcp },
    identityNames: { value: {
      gateway: requiredEnvironment(env, 'GATEWAY_IDENTITY_NAME'),
      orchestrator: requiredEnvironment(env, 'ORCHESTRATOR_IDENTITY_NAME'),
      kernelmemory: requiredEnvironment(env, 'KERNEL_MEMORY_IDENTITY_NAME'),
      mcpserver: requiredEnvironment(env, 'MCP_IDENTITY_NAME'),
    } },
  };
  for (const [parameter, name] of Object.entries(requiredSettings)) parameters[parameter] = { value: requiredEnvironment(env, name) };
  for (const [parameter, name] of Object.entries(optionalSettings)) parameters[parameter] = { value: env[name] ?? '' };
  const temporary = mkdtempSync(join(tmpdir(), 'myaiagent-deployment-'));
  try {
    const file = join(temporary, 'parameters.json');
    writeFileSync(file, JSON.stringify(parameters), { mode: 0o600 });
    const result = run('az', ['deployment', 'group', 'create', '--only-show-errors',
      '--name', `myaiagent-${stage}-${env.GITHUB_RUN_ID || 'local'}`, '--resource-group', resourceGroup,
      '--template-file', 'infra/apps.bicep', '--parameters', `@${file}`,
      '--query', 'properties.outputs.gatewayFqdn.value', '--output', 'tsv'],
    { env, encoding: 'utf8', shell: false, timeout: 30 * 60_000, maxBuffer: 1024 * 1024 });
    if (result.status !== 0 || result.error || result.signal) {
      throw new Error('Azure deployment failed; inspect the deployment in Azure. No command output was printed.');
    }
    let fqdn: string;
    try {
      fqdn = gatewayHostname(result.stdout?.toString().trim() ?? '');
    } catch (error) {
      if (!(error instanceof UsageError)) throw error;
      throw new Error('Azure deployment returned an invalid gateway hostname.');
    }
    if (env.GITHUB_OUTPUT) appendFileSync(env.GITHUB_OUTPUT, `fqdn=${fqdn}\n`);
    return fqdn;
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    console.log(deployContainerApps(process.argv.slice(2)));
  } catch (error) {
    console.error(error instanceof Error ? error.message : 'Deployment failed.');
    process.exitCode = error instanceof UsageError ? 2 : 1;
  }
}
