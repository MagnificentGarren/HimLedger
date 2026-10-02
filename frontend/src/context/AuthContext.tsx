import React, { useCallback, useState } from 'react';
import { AuthContext, type AuthMethod, type User } from './auth';

const storedUser = (): User | null => {
  try {
    const value = localStorage.getItem('user');
    return value ? JSON.parse(value) as User : null;
  } catch {
    localStorage.removeItem('user');
    localStorage.removeItem('token');
    localStorage.removeItem('authMethod');
    return null;
  }
};

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('token'));
  const [user, setUser] = useState<User | null>(() => storedUser());

  const login = useCallback((newToken: string, newUser: User, method: AuthMethod = 'local') => {
    const sessionToken = method === 'entra' ? 'entra-session' : newToken;
    localStorage.setItem('token', sessionToken);
    localStorage.setItem('user', JSON.stringify(newUser));
    localStorage.setItem('authMethod', method);
    setToken(sessionToken);
    setUser(newUser);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    localStorage.removeItem('authMethod');
    setToken(null);
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, token, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};
