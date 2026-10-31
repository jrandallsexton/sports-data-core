import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { CardGameScreen } from '@/src/components/features/cardgame/CardGameScreen';

jest.mock('react-native-safe-area-context', () => require('react-native-safe-area-context/jest/mock').default);
// Signed out in tests, so nothing is recorded; the mock keeps axios/Firebase out of the module graph.
jest.mock('@/src/services/api/miniGamesApi', () => ({
  miniGamesApi: { submitResults: jest.fn(), getStats: jest.fn() },
}));

// Lays the game area out at a phone size so the hand renders.
function layOut() {
  fireEvent(screen.getByTestId('cardgame-area'), 'layout', { nativeEvent: { layout: { x: 0, y: 0, width: 390, height: 700 } } });
}

describe('CardGameScreen', () => {
  it('kicks off and plays a card with one tap', async () => {
    render(<CardGameScreen onExit={jest.fn()} />);
    fireEvent.press(screen.getByText('Kick off'));
    layOut();

    // The first decision is the human's read (CPU receives) or a drive window / commit (human receives).
    const title = await screen.findByText(/^(Defense: commit your read|Offense: commit a play|[A-Za-z-]+ window)$/, {}, { timeout: 4000 });
    const before = title.props.children;
    layOut();

    const playable = screen.queryAllByLabelText(/, playable$/);
    if (playable.length) fireEvent.press(playable[0]);
    else fireEvent.press(screen.getByText(/^Pass$/));

    await waitFor(() => expect(screen.queryByText(before)).toBeNull(), { timeout: 4000 });
    screen.unmount();
  }, 20000);
});
