import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import UsersApi from '../api/usersApi';
import { useAuth } from './AuthContext';

const UserContext = createContext();

export const UserProvider = ({ children }) => {
  const { user, loading: authLoading } = useAuth();
  const [userDto, setUserDto] = useState(null);
  const [loading, setLoading] = useState(true);

  const loadUserDto = async () => {
    if (!user) {
      setUserDto(null);
      setLoading(false);
      return;
    }

    try {
      const response = await UsersApi.getCurrentUser();
      setUserDto(response.data);
    } catch (err) {
      console.error('Failed to load user DTO:', err);
      setUserDto(null);
    } finally {
      setLoading(false);
    }
  };

  const refreshUserDto = useCallback(async () => {
    if (!user) return;
    
    try {
      const response = await UsersApi.getCurrentUser();
      setUserDto(response.data);
    } catch (err) {
      console.error('Failed to refresh user DTO:', err);
    }
  }, [user]);

  useEffect(() => {
    if (!authLoading) {
      loadUserDto();
    }
  }, [user, authLoading]);

  return (
    <UserContext.Provider value={{ userDto, loading, refreshUserDto }}>
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
