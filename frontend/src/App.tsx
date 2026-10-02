import React from 'react';
import { BrowserRouter as Router, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { useAuth, workspacePathForRole } from './context/auth';
import { AppWorkspace } from './pages/AppWorkspace';
import { Login } from './pages/Login';
import { ArchitecturePage, FeaturesPage, HomePage, PricingPage } from './pages/PublicSite';

export type Role = 'Employee' | 'Manager' | 'Finance' | 'Admin';

export const RoleWorkspace: React.FC<{ role: Role }> = ({ role }) => {
  const { token, user } = useAuth();
  if (!token || !user) return <Navigate to="/login" replace />;
  return user.role === role
    ? <AppWorkspace />
    : <Navigate to={workspacePathForRole(user.role)} replace />;
};

const RoleRedirect: React.FC = () => {
  const { token, user } = useAuth();
  return token && user ? <Navigate to={workspacePathForRole(user.role)} replace /> : <Navigate to="/login" replace />;
};

export const App: React.FC = () => {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/features" element={<FeaturesPage />} />
          <Route path="/about" element={<ArchitecturePage />} />
          <Route path="/pricing" element={<PricingPage />} />
          <Route path="/login" element={<Login />} />
          <Route path="/employee/*" element={<RoleWorkspace role="Employee" />} />
          <Route path="/manager/*" element={<RoleWorkspace role="Manager" />} />
          <Route path="/finance/*" element={<RoleWorkspace role="Finance" />} />
          <Route path="/admin/*" element={<RoleWorkspace role="Admin" />} />
          <Route path="/app/*" element={<RoleRedirect />} />
          <Route path="/workspace/*" element={<RoleRedirect />} />
          <Route path="/dashboard" element={<RoleRedirect />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
};

export default App;
