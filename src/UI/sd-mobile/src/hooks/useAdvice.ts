import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { picksApi } from '@/src/services/api/picksApi';
import type { SubmitPickPayload } from '@/src/services/api/picksApi';
import type { AdvisedPick, AdvisorLevel, PickAdvice, PickType } from '@/src/types/models';
import { pickKeys } from './useContest';
import { useAuthStore } from '@/src/stores/authStore';

export const adviceKeys = {
  // One entry per level; "recommended" is the first, level-less request whose
  // response tells us which level StatBot chose. Keying by level is what
  // makes level-switching race-free: each card reads its own cache slot, so
  // the last click — never the last response to land — decides what shows.
  forLevel: (leagueId: string, week: number, level: AdvisorLevel | 'recommended') =>
    ['advice', leagueId, week, level] as const,
};

/**
 * StatBot's advice for one league-week at one level (or the recommended
 * level when `level` is null). Read-only. See docs/features/statbot-advisor.md.
 */
export function usePickAdvice(
  leagueId: string | null | undefined,
  week: number | null | undefined,
  level: AdvisorLevel | null,
  enabled: boolean,
) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<PickAdvice>({
    queryKey: adviceKeys.forLevel(leagueId ?? '', week ?? 0, level ?? 'recommended'),
    queryFn: () => picksApi.getAdvice(leagueId!, week!, level ?? undefined).then((r) => r.data),
    enabled: isInitialized && !!user && !!leagueId && week != null && enabled,
    // Advice depends on the slate, standings, and the user's own picks; a
    // fresh open should refetch, but flipping between cards should not.
    staleTime: 1000 * 60,
    retry: 1,
  });
}

/**
 * Writes StatBot's sheet through the same submit endpoint as a hand pick, one
 * call per pick, so every server-side rule applies unchanged. Sequential so a
 * failure leaves a known prefix applied; the refetch shows the true state.
 */
export function useApplyAdvice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      leagueId: string;
      week: number;
      pickType: PickType;
      useConfidencePoints: boolean;
      picks: AdvisedPick[];
    }) => {
      let applied = 0;
      try {
        for (const p of vars.picks) {
          const payload: SubmitPickPayload = {
            pickemGroupId: vars.leagueId,
            contestId: p.contestId,
            pickType: vars.pickType,
            franchiseSeasonId: p.franchiseSeasonId!,
            week: vars.week,
          };
          if (vars.useConfidencePoints && p.confidencePoints != null) {
            payload.confidencePoints = p.confidencePoints;
          }
          await picksApi.submitPick(payload);
          applied++;
        }
      } catch (err) {
        throw new ApplyAdviceError(applied, vars.picks.length, err);
      }
      return { applied };
    },
    onSettled: (_res, _err, vars) => {
      qc.invalidateQueries({ queryKey: pickKeys.byLeagueWeek(vars.leagueId, vars.week) });
      qc.invalidateQueries({ queryKey: ['advice', vars.leagueId, vars.week] });
    },
  });
}

export class ApplyAdviceError extends Error {
  constructor(
    public readonly applied: number,
    public readonly total: number,
    public readonly cause: unknown,
  ) {
    super(`Applied ${applied} of ${total} picks before an error`);
    this.name = 'ApplyAdviceError';
  }
}
