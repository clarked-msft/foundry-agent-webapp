import type { Configuration } from "@azure/msal-browser";
import { LogLevel } from "@azure/msal-browser";

const authorityHost = (import.meta.env.VITE_ENTRA_AUTHORITY || "https://login.microsoftonline.com").replace(/\/+$/, "");
const clientId = import.meta.env.VITE_ENTRA_SPA_CLIENT_ID;

if (!clientId) {
  throw new Error(
    "VITE_ENTRA_SPA_CLIENT_ID is not set. This must be provided during build time. " +
    "For local dev, ensure azd environment is configured and run preprovision hook."
  );
}

// When OBO is enabled, scopes target the backend API app instead of the SPA
const scopeClientId = import.meta.env.VITE_ENTRA_BACKEND_CLIENT_ID || clientId;

const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID;

if (!tenantId) {
  throw new Error(
    "VITE_ENTRA_TENANT_ID is not set. This must be provided during build time. " +
    "For local dev, run setup-local-dev.ps1 to configure from azd environment."
  );
}

const authority = authorityHost.includes(`/${tenantId}`)
  ? authorityHost
  : `${authorityHost}/${tenantId}`;

const configuredScope = import.meta.env.VITE_ENTRA_API_SCOPE || import.meta.env.VITE_ENTRA_SCOPE;
const defaultScope = `api://${scopeClientId}/Chat.ReadWrite`;
const apiScope = configuredScope || defaultScope;

export const msalConfig: Configuration = {
  auth: {
    clientId: clientId,
    authority: authority,
    redirectUri: window.location.origin, // Will be https://<container-app-url> in production
    postLogoutRedirectUri: window.location.origin,
    navigateToLoginRequestUrl: false, // Avoid redirect loops
  },
  cache: {
    cacheLocation: "localStorage", // Use localStorage for token caching
    storeAuthStateInCookie: false, // Set to true if IE11 support needed
  },
  system: {
    loggerOptions: {
      logLevel: import.meta.env.DEV ? LogLevel.Info : LogLevel.Warning,
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            break;
          case LogLevel.Warning:
            console.warn(message);
            break;
          case LogLevel.Info:
            console.info(message);
            break;
          case LogLevel.Verbose:
            console.debug(message);
            break;
        }
      },
    },
  },
};

// API permission scope (defaults to the app registration scope but can be overridden for sovereign clouds)
export const loginRequest = {
  scopes: [apiScope],
};

export const tokenRequest = {
  scopes: [apiScope],
  forceRefresh: false, // Use cached token if valid
};
