import React from 'react';
import { render, screen } from '@testing-library/react-native';

import {
  StatsComparisonModal,
  statFavored,
  metricFavored,
  METRICS_SPEC,
} from '@/src/components/features/games/StatsComparisonModal';
import type { Matchup, TeamComparisonData } from '@/src/types/models';

// ─── Favored helpers — the math behind every tab/chip count and highlight ─────

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
}): TeamComparisonData =>
  ({
    teamA: {
      name: 'Florida A&M Rattlers',
      stats: {
        data: {
          statistics: {
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
          statistics: {
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
