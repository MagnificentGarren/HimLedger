import React from 'react';
import { BrowserRouter as Router, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import { AppWorkspace } from './pages/AppWorkspace';
import { AuditLogsPage } from './pages/AuditLogs';
import { BudgetsPage } from './pages/Budgets';
import { ClaimsPage } from './pages/Claims';
import { DepartmentsAdminPage } from './pages/DepartmentsAdmin';
import { Login } from './pages/Login';
import { ArchitecturePage, FeaturesPage, HomePage, PricingPage } from './pages/PublicSite';

const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { token } = useAuth();

  return token ? <>{children}</> : <Navigate to="/login" replace />;
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
          <Route
            path="/app/*"
            element={
              <ProtectedRoute>
                <AppWorkspace />
              </ProtectedRoute>
            }
          />
          <Route path="/workspace" element={<Navigate to="/workspace/overview" replace />} />
          <Route path="/workspace/overview" element={<ProtectedRoute><AppWorkspace /></ProtectedRoute>} />
          <Route path="/workspace/claims" element={<ProtectedRoute><ClaimsPage /></ProtectedRoute>} />
          <Route path="/workspace/budgets" element={<ProtectedRoute><BudgetsPage /></ProtectedRoute>} />
          <Route path="/workspace/audit-logs" element={<ProtectedRoute><AuditLogsPage /></ProtectedRoute>} />
          <Route path="/workspace/departments" element={<ProtectedRoute><DepartmentsAdminPage /></ProtectedRoute>} />
          <Route path="/workspace/user-roles" element={<ProtectedRoute><DepartmentsAdminPage /></ProtectedRoute>} />
          <Route path="/workspace/users" element={<ProtectedRoute><AppWorkspace /></ProtectedRoute>} />
          <Route path="/workspace/*" element={<ProtectedRoute><AppWorkspace /></ProtectedRoute>} />
          <Route path="/dashboard" element={<Navigate to="/app/dashboard" replace />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
};

export default App;
