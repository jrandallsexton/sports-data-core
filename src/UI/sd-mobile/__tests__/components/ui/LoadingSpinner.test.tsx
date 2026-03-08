import React from 'react';
import { render, screen } from '@testing-library/react-native';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';

describe('LoadingSpinner', () => {
  it('renders without crashing', () => {
    render(<LoadingSpinner />);
  });

  it('displays message when provided', () => {
    render(<LoadingSpinner message="Loading data..." />);
    expect(screen.getByText('Loading data...')).toBeTruthy();
  });

  it('does not display message when not provided', () => {
    render(<LoadingSpinner />);
    expect(screen.queryByText('Loading data...')).toBeNull();
  });
});
