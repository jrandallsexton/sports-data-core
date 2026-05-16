import React from 'react';
import { act, render, screen } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// MatchupCard's transitive import chain pulls in firebase/auth (via the
// axios api client). The Firebase v12 ESM shim trips Jest's transformer
// on `@firebase/util/dist/postinstall.mjs`, so stub the auth module —
// the card we render never calls a Firebase method anyway.
jest.mock('firebase/auth', () => ({
  getAuth: () => ({ currentUser: null }),
}));

import { MatchupCard } from '@/src/components/features/games/MatchupCard';
import { useContestUpdatesStore } from '@/src/stores/contestUpdatesStore';
import type { Matchup } from '@/src/types/models';

// GameStatus → useUserTimeZone → useCurrentUser uses TanStack Query.
// Wrap renders in a fresh QueryClient (retry off so failed queries don't
// schedule timers that leak between tests).
function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>{ui}</QueryClientProvider>,
  );
}

const CID = '00000000-0000-0000-0000-000000000001';

const buildBaseballMatchup = (overrides?: Partial<Matchup>): Matchup => ({
  contestId: CID,
  startDateUtc: '2026-05-15T18:00:00Z',

  away: 'Yankees',
  awayShort: 'NYY',
  awaySlug: 'yankees',
  awayFranchiseSeasonId: '00000000-0000-0000-0000-0000000000a1',
  awayLogoUri: null,

  home: 'Red Sox',
  homeShort: 'BOS',
  homeSlug: 'red-sox',
  homeFranchiseSeasonId: '00000000-0000-0000-0000-0000000000a2',
  homeLogoUri: null,

  status: 'Scheduled',
  ...overrides,
});

const buildFootballMatchup = (overrides?: Partial<Matchup>): Matchup => ({
  contestId: CID,
  startDateUtc: '2026-05-15T18:00:00Z',

  away: 'Chiefs',
  awayShort: 'KC',
  awaySlug: 'chiefs',
  awayFranchiseSeasonId: '00000000-0000-0000-0000-0000000000b1',
  awayLogoUri: null,

  home: 'Bills',
  homeShort: 'BUF',
  homeSlug: 'bills',
  homeFranchiseSeasonId: '00000000-0000-0000-0000-0000000000b2',
  homeLogoUri: null,

  status: 'Scheduled',
  ...overrides,
});

describe('MatchupCard — live updates', () => {
  beforeEach(() => {
    useContestUpdatesStore.setState(useContestUpdatesStore.getInitialState(), true);
  });

  it('renders baseball InProgress UI after a BaseballPlayCompleted event arrives', () => {
    const matchup = buildBaseballMatchup();
    renderWithProviders(<MatchupCard matchup={matchup} leagueSport="BaseballMlb" />);

    // Before any event: card shows Scheduled-state copy (no LIVE label).
    expect(screen.queryByText('LIVE')).toBeNull();

    // Push a baseball play event for this contest into the store.
    act(() => {
      useContestUpdatesStore.getState().handleBaseballPlayCompleted({
        contestId: CID,
        competitionId: '00000000-0000-0000-0000-0000000000cc',
        playId: '00000000-0000-0000-0000-0000000000dd',
        playDescription: 'A. Judge homers to deep center.',
        inning: 3,
        halfInning: 'Top',
        awayScore: 4,
        homeScore: 2,
        balls: 1,
        strikes: 2,
        outs: 1,
        runnerOnFirst: true,
        runnerOnSecond: false,
        runnerOnThird: true,
        atBatAthleteSeasonId: null,
        atBatShortName: 'A. Judge',
        atBatPositionAbbreviation: 'RF',
        atBatHeadshotUrl: null,
        pitchingAthleteSeasonId: null,
        pitchingShortName: 'C. Sale',
        pitchingPositionAbbreviation: 'P',
        pitchingHeadshotUrl: null,
      });
    });

    // LIVE label appears (status was self-healed to InProgress by the store).
    expect(screen.getByText('LIVE')).toBeTruthy();

    // Baseball summary row: "Top 3 · 1-2 · 1 out".
    expect(screen.getByText(/Top 3/)).toBeTruthy();
    expect(screen.getByText(/1-2/)).toBeTruthy();
    expect(screen.getByText(/1 out\b/)).toBeTruthy();

    // At-bat header.
    expect(screen.getByText(/AB:\s*A\. Judge/)).toBeTruthy();
    expect(screen.getByText(/P:\s*C\. Sale/)).toBeTruthy();

    // Last-play description.
    expect(screen.getByText(/homers to deep center/)).toBeTruthy();

    // Score line includes both teams' new scores.
    expect(screen.getByText(/NYY 4 – 2 BOS/)).toBeTruthy();
  });

  it('renders football InProgress UI after a FootballPlayCompleted event arrives', () => {
    const matchup = buildFootballMatchup();
    renderWithProviders(<MatchupCard matchup={matchup} leagueSport="FootballNfl" />);

    expect(screen.queryByText('LIVE')).toBeNull();

    act(() => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted({
        contestId: CID,
        competitionId: '00000000-0000-0000-0000-0000000000ee',
        playId: '00000000-0000-0000-0000-0000000000ff',
        playDescription: 'P. Mahomes 18-yard pass.',
        period: 'Q3',
        clock: '4:21',
        awayScore: 14,
        homeScore: 17,
        possessionFranchiseSeasonId: '00000000-0000-0000-0000-0000000000b1', // away
        isScoringPlay: false,
        ballOnYardLine: 22,
      });
    });

    // LIVE + period/clock + new score.
    expect(screen.getByText('LIVE')).toBeTruthy();
    expect(screen.getByText(/Q3\s*–\s*4:21/)).toBeTruthy();
    expect(screen.getByText(/KC 14 – 17 BUF/)).toBeTruthy();
  });

  it('does not subscribe to events for a different contestId', () => {
    const matchup = buildBaseballMatchup();
    renderWithProviders(<MatchupCard matchup={matchup} leagueSport="BaseballMlb" />);

    // Event for a DIFFERENT contest — must not flip this card.
    act(() => {
      useContestUpdatesStore.getState().handleBaseballPlayCompleted({
        contestId: 'some-other-contest',
        competitionId: '00000000-0000-0000-0000-0000000000cc',
        playId: '00000000-0000-0000-0000-0000000000dd',
        playDescription: '',
        inning: 9,
        halfInning: 'Bottom',
        awayScore: 99,
        homeScore: 99,
        balls: 0,
        strikes: 0,
        outs: 0,
        runnerOnFirst: false,
        runnerOnSecond: false,
        runnerOnThird: false,
        atBatAthleteSeasonId: null,
        atBatShortName: null,
        atBatPositionAbbreviation: null,
        atBatHeadshotUrl: null,
        pitchingAthleteSeasonId: null,
        pitchingShortName: null,
        pitchingPositionAbbreviation: null,
        pitchingHeadshotUrl: null,
      });
    });

    expect(screen.queryByText('LIVE')).toBeNull();
  });
});
