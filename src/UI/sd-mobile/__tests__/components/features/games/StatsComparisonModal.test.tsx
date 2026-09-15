import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import {
  StatsComparisonModal,
  statFavored,
  metricFavored,
  METRICS_SPEC,
} from '@/src/components/features/games/StatsComparisonModal';
import type { Matchup, TeamComparisonData } from '@/src/types/models';
import { useSectionCollapseStore } from '@/src/stores/sectionCollapseStore';

// ─── Favored helpers — the math behind every tab/chip count and highlight ─────

// The collapse store is a module singleton; stats categories now start
// collapsed and a press persists, so each test starts cold.
beforeEach(() => {
  useSectionCollapseStore.setState({ collapsed: {}, hydrated: false });
});

describe('statFavored', () => {
  it('favors the higher value by default', () => {
    expect(statFavored({ displayValue: '45' }, { displayValue: '30' })).toBe('away');
    expect(statFavored({ displayValue: '10' }, { displayValue: '30' })).toBe('home');
  });

  it('inverts for lower-is-better stats (isNegativeAttribute)', () => {
    // Turnover-type row: fewer is better — the SMALLER value must win.
    expect(
      statFavored({ displayValue: '3', isNegativeAttribute: true }, { displayValue: '1' })
    ).toBe('home');
    expect(
      statFavored({ displayValue: '1', isNegativeAttribute: true }, { displayValue: '3' })
    ).toBe('away');
  });

  it('returns null on ties, missing sides, and non-numeric values', () => {
    expect(statFavored({ displayValue: '7' }, { displayValue: '7' })).toBeNull();
    expect(statFavored(undefined, { displayValue: '7' })).toBeNull();
    expect(statFavored({ displayValue: '-' }, { displayValue: '7' })).toBeNull();
  });
});

describe('metricFavored', () => {
  const ypp = METRICS_SPEC.flatMap(g => g.metrics).find(m => m.key === 'ypp')!;
  const oppYpp = METRICS_SPEC.flatMap(g => g.metrics).find(m => m.key === 'oppYpp')!;

  it('higher-is-better metrics favor the larger raw value', () => {
    expect(metricFavored(ypp, 5.0, 6.0)).toBe('home');
    expect(metricFavored(ypp, 6.0, 5.0)).toBe('away');
  });

  it('opp* metrics invert: the smaller raw value wins', () => {
    expect(metricFavored(oppYpp, 0.35, 0.5)).toBe('away');
    expect(metricFavored(oppYpp, 0.5, 0.35)).toBe('home');
  });

  it('returns null when either side is missing', () => {
    expect(metricFavored(ypp, null, 6.0)).toBeNull();
    expect(metricFavored(ypp, 6.0, undefined)).toBeNull();
  });
});

// ─── Modal render — label chain and metrics gating ────────────────────────────

const matchup = {
  contestId: '00000000-0000-0000-0000-000000000001',
  away: 'Florida A&M Rattlers',
  home: 'Miami Hurricanes',
} as unknown as Matchup;

const comparisonWith = (overrides?: {
  awayEntries?: object[];
  homeEntries?: object[];
  awayMetrics?: object | null;
  homeMetrics?: object | null;
  /** Replace the whole statistics record (e.g. {} for the stats-empty path). */
  statistics?: Record<string, object[]>;
}): TeamComparisonData =>
  ({
    teamA: {
      name: 'Florida A&M Rattlers',
      stats: {
        data: {
          statistics: overrides?.statistics ?? {
            defensive: overrides?.awayEntries ?? [
              // The regression this PR is named for: statisticValue is the
              // payload's human label — no legacy label/name present.
              { statisticKey: 'assistTackles', statisticValue: 'Assisted Tackles', displayValue: '45' },
            ],
          },
        },
      },
      metrics: overrides?.awayMetrics === undefined ? null : { data: overrides.awayMetrics },
    },
    teamB: {
      name: 'Miami Hurricanes',
      stats: {
        data: {
          statistics: overrides?.statistics ?? {
            defensive: overrides?.homeEntries ?? [
              { statisticKey: 'assistTackles', statisticValue: 'Assisted Tackles', displayValue: '30' },
            ],
          },
        },
      },
      metrics: overrides?.homeMetrics === undefined ? null : { data: overrides.homeMetrics },
    },
    history: null,
  }) as unknown as TeamComparisonData;

const renderModal = (comparison: TeamComparisonData) =>
  render(
    <StatsComparisonModal
      visible
      onClose={() => {}}
      matchup={matchup}
      comparison={comparison}
      isLoading={false}
      showGambling={false}
    />
  );

describe('StatsComparisonModal', () => {
  it('renders the payload statisticValue as the row label, never "Stat 1"', () => {
    renderModal(comparisonWith());

    // Categories start collapsed; open the one category the fixture has.
    fireEvent.press(screen.getByText('defensive (1:0)'));
    expect(screen.getByText('Assisted Tackles')).toBeTruthy();
    expect(screen.queryByText('Stat 1')).toBeNull();
  });

  it('falls back to the legacy label field when statisticValue is absent', () => {
    renderModal(
      comparisonWith({
        // BOTH sides legacy-shaped: the home side's statisticValue would
        // (correctly) win the chain otherwise.
        awayEntries: [{ label: 'Legacy Label Stat', displayValue: '12' }],
        homeEntries: [{ label: 'Legacy Label Stat', displayValue: '9' }],
      })
    );

    fireEvent.press(screen.getByText('defensive (1:0)'));
    expect(screen.getByText('Legacy Label Stat')).toBeTruthy();
  });

  it('hides the Metrics tab for the empty zeroed DTO the API returns when no metrics exist', () => {
    // GetTeamMetrics deliberately returns HTTP 200 with an empty DTO
    // (gamesPlayed 0, decimals 0) for not-yet-generated metrics — non-null
    // alone must NOT light the tab as "Metrics (0:0)" full of fake zeros.
    renderModal(
      comparisonWith({ awayMetrics: { gamesPlayed: 0 }, homeMetrics: { gamesPlayed: 0 } })
    );

    expect(screen.queryByText(/^Metrics \(/)).toBeNull();
  });

  it('lands on the metrics rows by default when statistics are empty', () => {
    // The stats-empty + metrics-present path the mainTab fallthrough and
    // relaxed empty-state guard exist for (week-1 first meeting with a
    // stats soft-failure): the body must render the metrics rows, not
    // "Stats not available."
    renderModal(
      comparisonWith({
        statistics: {},
        awayMetrics: { gamesPlayed: 1, ypp: 6.0 },
        homeMetrics: { gamesPlayed: 1, ypp: 5.0 },
      })
    );

    expect(screen.getByText('Yards Per Play')).toBeTruthy();
    expect(screen.queryByText('Stats not available.')).toBeNull();
  });

  it('pressing the Metrics tab renders formatted rows with inverted opp* highlight', () => {
    renderModal(
      comparisonWith({
        awayMetrics: { gamesPlayed: 1, ypp: 6.0, oppYpp: 0.5 },
        homeMetrics: { gamesPlayed: 2, ypp: 5.0, oppYpp: 0.35 },
      })
    );

    fireEvent.press(screen.getByText('Metrics (1:1)'));

    // Group header + dec2-formatted values from the spec.
    expect(screen.getByText('Offensive Efficiency')).toBeTruthy();
    expect(screen.getByText('6.00')).toBeTruthy();
    expect(screen.getByText('5.00')).toBeTruthy();
    // oppYpp rows render too (0.50 vs 0.35 — home leads the inverted metric).
    expect(screen.getByText('0.35')).toBeTruthy();
    expect(screen.getByText('0.50')).toBeTruthy();
  });

  it('renders "-" for null metric values but a real percent for genuine zero', () => {
    // RzTdRate & co. are decimal? through the whole chain (a team with no
    // red-zone possessions arrives as null). A fabricated '0.0%' is
    // indistinguishable from a genuine 0% — null must render '-' (which
    // parseNumeric maps to null: no bar, no favored tint), while a real
    // 0 must still render '0.0%'.
    renderModal(
      comparisonWith({
        awayMetrics: { gamesPlayed: 1, ypp: 6.0, rzTdRate: null },
        homeMetrics: { gamesPlayed: 1, ypp: 5.0, rzTdRate: 0 },
      })
    );

    fireEvent.press(screen.getByText('Metrics (1:0)'));

    // Home's genuine 0% renders as a value; away's null (and every other
    // absent pct/dec2 metric) renders '-', never a plausible zero.
    expect(screen.getAllByText('0.0%')).toHaveLength(1);
    expect(screen.getAllByText('-').length).toBeGreaterThan(0);
    expect(screen.queryByText('0.00')).toBeNull();
  });

  it('renders The Line as a launch-expanded collapsible with band wording and ATS evidence', () => {
    const comparison = {
      ...comparisonWith(),
      history: {
        headToHead: [],
        awayPriorSeasonGames: [],
        homePriorSeasonGames: [],
        spreadContext: {
          favoriteTeam: 'Tennessee Volunteers',
          underdogTeam: 'Georgia Tech Yellow Jackets',
          magnitude: 12.5,
          spreadDetails: 'TENN -12.5',
          favoriteWonByMargin: null,
          underdogLostByMargin: null,
          favoriteAtsAsBigFavorite: {
            threshold: 10,
            thresholdUpper: 14,
            games: 9,
            covers: 4,
            dataFloorSeason: 2022,
            windowGames: [
              {
                gameDate: '2025-11-22T00:00:00Z',
                seasonYear: 2025,
                opponent: 'Syracuse Orange',
                teamScore: 45,
                opponentScore: 26,
                teamSpread: -13.5,
                covered: true,
                opponentSeasonRecord: '3-9',
              },
            ],
          },
          underdogAtsAsBigUnderdog: null,
        },
      },
    } as unknown as TeamComparisonData;

    render(
      <StatsComparisonModal
        visible
        onClose={() => {}}
        matchup={matchup}
        comparison={comparison}
        isLoading={false}
        showGambling
      />
    );

    // Band wording (never an open-ended "10+") plus the evidence row with
    // the team-relative line and cover marker.
    expect(screen.getByText(/as a 10–14 point favorite/)).toBeTruthy();
    expect(screen.getByText("'25 Syracuse Orange 45-26 (3-9) · -13.5 ✓")).toBeTruthy();

    // Collapsible — unlike the persisted sections it starts expanded on
    // every launch; pressing the header hides the facts.
    fireEvent.press(screen.getByText('The Line: TENN -12.5'));
    expect(screen.queryByText(/as a 10–14 point favorite/)).toBeNull();
  });

  it('shows the Metrics tab with favored counts when both sides have real metrics', () => {
    renderModal(
      comparisonWith({
        awayMetrics: { gamesPlayed: 1, ypp: 6.0, oppYpp: 0.5 },
        homeMetrics: { gamesPlayed: 2, ypp: 5.0, oppYpp: 0.35 },
      })
    );

    // away leads ypp, home leads oppYpp (inverted) → 1:1
    expect(screen.getByText('Metrics (1:1)')).toBeTruthy();
  });
});

describe('StatsComparisonModal head-to-head', () => {
  const h2hHistory = (games: object[]) =>
    ({
      ...comparisonWith(),
      history: {
        headToHead: games,
        awayPriorSeasonGames: [],
        homePriorSeasonGames: [],
        spreadContext: null,
      },
    }) as unknown as TeamComparisonData;

  it('renders short team names in the meeting rows and the ATS line, keeping the tally on full names', () => {
    const comparison = h2hHistory([
      {
        gameDate: '2024-11-23T20:30:00Z',
        seasonYear: 2024,
        phase: 'Regular Season',
        homeTeam: 'Miami Hurricanes',
        awayTeam: 'Florida A&M Rattlers',
        homeTeamShort: 'Miami',
        awayTeamShort: 'Florida A&M',
        homeScore: 56,
        awayScore: 9,
        // Identity fields stay on the full DisplayName - this is what the
        // History tab tally compares against matchup.home/away.
        winner: 'Miami Hurricanes',
        spreadWinner: 'Miami Hurricanes',
        spread: 'MIA -23.5',
        overUnder: 65.5,
        overUnderResult: 'Under',
      },
    ]);

    render(
      <StatsComparisonModal
        visible
        onClose={() => {}}
        matchup={matchup}
        comparison={comparison}
        isLoading={false}
        showGambling
      />
    );

    expect(screen.getByText('Florida A&M 9')).toBeTruthy();
    expect(screen.getByText('Miami 56')).toBeTruthy();
    expect(screen.getByText('ATS: Miami')).toBeTruthy();
    expect(screen.queryByText('Miami Hurricanes 56')).toBeNull();
    expect(screen.getByText('History (0:1)')).toBeTruthy();
  });

  it('falls back to the full names when the payload carries no short names', () => {
    const comparison = h2hHistory([
      {
        gameDate: '2013-10-26T19:30:00Z',
        seasonYear: 2013,
        phase: 'Regular Season',
        homeTeam: 'Miami Hurricanes',
        awayTeam: 'Florida A&M Rattlers',
        homeScore: 41,
        awayScore: 7,
        winner: 'Miami Hurricanes',
        spreadWinner: null,
        spread: null,
        overUnder: null,
        overUnderResult: null,
      },
    ]);

    renderModal(comparison);

    expect(screen.getByText('Florida A&M Rattlers 7')).toBeTruthy();
    expect(screen.getByText('Miami Hurricanes 41')).toBeTruthy();
  });
});

describe('StatsComparisonModal short names in The Line and Last 5 Games', () => {
  it('uses short names for the sentence head, the evidence rows and the prior-season opponent', () => {
    const shortMatchup = { ...matchup, awayShort: 'Florida A&M', homeShort: 'Miami' } as unknown as Matchup;
    const comparison = {
      ...comparisonWith(),
      history: {
        headToHead: [],
        awayPriorSeasonGames: [
          {
            gameDate: '2025-11-22T00:00:00Z',
            seasonYear: 2025,
            phase: 'Regular Season',
            homeTeam: 'Miami Hurricanes',
            awayTeam: 'Florida A&M Rattlers',
            homeTeamShort: 'Miami',
            awayTeamShort: 'Florida A&M',
            homeScore: 38,
            awayScore: 6,
            winner: 'Miami Hurricanes',
          },
        ],
        homePriorSeasonGames: [],
        spreadContext: {
          favoriteTeam: 'Miami Hurricanes',
          underdogTeam: 'Florida A&M Rattlers',
          magnitude: 12.5,
          spreadDetails: 'MIA -12.5',
          favoriteWonByMargin: null,
          underdogLostByMargin: null,
          favoriteAtsAsBigFavorite: {
            threshold: 10,
            thresholdUpper: 14,
            games: 9,
            covers: 4,
            dataFloorSeason: 2022,
            windowGames: [
              {
                gameDate: '2025-11-22T00:00:00Z',
                seasonYear: 2025,
                opponent: 'Syracuse Orange',
                opponentShort: 'Syracuse',
                teamScore: 45,
                opponentScore: 26,
                teamSpread: -13.5,
                covered: true,
                opponentSeasonRecord: '3-9',
              },
            ],
          },
          underdogAtsAsBigUnderdog: null,
        },
      },
    } as unknown as TeamComparisonData;

    render(
      <StatsComparisonModal
        visible
        onClose={() => {}}
        matchup={shortMatchup}
        comparison={comparison}
        isLoading={false}
        showGambling
      />
    );

    expect(screen.getByText('Miami as a 10–14 point favorite:')).toBeTruthy();
    expect(screen.getByText("'25 Syracuse 45-26 (3-9) · -13.5 ✓")).toBeTruthy();
    // Last 5 Games: Florida A&M's row reads "@ Miami", not the full name, while
    // the W/L badge still resolved from the full-name identity fields.
    expect(screen.getByText('@ Miami')).toBeTruthy();
    expect(screen.getByText('L')).toBeTruthy();
  });
});

describe('StatsComparisonModal stacked stat categories', () => {
  it('stacks every category with its tally as a collapsible header', () => {
    renderModal(comparisonWith());

    // The category is a section header carrying its favored tally (away
    // 45 assisted tackles beats home 30), not a chip to swipe to.
    // ...and it starts COLLAPSED, so the tab opens as an index of headers.
    const header = screen.getByText('defensive (1:0)');
    expect(screen.queryByText('Assisted Tackles')).toBeNull();

    fireEvent.press(header);
    expect(screen.getByText('Assisted Tackles')).toBeTruthy();

    fireEvent.press(header);
    expect(screen.queryByText('Assisted Tackles')).toBeNull();
  });
});
