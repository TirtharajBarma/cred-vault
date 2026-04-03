declare global {
  interface Window {
    __env?: {
      apiGatewayUrl?: string;
    };
  }
}

export const environment = {
  production: false,
  apiGatewayUrl: window.__env?.apiGatewayUrl || 'http://localhost:5006'
};
