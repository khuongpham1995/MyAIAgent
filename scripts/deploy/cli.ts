export class UsageError extends Error {}

export function requiredEnvironment(env: NodeJS.ProcessEnv, name: string): string {
  const value = env[name];
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

export function gatewayHostname(value: string): string {
  if (!value || value.length > 253 || value.split('.').some(label =>
    !/^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/i.test(label))) {
    throw new UsageError('Expected a gateway hostname without a scheme, port, path, or credentials.');
  }
  return value.toLowerCase();
}
