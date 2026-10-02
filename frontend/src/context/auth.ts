import { createContext, useContext } from 'react';

export interface User {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  departmentId?: number | null;
}

export type AuthMethod = 'local' | 'entra';

export const workspacePathForRole = (role: string | undefined): string => {
  switch (role) {
    case 'Manager': return '/manager/dashboard';
    case 'Finance': return '/finance/dashboard';
    case 'Admin': return '/admin/dashboard';
    default: return '/employee/dashboard';
  }
};

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (token: string, user: User, method?: AuthMethod) => void;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
};
