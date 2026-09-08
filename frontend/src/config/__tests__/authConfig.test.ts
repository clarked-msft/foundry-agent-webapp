import { describe, it, expect, vi, beforeEach } from 'vitest';

describe('authConfig', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    delete process.env.VITE_ENTRA_AUTHORITY;
    delete process.env.VITE_ENTRA_API_SCOPE;
    delete process.env.VITE_ENTRA_SCOPE;
  });

  it('uses SPA client ID for scopes when backend client ID not set', async () => {
    vi.stubEnv('VITE_ENTRA_SPA_CLIENT_ID', 'spa-client-id');
    vi.stubEnv('VITE_ENTRA_TENANT_ID', 'tenant-id');
    vi.stubEnv('VITE_ENTRA_BACKEND_CLIENT_ID', '');
    const { loginRequest } = await import('../../config/authConfig');
    expect(loginRequest.scopes[0]).toBe('api://spa-client-id/Chat.ReadWrite');
  });

  it('uses backend client ID for scopes when set', async () => {
    vi.stubEnv('VITE_ENTRA_SPA_CLIENT_ID', 'spa-client-id');
    vi.stubEnv('VITE_ENTRA_TENANT_ID', 'tenant-id');
    vi.stubEnv('VITE_ENTRA_BACKEND_CLIENT_ID', 'backend-client-id');
    const { loginRequest } = await import('../../config/authConfig');
    expect(loginRequest.scopes[0]).toBe('api://backend-client-id/Chat.ReadWrite');
  });

  it('uses custom authority host and custom API scope when configured', async () => {
    vi.stubEnv('VITE_ENTRA_SPA_CLIENT_ID', 'spa-client-id');
    vi.stubEnv('VITE_ENTRA_TENANT_ID', 'tenant-id');
    vi.stubEnv('VITE_ENTRA_AUTHORITY', 'https://login.microsoftonline.us');
    vi.stubEnv('VITE_ENTRA_API_SCOPE', 'api://custom-api/Custom.Scope');
    const { msalConfig, loginRequest } = await import('../../config/authConfig');
    expect(msalConfig.auth.authority).toBe('https://login.microsoftonline.us/tenant-id');
    expect(loginRequest.scopes[0]).toBe('api://custom-api/Custom.Scope');
  });
});
