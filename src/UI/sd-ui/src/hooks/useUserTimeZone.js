import { useUserDto } from "../contexts/UserContext";
import { DEFAULT_TIMEZONE } from "../utils/timeUtils";

/**
 * Returns the IANA timezone the UI should render game times in for the
 * current user. Falls back to America/New_York (Eastern Time) when the
 * user hasn't picked one yet, when the user DTO hasn't loaded, or when
 * the user is on a marketing/auth page outside the UserProvider.
 */
export function useUserTimeZone() {
  try {
    const ctx = useUserDto();
    return ctx?.userDto?.timezone || DEFAULT_TIMEZONE;
  } catch {
    return DEFAULT_TIMEZONE;
  }
}
