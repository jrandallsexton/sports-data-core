import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { PlayerRosterBuilder } from '@/src/components/features/pickem/PlayerRosterBuilder';
import * as api from '@/src/services/api/playerPickemApi';
import type { Lineup, LineupSlot, PickemAthlete } from '@/src/services/api/playerPickemApi';

jest.mock('@/src/services/api/playerPickemApi', () => ({
  getAthletesByPosition: jest.fn(),
  getMyLineup: jest.fn(),
  upsertSlot: jest.fn(),
  clearSlot: jest.fn(),
}));

const mocked = api as jest.Mocked<typeof api>;

const owens: PickemAthlete = {
  athleteId: 'ath-owens',
  athleteSeasonId: 'as-owens',
  firstName: 'Trey',
  lastName: 'Owens',
  teamName: 'Texas Longhorns',
  teamSlug: 'texas-longhorns',
  position: 'QB',
  opponentName: 'Oklahoma Sooners',
  opponentSlug: 'oklahoma-sooners',
  opponentDefPerGame: 240.5,
  currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { passYds: 1400 } },
  previousSeason: null,
};

const savedQb = (overrides: Partial<LineupSlot> = {}): LineupSlot => ({
  slotId: 'QB',
  athleteId: 'ath-manning',
  athleteSeasonId: 'as-manning',
  position: 'QB',
  firstName: 'Arch',
  lastName: 'Manning',
  teamName: 'Texas Longhorns',
  teamSlug: 'texas-longhorns',
  contestId: 'c-1',
  contestStartUtc: '2026-10-10T19:30:00Z',
  opponentName: 'Oklahoma Sooners',
  isLocked: false,
  points: null,
  statLine: null,
  ...overrides,
});

const lineup = (slots: LineupSlot[]): Lineup => ({
  leagueId: 'lg-1',
  seasonYear: 2026,
  seasonWeek: 7,
  slots,
  totalPoints: 0,
});

const renderBuilder = () =>
  render(<PlayerRosterBuilder leagueId="lg-1" seasonYear={2026} week={7} sport="FootballNcaa" />);

describe('PlayerRosterBuilder (mobile)', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mocked.getAthletesByPosition.mockResolvedValue({ athletes: [owens] });
    mocked.getMyLineup.mockResolvedValue(lineup([]));
  });

  it("loads the saved lineup from the server into its slot", async () => {
    mocked.getMyLineup.mockResolvedValue(lineup([savedQb()]));
    renderBuilder();

    expect(await screen.findByText('A. Manning')).toBeTruthy();
    expect(mocked.getMyLineup).toHaveBeenCalledWith('lg-1', 2026, 7);
  });

  it('Add saves the pick through the API and re-reads the lineup', async () => {
    const saved = savedQb({ athleteId: 'ath-owens', athleteSeasonId: 'as-owens', firstName: 'Trey', lastName: 'Owens' });
    mocked.upsertSlot.mockResolvedValue(saved);
    renderBuilder();

    fireEvent.press(await screen.findByLabelText('Add Trey Owens'));

    await waitFor(() =>
      expect(mocked.upsertSlot).toHaveBeenCalledWith('lg-1', 2026, 7, 'QB', owens),
    );
    expect(await screen.findByText('T. Owens')).toBeTruthy();
    await waitFor(() => expect(mocked.getMyLineup).toHaveBeenCalledTimes(2));
  });

  it("shows the server's reason when a save is rejected", async () => {
    mocked.upsertSlot.mockRejectedValue({
      response: { data: { errors: [{ errorMessage: 'That game has already started.' }] } },
    });
    renderBuilder();

    fireEvent.press(await screen.findByLabelText('Add Trey Owens'));

    expect(await screen.findByText('That game has already started.')).toBeTruthy();
  });

  it('a locked slot can be neither replaced nor cleared', async () => {
    mocked.getMyLineup.mockResolvedValue(lineup([savedQb({ isLocked: true })]));
    renderBuilder();

    const button = await screen.findByLabelText('Locked Trey Owens');
    fireEvent.press(button);

    expect(mocked.upsertSlot).not.toHaveBeenCalled();
    expect(screen.queryByLabelText('Remove Arch Manning')).toBeNull();
  });

  it('shows the short team and opponent names when the API sends them', async () => {
    mocked.getAthletesByPosition.mockResolvedValue({
      athletes: [{ ...owens, teamShortName: 'Texas', opponentShortName: 'Oklahoma' }],
    });
    renderBuilder();

    expect(await screen.findByText('Texas')).toBeTruthy();
    expect(screen.getByText(/vs Oklahoma ·/)).toBeTruthy();
    expect(screen.queryByText('Texas Longhorns')).toBeNull();
  });

  it('falls back to the full names when no short names are sent', async () => {
    renderBuilder();

    expect(await screen.findByText('Texas Longhorns')).toBeTruthy();
    expect(screen.getByText(/vs Oklahoma Sooners/)).toBeTruthy();
  });

  it('shows a load error with a retry instead of an empty position', async () => {
    mocked.getAthletesByPosition.mockRejectedValueOnce(new Error('network'));
    renderBuilder();

    expect(await screen.findByText('Could not load athletes.')).toBeTruthy();
    expect(screen.queryByText('No athletes for this position.')).toBeNull();

    fireEvent.press(screen.getByText('Retry'));
    expect(await screen.findByLabelText('Add Trey Owens')).toBeTruthy();
  });
});
