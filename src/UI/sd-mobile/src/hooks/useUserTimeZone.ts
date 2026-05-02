import { useCurrentUser } from '@/src/hooks/useStandings';
import { DEFAULT_TIMEZONE } from '@/src/utils/timeUtils';

/**
 * Returns the IANA timezone the UI should render game times in for the
 * current user. Falls back to America/New_York (Eastern Time) when the
 * user hasn't picked one yet, when the user DTO hasn't loaded, or when
 * we're rendering on a route outside the auth gate where /user/me hasn't
 * been fetched.
 *
 * Mirrors `src/UI/sd-ui/src/hooks/useUserTimeZone.js`.
 */
export function useUserTimeZone(): string {
  const { data: me } = useCurrentUser();
  return me?.timezone || DEFAULT_TIMEZONE;
}
