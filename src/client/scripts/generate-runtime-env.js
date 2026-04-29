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
const fromFilesGoogleClientId = modeEnv.GOOGLE_CLIENT_ID || commonEnv.GOOGLE_CLIENT_ID || '';
const fromProcessGoogleClientId = (process.env.GOOGLE_CLIENT_ID || '').trim();
const isPlaceholderGoogleClientId = fromProcessGoogleClientId.includes('your-google-client-id');
const fromFilesRazorpayKeyId = modeEnv.RAZORPAY_KEY_ID || commonEnv.RAZORPAY_KEY_ID || '';
const fromProcessRazorpayKeyId = (process.env.RAZORPAY_KEY_ID || '').trim();

// For local development, favor checked-in env files unless an explicit non-placeholder override is provided.
const apiGatewayUrl = fromProcess && !isPlaceholder ? fromProcess : fromFiles || fromProcess || '';
const googleClientId = fromProcessGoogleClientId && !isPlaceholderGoogleClientId
  ? fromProcessGoogleClientId
  : fromFilesGoogleClientId || fromProcessGoogleClientId || '';
const razorpayKeyId = fromProcessRazorpayKeyId || fromFilesRazorpayKeyId || '';

fs.mkdirSync(path.dirname(outputPath), { recursive: true });

const output = [
  'export const runtimeEnv = {',
  `  apiGatewayUrl: ${JSON.stringify(apiGatewayUrl)},`,
  `  googleClientId: ${JSON.stringify(googleClientId)},`,
  `  razorpayKeyId: ${JSON.stringify(razorpayKeyId)}`,
  '} as const;',
  ''
].join('\n');

fs.writeFileSync(outputPath, output, 'utf8');
console.log(`[env] Generated src/environments/environment.generated.ts (${mode})`);
