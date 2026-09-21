import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { PickAccuracyCard } from '@/src/components/features/home/PickAccuracyCard';
import type { PickAccuracyByWeek } from '@/src/types/models';

const week = (week: number, correct: number, total: number) => ({
  week,
  correctPicks: correct,
  totalPicks: total,
  accuracyPercent: Math.round((correct / total) * 1000) / 10,
});

const entry = (
  leagueId: string,
  leagueName: string,
  weeks: ReturnType<typeof week>[],
  overall: number,
): PickAccuracyByWeek => ({
  userId: 'u1',
  userName: 'Randall',
  leagueId,
  leagueName,
  weeklyAccuracy: weeks,
  overallAccuracyPercent: overall,
});

const alpha = entry('a', 'Alpha League', [week(1, 6, 10), week(2, 8, 10), week(3, 5, 10)], 63.3);
const beta = entry('b', 'Beta League', [week(1, 9, 10)], 90);

describe('PickAccuracyCard', () => {
  it('renders nothing with no leagues', () => {
    const { toJSON } = render(<PickAccuracyCard leagues={[]} />);
    expect(toJSON()).toBeNull();
  });

  it('shows the season figure, one bar per graded week, and the league name when there is one league', () => {
    render(<PickAccuracyCard leagues={[alpha]} />);

    expect(screen.getByTestId('season-percent').props.children).toBe('63.3%');
    expect(screen.getByText('season · 19/30 correct')).toBeTruthy();
    expect(screen.getByText('Alpha League')).toBeTruthy();

    expect(screen.getByTestId('bar-week-1')).toBeTruthy();
    expect(screen.getByTestId('bar-week-2')).toBeTruthy();
    expect(screen.getByTestId('bar-week-3')).toBeTruthy();
    expect(screen.queryByTestId('bar-week-4')).toBeNull();

    // No picker for a single league.
    expect(screen.queryByLabelText(/^Show /)).toBeNull();
  });

  it('scales bars to the weekly percentage', () => {
    render(<PickAccuracyCard leagues={[alpha]} />);
    const flat = (id: string) => {
      const style = screen.getByTestId(id).props.style;
      return Object.assign({}, ...(Array.isArray(style) ? style.flat() : [style]));
    };
    // 80% of the 120px plot, 60%, 50%.
    expect(flat('bar-week-2').height).toBeCloseTo(96, 5);
    expect(flat('bar-week-1').height).toBeCloseTo(72, 5);
    expect(flat('bar-week-3').height).toBeCloseTo(60, 5);
  });

  it('places the mean line at the mean of the weekly percentages', () => {
    render(<PickAccuracyCard leagues={[alpha]} />);
    const style = screen.getByTestId('mean-line').props.style;
    const flat = Object.assign({}, ...(Array.isArray(style) ? style.flat() : [style]));
    // mean(60, 80, 50) = 63.33 → 63.33% of 120px.
    expect(flat.bottom).toBeCloseTo(76, 0);
    expect(screen.getByText(/mean 63.3%/)).toBeTruthy();
  });

  it('offers a chip per league when there are several, and switches on tap', () => {
    render(<PickAccuracyCard leagues={[alpha, beta]} />);

    expect(screen.getAllByLabelText(/^Show /)).toHaveLength(2);
    expect(screen.getByTestId('season-percent').props.children).toBe('63.3%');

    fireEvent.press(screen.getByText('Beta League'));

    expect(screen.getByTestId('season-percent').props.children).toBe('90%');
    expect(screen.getByText('season · 9/10 correct')).toBeTruthy();
    expect(screen.getByTestId('bar-week-1')).toBeTruthy();
    expect(screen.queryByTestId('bar-week-2')).toBeNull();
  });

  it('falls back to the first league when the selected one disappears', () => {
    const { rerender } = render(<PickAccuracyCard leagues={[alpha, beta]} />);
    fireEvent.press(screen.getByText('Beta League'));
    expect(screen.getByTestId('season-percent').props.children).toBe('90%');

    rerender(<PickAccuracyCard leagues={[alpha]} />);
    expect(screen.getByTestId('season-percent').props.children).toBe('63.3%');
    expect(screen.queryByText('Beta League')).toBeNull();
  });
});
