import React from 'react';
import { render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The api client's transitive import chain pulls in firebase/auth; stub it
// (same rationale as MatchupCard.live.test.tsx).
jest.mock('firebase/auth', () => ({
  getAuth: () => ({ currentUser: null }),
}));

jest.mock('@/src/services/api/seasonApi', () => ({
  seasonApi: {
    getCurrentSeason: jest.fn(),
  },
  currentSeasonKeys: {
    current: (sport: string, league: string) => ['season', 'current', sport, league],
  },
}));

jest.mock('@/src/services/api/rankingsApi', () => {
  const actual = jest.requireActual('@/src/services/api/rankingsApi');
  return {
    ...actual,
    rankingsApi: {
      getSeasonRankings: jest.fn(),
      getWeekRankings: jest.fn(),
    },
  };
});

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockPush }),
}));

import { RankingsCard } from '@/src/components/features/home/RankingsCard';
import { seasonApi } from '@/src/services/api/seasonApi';
import { rankingsApi } from '@/src/services/api/rankingsApi';

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const entry = (rank: number, name: string, slug: string) => ({
  rank,
  franchiseName: name,
  franchiseSlug: slug,
  franchiseSeasonId: `fs-${rank}`,
  wins: 0,
  losses: 0,
});

const apPoll = {
  pollId: 'ap',
  pollName: 'AP Top 25',
  seasonYear: 2026,
  week: 1,
  pollDateUtc: '2026-08-17T00:00:00Z',
  hasPoints: true,
  hasFirstPlaceVotes: true,
  hasTrends: false,
  entries: Array.from({ length: 25 }, (_, i) => entry(i + 1, `Team ${i + 1}`, `team-${i + 1}`)),
};

beforeEach(() => {
  jest.clearAllMocks();
  (seasonApi.getCurrentSeason as jest.Mock).mockResolvedValue({
    data: { seasonYear: 2026 },
  });
});

describe('RankingsCard', () => {
  it('renders the AP top 5 with a full-rankings affordance', async () => {
    (rankingsApi.getSeasonRankings as jest.Mock).mockResolvedValue({
      data: [{ ...apPoll, pollId: 'usa', pollName: 'Coaches Poll' }, apPoll],
    });

    renderWithProviders(<RankingsCard />);

    // Prefers 'ap' over the first-listed poll.
    await waitFor(() => expect(screen.getByText('AP TOP 25')).toBeTruthy());
    expect(screen.getByText('Team 1')).toBeTruthy();
    expect(screen.getByText('Team 5')).toBeTruthy();
    expect(screen.queryByText('Team 6')).toBeNull();
    expect(screen.getByText('Full rankings ›')).toBeTruthy();

    // Rankings were requested for the RESOLVED season, not a literal.
    expect(rankingsApi.getSeasonRankings).toHaveBeenCalledWith(2026);
  });

  it('renders nothing when no polls exist for the season', async () => {
    (rankingsApi.getSeasonRankings as jest.Mock).mockResolvedValue({ data: [] });

    renderWithProviders(<RankingsCard />);

    await waitFor(() => expect(rankingsApi.getSeasonRankings).toHaveBeenCalled());
    expect(screen.queryByText('Full rankings ›')).toBeNull();
  });

  it('never falls back to the CFP poll when AP is absent', async () => {
    (rankingsApi.getSeasonRankings as jest.Mock).mockResolvedValue({
      data: [{ ...apPoll, pollId: 'cfp', pollName: 'CFP Rankings' }],
    });

    renderWithProviders(<RankingsCard />);

    await waitFor(() => expect(rankingsApi.getSeasonRankings).toHaveBeenCalled());
    expect(screen.queryByText('CFP RANKINGS')).toBeNull();
    expect(screen.queryByText('Team 1')).toBeNull();
  });
});
