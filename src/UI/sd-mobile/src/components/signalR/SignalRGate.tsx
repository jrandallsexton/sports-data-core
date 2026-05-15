import { useAuth } from '@/src/hooks/useAuth';
import { useSignalRClient } from '@/src/hooks/useSignalRClient';

/**
 * Auth-gated mount point for the SignalR connection. The inner component
 * is only rendered once a Firebase user exists — that's the only way to
 * keep `useSignalRClient` strictly mounted vs unmounted across sign-in /
 * sign-out without making the hook itself auth-aware (hooks can't be
 * conditional, but components can).
 *
 * Returns null — there is no visible UI. The hook does its work via
 * side effects on `contestUpdatesStore`.
 */
function SignalRMount(): null {
  useSignalRClient();
  return null;
}

export function SignalRGate() {
  const { isAuthenticated, isInitialized } = useAuth();
  if (!isInitialized || !isAuthenticated) return null;
  return <SignalRMount />;
}
