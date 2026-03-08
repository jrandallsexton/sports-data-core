import { useAuthStore } from '@/src/stores/authStore';

describe('authStore', () => {
  beforeEach(() => {
    // Reset store to initial state between tests
    useAuthStore.setState({
      user: null,
      isLoading: false,
      isInitialized: false,
    });
  });

  it('has correct initial state', () => {
    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.isLoading).toBe(false);
    expect(state.isInitialized).toBe(false);
  });

  it('setUser updates user', () => {
    const mockUser = { uid: '123', email: 'test@test.com' } as any;
    useAuthStore.getState().setUser(mockUser);
    expect(useAuthStore.getState().user).toBe(mockUser);
  });

  it('setLoading updates isLoading', () => {
    useAuthStore.getState().setLoading(true);
    expect(useAuthStore.getState().isLoading).toBe(true);
  });

  it('setInitialized updates isInitialized', () => {
    useAuthStore.getState().setInitialized(true);
    expect(useAuthStore.getState().isInitialized).toBe(true);
  });
});
