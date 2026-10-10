import React from 'react';
import { Button, Text } from '@fluentui/react-components';
import { CopilotMessage } from '@fluentui-copilot/react-copilot-chat';
import { AgentIcon } from '../core/AgentIcon';
import styles from './OAuthConsentCard.module.css';

interface OAuthConsentCardProps {
  consentLink: string;
  serverLabel: string;
  onContinue: () => void;
  onDismiss: () => void;
  disabled?: boolean;
  resolved?: 'continued' | 'dismissed';
  agentName?: string;
  agentLogo?: string;
}

export const OAuthConsentCard: React.FC<OAuthConsentCardProps> = ({
  consentLink,
  serverLabel,
  onContinue,
  onDismiss,
  disabled,
  resolved,
  agentName = 'AI Assistant',
  agentLogo,
}) => (
  <CopilotMessage
    avatar={<AgentIcon logoUrl={agentLogo} />}
    name={agentName}
    loadingState="none"
    className={styles.message}
  >
    <div className={styles.content}>
      <Text className={styles.title} weight="semibold">
        {resolved === 'continued'
          ? 'Authorization submitted'
          : resolved === 'dismissed'
            ? 'Authorization postponed'
            : 'Authorization required'}
      </Text>

      <Text className={styles.description}>
        {resolved
          ? `OAuth authorization for ${serverLabel} was ${resolved === 'continued' ? 'submitted' : 'postponed'}.`
          : `${serverLabel} needs your permission before the agent can use it. Authorize in a new tab, then return here to continue.`}
      </Text>

      {!resolved && (
        <div className={styles.actions}>
          <a
            className={styles.authorizeLink}
            href={consentLink}
            target="_blank"
            rel="noopener noreferrer"
          >
            Authorize access
          </a>
          <Button appearance="primary" onClick={onContinue} disabled={disabled}>
            I've authorized
          </Button>
          <Button appearance="subtle" onClick={onDismiss} disabled={disabled}>
            Not now
          </Button>
        </div>
      )}
    </div>
  </CopilotMessage>
);
