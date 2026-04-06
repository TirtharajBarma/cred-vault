import { runtimeEnv } from './environment.generated';

export interface EnvironmentConfig {
  production: boolean;
  apiGatewayUrl: string;
  pointsToRupeeRate: number;
}

const fallbackApiGatewayUrl = 'http://localhost:5006';
const configuredApiGatewayUrl = (runtimeEnv.apiGatewayUrl || '').trim();
const hasPlaceholderGateway = configuredApiGatewayUrl.includes('your-gateway-domain.com');
const effectiveApiGatewayUrl = !configuredApiGatewayUrl || hasPlaceholderGateway
  ? fallbackApiGatewayUrl
  : configuredApiGatewayUrl;

export const environment: EnvironmentConfig = {
  production: false,
  apiGatewayUrl: effectiveApiGatewayUrl,
  pointsToRupeeRate: 0.25
};
