import React from 'react';
import { render, screen } from '@testing-library/react-native';

import { DeetsMeter } from '@/src/components/features/games/DeetsMeter';
import type { ContestPrediction } from '@/src/types/models';

const CID = '00000000-0000-0000-0000-000000000001';
const AWAY_FSID = '00000000-0000-0000-0000-0000000000a1';
const HOME_FSID = '00000000-0000-0000-0000-0000000000a2';

const prediction = (overrides?: Partial<ContestPrediction>): ContestPrediction => ({
  contestId: CID,
  winnerFranchiseSeasonId: HOME_FSID,
  winProbability: 0.72,
  predictionType: 'StraightUp',
  modelVersion: 'v1.1.2',
  ...overrides,
});

const renderMeter = (
  predictions: ContestPrediction[] | undefined,
  pickType?: 'StraightUp' | 'AgainstTheSpread' | null
) =>
  render(
    <DeetsMeter
      predictions={predictions}
      pickType={pickType}
      homeFranchiseSeasonId={HOME_FSID}
      awayFranchiseSeasonId={AWAY_FSID}
    />
  );

describe('DeetsMeter', () => {
  it('renders nothing when there are no predictions', () => {
    renderMeter(undefined, null);
    expect(screen.queryByTestId('deetsmeter')).toBeNull();
  });

  it('renders nothing when predictions exist but none are SU or ATS', () => {
    renderMeter([prediction({ predictionType: 'OverUnder' })], null);
    expect(screen.queryByTestId('deetsmeter')).toBeNull();
  });

  it('shows only the SU meter in a StraightUp league', () => {
    renderMeter(
      [
        prediction(),
        prediction({ predictionType: 'AgainstTheSpread', winProbability: 0.55 }),
      ],
      'StraightUp'
    );
    expect(screen.getByTestId('deetsmeter-su')).toBeTruthy();
    expect(screen.queryByTestId('deetsmeter-ats')).toBeNull();
  });

  it('shows only the ATS meter in an AgainstTheSpread league', () => {
    renderMeter(
      [
        prediction(),
        prediction({ predictionType: 'AgainstTheSpread', winProbability: 0.55 }),
      ],
      'AgainstTheSpread'
    );
    expect(screen.queryByTestId('deetsmeter-su')).toBeNull();
    expect(screen.getByTestId('deetsmeter-ats')).toBeTruthy();
  });

  it('shows both meters when pickType is not supplied (web parity)', () => {
    renderMeter(
      [
        prediction(),
        prediction({ predictionType: 'AgainstTheSpread', winProbability: 0.55 }),
      ],
      null
    );
    expect(screen.getByTestId('deetsmeter-su')).toBeTruthy();
    expect(screen.getByTestId('deetsmeter-ats')).toBeTruthy();
  });

  it('computes home/away percentages when home is favored', () => {
    // winProbability belongs to the WINNER side: home at 0.72 → 72 / 28.
    renderMeter([prediction()], 'StraightUp');
    expect(screen.getByText('72%')).toBeTruthy();
    expect(screen.getByText('28%')).toBeTruthy();
  });

  it('inverts the percentage when away is favored', () => {
    // Away at 0.72 → home is round(1 - 0.72) = 28, away the complement 72.
    renderMeter(
      [prediction({ winnerFranchiseSeasonId: AWAY_FSID })],
      'StraightUp'
    );
    expect(screen.getByText('72%')).toBeTruthy();
    expect(screen.getByText('28%')).toBeTruthy();
  });

  it('percentages always sum to 100 across rounding (web-verbatim math)', () => {
    // 0.505 → home round(50.5) = 51, away = 100 - 51 = 49 (not round(49.5)=50).
    renderMeter([prediction({ winProbability: 0.505 })], 'StraightUp');
    expect(screen.getByText('51%')).toBeTruthy();
    expect(screen.getByText('49%')).toBeTruthy();
  });

  it('renders the deetsMeter header', () => {
    renderMeter([prediction()], 'StraightUp');
    expect(screen.getByText('deetsMeter™')).toBeTruthy();
  });
});
