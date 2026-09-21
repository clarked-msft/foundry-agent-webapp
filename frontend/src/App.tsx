import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsalAuthentication } from "@azure/msal-react";
import { Button, Spinner, Text } from '@fluentui/react-components';
import { useAppState } from './hooks/useAppState';
import { InteractionType } from "@azure/msal-browser";
import { ErrorBoundary } from "./components/core/ErrorBoundary";
import { AgentChat } from "./components/AgentChat";
import { loginRequest } from "./config/authConfig";
import { useState, useEffect, useCallback } from "react";
import { useAuth } from "./hooks/useAuth";
import type { IAgentMetadata } from "./types/chat";
import "./App.css";

function App() {
  // This hook handles authentication automatically - redirects if not authenticated
  const { login, error: authenticationError } = useMsalAuthentication(InteractionType.Redirect, loginRequest);
  const { auth } = useAppState();
  const { getAccessToken } = useAuth();
  const [agentMetadata, setAgentMetadata] = useState<IAgentMetadata | null>(null);
  const [isLoadingAgent, setIsLoadingAgent] = useState(true);
  const [signInError, setSignInError] = useState<string | null>(null);

  const handleSignIn = useCallback(async () => {
    setSignInError(null);
    try {
      await login();
    } catch (error) {
      setSignInError(error instanceof Error ? error.message : 'Sign in failed');
    }
  }, [login]);

  // Wrap fetchAgentMetadata in useCallback to make it stable for the effect
  const fetchAgentMetadata = useCallback(async () => {
    if (auth.status !== 'authenticated') {
      setIsLoadingAgent(false);
      return;
    }

    setIsLoadingAgent(true);
    try {
      const token = await getAccessToken();
      if (!token) {
        throw new Error('Failed to acquire access token');
      }
      const apiUrl = import.meta.env.VITE_API_URL || '/api';
      
      const response = await fetch(`${apiUrl}/agent`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const data = await response.json();
      setAgentMetadata(data);
      
      // Update document title with agent name
      document.title = data.name ? `${data.name} - Azure AI Agent` : 'Azure AI Agent';
    } catch (error) {
      console.error('Error fetching agent metadata:', error);
      // Fallback data keeps UI functional on error
      setAgentMetadata({
        id: 'fallback-agent',
        object: 'agent',
        createdAt: Date.now() / 1000,
        name: 'Azure AI Agent',
        description: 'Your intelligent conversational partner powered by Azure AI',
        model: 'gpt-4o-mini',
        metadata: { logo: 'Avatar_Default.svg' }
      });
      document.title = 'Azure AI Agent';
    } finally {
      setIsLoadingAgent(false);
    }
  }, [auth.status, getAccessToken]);

  useEffect(() => {
    fetchAgentMetadata();
  }, [fetchAgentMetadata]);

  return (
    <ErrorBoundary>
      {auth.status === 'initializing' || (auth.status === 'authenticated' && isLoadingAgent) ? (
        <div className="app-container" style={{ 
          display: 'flex', 
          alignItems: 'center', 
          justifyContent: 'center', 
          height: '100vh', 
          flexDirection: 'column', 
          gap: '1rem' 
        }}>
          <Spinner size="large" />
          <p style={{ margin: 0 }}>
            {auth.status === 'initializing' ? 'Preparing your session...' : 'Loading agent...'}
          </p>
        </div>
      ) : (
        <>
          <AuthenticatedTemplate>
            {agentMetadata && (
              <div className="app-container">
                <AgentChat 
                  agentId={agentMetadata.id}
                  agentName={agentMetadata.name}
                  agentDescription={agentMetadata.description || undefined}
                  agentLogo={agentMetadata.metadata?.logo}
                  starterPrompts={agentMetadata.starterPrompts || undefined}
                />
              </div>
            )}
          </AuthenticatedTemplate>
          <UnauthenticatedTemplate>
            <div className="app-container" style={{ 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'center', 
              height: '100vh',
              flexDirection: 'column',
              gap: '1rem'
            }}>
              <Text>Your session has ended.</Text>
              <Button appearance="primary" onClick={handleSignIn}>
                Sign in
              </Button>
              {(signInError || authenticationError) && (
                <Text role="alert">
                  {signInError || authenticationError?.message}
                </Text>
              )}
            </div>
          </UnauthenticatedTemplate>
        </>
      )}
    </ErrorBoundary>
  );
}

export default App;
