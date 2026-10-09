import React from 'react';
import { render, screen } from '@testing-library/react-native';
import { PlayerStandingsList } from '@/src/components/features/standings/PlayerStandingsList';
import { PlayerWeekLineupsPane } from '@/src/components/features/standings/PlayerWeekLineupsPane';
import * as hooks from '@/src/hooks/usePlayerPickemLeaderboard';

jest.mock('@/src/hooks/usePlayerPickemLeaderboard', () => ({
  usePlayerStandings: jest.fn(),
  useLeagueWeekLineups: jest.fn(),
}));

const mocked = hooks as jest.Mocked<typeof hooks>;
const query = (data: unknown) =>
  ({ data, isLoading: false, isError: false, refetch: jest.fn(), isRefetching: false }) as never;

describe('PlayerStandingsList', () => {
  it('ranks by points (ties share), marks you by API user id, shows weekly wins', () => {
    mocked.usePlayerStandings.mockReturnValue(
      query({
        rows: [
          { userId: 'a', displayName: 'Ann', totalPoints: 120.4, weeklyWins: 2, weeks: [{}, {}] },
          { userId: 'b', displayName: 'Ben', totalPoints: 120.4, weeklyWins: 0, weeks: [{}, {}] },
          { userId: 'c', displayName: 'Cal', totalPoints: 80, weeklyWins: 0, weeks: [{}] },
        ],
      }),
    );
    render(<PlayerStandingsList leagueId="lg" seasonYear={2026} currentUserId="b" />);

    expect(screen.getAllByText('1')).toHaveLength(2);
    expect(screen.getByText('3')).toBeTruthy();
    expect(screen.getByText(/Ben\s+\(you\)/)).toBeTruthy();
    expect(screen.getByText(/🏆 2/)).toBeTruthy();
  });
});

describe('PlayerWeekLineupsPane', () => {
  const data = {
    members: [
      {
        userId: 'me',
        displayName: 'Ann',
        isSynthetic: false,
        totalPoints: 23.9,
        hiddenSlotCount: 0,
        slots: [
          {
            slotId: 'QB', firstName: 'CJ', lastName: 'Carr', teamName: 'Notre Dame',
            points: 23.9, statLine: '275 pass yds, 2 TD, 0 INT',
          },
        ],
      },
      { userId: 'ben', displayName: 'Ben', isSynthetic: false, totalPoints: 0, hiddenSlotCount: 2, slots: [] },
      { userId: 'bot', displayName: 'StatBot', isSynthetic: true, totalPoints: 10, hiddenSlotCount: 0, slots: [] },
    ],
  };

  const renderPane = (showBots: boolean) =>
    render(
      <PlayerWeekLineupsPane
        leagueId="lg"
        seasonYear={2026}
        week={5}
        seasonWeeks={[5]}
        onWeekChange={jest.fn()}
        showBots={showBots}
        currentUserId="me"
      />,
    );

  it("shows each member's players, points and stat lines, and hidden slots", () => {
    mocked.useLeagueWeekLineups.mockReturnValue(query(data));
    renderPane(true);

    expect(screen.getByText(/C\. Carr/)).toBeTruthy();
    expect(screen.getByText('275 pass yds, 2 TD, 0 INT')).toBeTruthy();
    expect(screen.getAllByText('23.9')).toHaveLength(2); // slot points and the card total
    expect(screen.getByText(/2 hidden until kickoff/)).toBeTruthy();
    expect(screen.getByText(/StatBot/)).toBeTruthy();
  });

  it('leaves bots out when Show Bots is off', () => {
    mocked.useLeagueWeekLineups.mockReturnValue(query(data));
    renderPane(false);

    expect(screen.queryByText(/StatBot/)).toBeNull();
  });
});
