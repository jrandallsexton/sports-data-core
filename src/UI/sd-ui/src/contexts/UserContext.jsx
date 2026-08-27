import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import UsersApi from '../api/usersApi';
import { useAuth } from './AuthContext';

const UserContext = createContext();

export const UserProvider = ({ children }) => {
  const { user, loading: authLoading } = useAuth();
  const [userDto, setUserDto] = useState(null);
  const [loading, setLoading] = useState(true);
  // Typed per-user options (UserOptionsDto). null until loaded — consumers
  // treat null as all-defaults via shouldShowGambling & co., so a fetch
  // failure degrades to the safe defaults rather than blocking render.
  const [userOptions, setUserOptions] = useState(null);

  const loadUserDto = useCallback(async () => {
    if (!user) {
      setUserDto(null);
      setUserOptions(null);
      setLoading(false);
      return;
    }

    try {
      const response = await UsersApi.getCurrentUser();
      setUserDto(response.data);
      // Options ride the /user/me payload — no second round-trip. The
      // ?? null keeps pre-rollout cached responses (no options field)
      // on the safe-defaults path.
      setUserOptions(response.data?.options ?? null);
    } catch (err) {
      console.error('Failed to load user DTO:', err);
      setUserDto(null);
      setUserOptions(null);
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Returns true when the refresh succeeded — callers that navigate based on
  // the refreshed DTO (e.g. PendingInvitesCard accepting an invite) check it;
  // existing fire-and-forget callers ignore the return value unchanged.
  const refreshUserDto = useCallback(async () => {
    if (!user) return false;

    try {
      const response = await UsersApi.getCurrentUser();
      setUserDto(response.data);
      setUserOptions(response.data?.options ?? null);
      return true;
    } catch (err) {
      console.error('Failed to refresh user DTO:', err);
      return false;
    }
  }, [user]);

  useEffect(() => {
    if (!authLoading) {
      loadUserDto();
    }
  }, [user, authLoading, loadUserDto]);

  // Optimistic write-through for the settings toggle: apply locally, PATCH,
  // revert on failure. Returns true on success so the caller can message.
  const updateUserOptions = useCallback(async (next) => {
    const previous = userOptions;
    setUserOptions(next);
    try {
      await UsersApi.updateUserOptions(next);
      return true;
    } catch (err) {
      console.error('Failed to update user options:', err);
      setUserOptions(previous);
      return false;
    }
  }, [userOptions]);

  return (
    <UserContext.Provider
      value={{ userDto, loading, refreshUserDto, userOptions, updateUserOptions }}
    >
      {children}
    </UserContext.Provider>
  );
};

export const useUserDto = () => {
  const context = useContext(UserContext);
  if (!context) {
    throw new Error('useUserDto must be used within a UserProvider');
  }
  return context;
};
