import { runtimeEnv } from './environment.generated';

export interface EnvironmentConfig {
  production: boolean;
  apiGatewayUrl: string;
  googleClientId: string;
  razorpayKeyId: string;
  pointsToRupeeRate: number;
}

const fallbackApiGatewayUrl = 'http://localhost:5006';
const configuredApiGatewayUrl = (runtimeEnv.apiGatewayUrl || '').trim();
const hasPlaceholderGateway = configuredApiGatewayUrl.includes('your-gateway-domain.com');
const effectiveApiGatewayUrl = !configuredApiGatewayUrl || hasPlaceholderGateway
  ? fallbackApiGatewayUrl
  : configuredApiGatewayUrl;
const configuredGoogleClientId = (runtimeEnv.googleClientId || '').trim();
const hasPlaceholderGoogleClientId = configuredGoogleClientId.includes('your-google-client-id');
const effectiveGoogleClientId = hasPlaceholderGoogleClientId ? '' : configuredGoogleClientId;

export const environment: EnvironmentConfig = {
  production: false,
  apiGatewayUrl: effectiveApiGatewayUrl,
  googleClientId: effectiveGoogleClientId,
  razorpayKeyId: (runtimeEnv.razorpayKeyId || '').trim(),
  pointsToRupeeRate: 0.25
};
