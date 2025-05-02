import { render, screen, act } from '@testing-library/react';
import { AuthProvider, useAuth } from '../../contexts/AuthContext';
import { getAuth, onAuthStateChanged, signOut as firebaseSignOut } from 'firebase/auth';
import apiClient from '../../api/apiClient';

// Mock Firebase auth
jest.mock('firebase/auth', () => ({
  getAuth: jest.fn(),
  onAuthStateChanged: jest.fn(),
  signOut: jest.fn()
}));

// Mock API client
jest.mock('../../api/apiClient', () => ({
  post: jest.fn()
}));

// Test component to use the auth context
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
    jest.clearAllMocks();
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

  it('handles user authentication', async () => {
    const mockUser = {
      uid: 'test-uid',
      getIdToken: jest.fn().mockResolvedValue('test-token')
    };

    // Mock onAuthStateChanged to call the callback with a user
    onAuthStateChanged.mockImplementation((auth, callback) => {
      callback(mockUser);
      return jest.fn(); // Return unsubscribe function
    });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    // Wait for state updates
    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('Not Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('User Present');
    expect(apiClient.post).toHaveBeenCalledWith('/auth/set-token', { token: 'test-token' });
  });

  it('handles user sign out', async () => {
    // Mock onAuthStateChanged to call the callback with null (no user)
    onAuthStateChanged.mockImplementation((auth, callback) => {
      callback(null);
      return jest.fn(); // Return unsubscribe function
    });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    // Wait for state updates
    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('Not Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('No User');
    expect(apiClient.post).toHaveBeenCalledWith('/auth/clear-token');
  });

  it('handles token setup failure', async () => {
    const mockUser = {
      uid: 'test-uid',
      getIdToken: jest.fn().mockRejectedValue(new Error('Token error'))
    };

    // Mock onAuthStateChanged to call the callback with a user
    onAuthStateChanged.mockImplementation((auth, callback) => {
      callback(mockUser);
      return jest.fn(); // Return unsubscribe function
    });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    // Wait for state updates
    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('Not Loading');
    expect(screen.getByTestId('user')).toHaveTextContent('No User');
  });
}); 