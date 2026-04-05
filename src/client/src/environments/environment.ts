import { runtimeEnv } from './environment.generated';

export interface EnvironmentConfig {
  production: boolean;
  apiGatewayUrl: string;
  pointsToRupeeRate: number;
}

const fallbackApiGatewayUrl = typeof window !== 'undefined' ? window.location.origin : '';

export const environment: EnvironmentConfig = {
  production: false,
  apiGatewayUrl: runtimeEnv.apiGatewayUrl || fallbackApiGatewayUrl,
  pointsToRupeeRate: 0.25
};
