import React from 'react';
import { render, screen } from '@testing-library/react-native';
import { FinalScoreResult } from '@/src/components/features/games/FinalScoreResult';

const AWAY_ID = 'fs-away';
const HOME_ID = 'fs-home';
const baseProps = {
  awayFranchiseSeasonId: AWAY_ID,
  homeFranchiseSeasonId: HOME_ID,
  awayShort: 'NYY',
  homeShort: 'BOS',
};

describe('FinalScoreResult — readiness gate', () => {
  it('renders null when pickType is StraightUp (SU is handled inline by the score line)', () => {
    const { toJSON } = render(
      <FinalScoreResult
        pickType="StraightUp"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
      />,
    );
    expect(toJSON()).toBeNull();
  });

  it('returns null on pre-enrichment ATS (winnerFranchiseSeasonId null) instead of showing false Push', () => {
    const { toJSON } = render(
      <FinalScoreResult
        pickType="AgainstTheSpread"
        {...baseProps}
        winnerFranchiseSeasonId={null}
        spreadWinnerFranchiseSeasonId={null}
      />,
    );
    expect(toJSON()).toBeNull();
  });

  it('returns null on pre-enrichment O/U (overUnderResult null) instead of showing false Push', () => {
    const { toJSON } = render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={null}
        overUnderResult={null}
      />,
    );
    expect(toJSON()).toBeNull();
  });

  it('returns null on O/U "None" (enrichment ran but no O/U line)', () => {
    const { toJSON } = render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult="None"
      />,
    );
    expect(toJSON()).toBeNull();
  });
});

describe('FinalScoreResult — ATS verdicts', () => {
  it('shows "✓ {short} covered" with the cover team short', () => {
    render(
      <FinalScoreResult
        pickType="AgainstTheSpread"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        spreadWinnerFranchiseSeasonId={AWAY_ID}
      />,
    );
    expect(screen.getByText('NYY covered')).toBeTruthy();
    expect(screen.getByText('✓')).toBeTruthy();
  });

  it('shows "Push" when enrichment ran but spreadWinnerFranchiseSeasonId is null', () => {
    render(
      <FinalScoreResult
        pickType="AgainstTheSpread"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        spreadWinnerFranchiseSeasonId={null}
      />,
    );
    expect(screen.getByText('Push')).toBeTruthy();
  });

  it('returns null when the spread-winner id matches neither team (defensive)', () => {
    const { toJSON } = render(
      <FinalScoreResult
        pickType="AgainstTheSpread"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        spreadWinnerFranchiseSeasonId="stray-id-not-on-card"
      />,
    );
    expect(toJSON()).toBeNull();
  });
});

describe('FinalScoreResult — O/U verdicts', () => {
  it('shows "Over {N}" for a tracked overUnderCurrent', () => {
    render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult="Over"
        overUnderCurrent={8.5}
      />,
    );
    expect(screen.getByText('Over 8.5')).toBeTruthy();
  });

  it('shows "Under" without value when overUnderCurrent is missing', () => {
    render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult="Under"
      />,
    );
    expect(screen.getByText('Under')).toBeTruthy();
  });

  it('shows "Push" when overUnderResult is the string "Push"', () => {
    render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult="Push"
      />,
    );
    expect(screen.getByText('Push')).toBeTruthy();
  });

  // Wire-shape note in the component: the API DTO uses OverUnderPick
  // (None/Over/Under) but Producer's Contest.OverUnder uses
  // OverUnderResult which has Push=3. MatchupForPickDtoMapper casts the
  // int through, so a real push may arrive as "Push", 3, or "3"
  // depending on serializer behavior on the unnamed enum value.
  it('shows "Push" when overUnderResult arrives as numeric 3 (unnamed enum cast)', () => {
    render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult={3}
      />,
    );
    expect(screen.getByText('Push')).toBeTruthy();
  });

  it('shows "Push" when overUnderResult arrives as the string "3"', () => {
    render(
      <FinalScoreResult
        pickType="OverUnder"
        {...baseProps}
        winnerFranchiseSeasonId={HOME_ID}
        overUnderResult="3"
      />,
    );
    expect(screen.getByText('Push')).toBeTruthy();
  });
});
