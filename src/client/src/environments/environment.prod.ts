import { runtimeEnv } from './environment.generated';

export interface EnvironmentConfig {
  production: boolean;
  apiGatewayUrl: string;
  googleClientId: string;
  pointsToRupeeRate: number;
}

const fallbackApiGatewayUrl = typeof window !== 'undefined' ? window.location.origin : '';
const configuredGoogleClientId = (runtimeEnv.googleClientId || '').trim();
const hasPlaceholderGoogleClientId = configuredGoogleClientId.includes('your-google-client-id');

export const environment: EnvironmentConfig = {
  production: true,
  apiGatewayUrl: runtimeEnv.apiGatewayUrl || fallbackApiGatewayUrl,
  googleClientId: hasPlaceholderGoogleClientId ? '' : configuredGoogleClientId,
  pointsToRupeeRate: 0.25
};
