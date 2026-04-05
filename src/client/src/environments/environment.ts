declare global {
  interface Window {
    __env?: {
      apiGatewayUrl?: string;
    };
  }
}

export interface EnvironmentConfig {
  production: boolean;
  apiGatewayUrl: string;
  pointsToRupeeRate: number;
}

export const environment: EnvironmentConfig = {
  production: false,
  apiGatewayUrl: window.__env?.apiGatewayUrl || 'http://localhost:5006',
  pointsToRupeeRate: 0.25
};
