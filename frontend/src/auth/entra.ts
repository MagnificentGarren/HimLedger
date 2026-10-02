import { PublicClientApplication, type AuthenticationResult, type SilentRequest } from '@azure/msal-browser';

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined;
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined;
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined;

export const isEntraConfigured = Boolean(clientId && authority && apiScope);

const requiredApiScope = (): string => {
  if (!apiScope) throw new Error('VITE_ENTRA_API_SCOPE is not configured.');
  return apiScope;
};

const msal = new PublicClientApplication({
  auth: {
    clientId: clientId || 'not-configured',
    authority: authority || 'https://login.microsoftonline.com/common',
    redirectUri: `${window.location.origin}/login`,
  },
});

let initialized: Promise<void> | undefined;
const initialize = () => {
  initialized ??= msal.initialize();
  return initialized;
};

export async function beginEntraSignIn(): Promise<void> {
  if (!isEntraConfigured) {
    throw new Error('Entra ID sign-in is not configured.');
  }
  await initialize();
  await msal.loginRedirect({ scopes: [requiredApiScope()] });
}

export async function completeEntraSignIn(): Promise<AuthenticationResult | null> {
  if (!isEntraConfigured) return null;
  await initialize();
  const result = await msal.handleRedirectPromise();
  if (!result?.account) return null;
  msal.setActiveAccount(result.account);
  const request: SilentRequest = { scopes: [requiredApiScope()], account: result.account };
  return msal.acquireTokenSilent(request);
}

export async function acquireEntraAccessToken(): Promise<string> {
  if (!isEntraConfigured) {
    throw new Error('Entra ID sign-in is not configured.');
  }
  await initialize();
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0];
  if (!account) {
    throw new Error('The Entra account is no longer available. Sign in again.');
  }
  msal.setActiveAccount(account);
  const result = await msal.acquireTokenSilent({ scopes: [requiredApiScope()], account });
  return result.accessToken;
}
