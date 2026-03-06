import type { League } from '@/src/types/models';

/** Extract the leagues array from the /user/me response. */
export function getLeagues(
  me: { leagues?: League[] } | undefined,
): League[] {
  return me?.leagues ?? [];
}
