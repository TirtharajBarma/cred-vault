const fs = require('fs');
const path = require('path');

const mode = process.argv[2] === 'production' ? 'production' : 'development';
const projectRoot = path.resolve(__dirname, '..');
const outputPath = path.join(projectRoot, 'src', 'environments', 'environment.generated.ts');

function parseEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return {};
  const content = fs.readFileSync(filePath, 'utf8');
  const result = {};

  for (const rawLine of content.split('\n')) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) continue;

    const separator = line.indexOf('=');
    if (separator === -1) continue;

    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim();
    if (!key) continue;

    result[key] = value;
  }

  return result;
}

const commonEnv = parseEnvFile(path.join(projectRoot, '.env'));
const modeEnv = mode === 'production'
  ? parseEnvFile(path.join(projectRoot, '.env.production'))
  : parseEnvFile(path.join(projectRoot, '.env.development'));

const fromFiles = modeEnv.API_GATEWAY_URL || commonEnv.API_GATEWAY_URL || '';
const fromProcess = (process.env.API_GATEWAY_URL || '').trim();
const isPlaceholder = fromProcess.includes('your-gateway-domain.com');

// For local development, favor checked-in env files unless an explicit non-placeholder override is provided.
const apiGatewayUrl = fromProcess && !isPlaceholder ? fromProcess : fromFiles || fromProcess || '';

fs.mkdirSync(path.dirname(outputPath), { recursive: true });

const output = [
  'export const runtimeEnv = {',
  `  apiGatewayUrl: ${JSON.stringify(apiGatewayUrl)}`,
  '} as const;',
  ''
].join('\n');

fs.writeFileSync(outputPath, output, 'utf8');
console.log(`[env] Generated src/environments/environment.generated.ts (${mode})`);