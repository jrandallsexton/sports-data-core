import { render, screen, act } from '@testing-library/react';
import { AuthProvider, useAuth } from '../../contexts/AuthContext';
import { onAuthStateChanged } from 'firebase/auth';

// Mock Firebase auth. onAuthStateChanged / onIdTokenChanged return an
// unsubscribe fn (AuthContext calls both and cleans them up on unmount).
// AuthContext no longer performs token set/clear itself — that moved to the
// apiClient request interceptor — so these tests only cover the user/loading
// state it derives from onAuthStateChanged.
vi.mock('firebase/auth', () => ({
  getAuth: vi.fn(),
  onAuthStateChanged: vi.fn(() => vi.fn()),
  onIdTokenChanged: vi.fn(() => vi.fn()),
  signOut: vi.fn()
}));

// Test component to read the auth context.
function TestComponent() {
  const { user, loading } = useAuth();
  return (
    <div>
      <div data-testid="loading">{loading ? 'Loading' : 'Not Loading'}</div>
      <div data-testid="user">{user ? 'User Present' : 'No User'}</div>
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: subscribed but no emission yet → still loading.
    onAuthStateChanged.mockImplementation(() => vi.fn());
  });

  it('initializes with loading state', () => {
    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    expect(screen.getByTestId('loading')).toHaveTextContent('Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('No User');
  });

  it('reflects an authenticated user', async () => {
    const mockUser = { uid: 'test-uid' };
    onAuthStateChanged.mockImplementation((auth, callback) => {
      callback(mockUser);
      return vi.fn();
    });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('Not Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('User Present');
  });

  it('reflects a signed-out user', async () => {
    onAuthStateChanged.mockImplementation((auth, callback) => {
      callback(null);
      return vi.fn();
    });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('Not Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('No User');
  });
});
