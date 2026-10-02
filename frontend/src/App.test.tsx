import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from './context/AuthContext';
import { RoleWorkspace } from './App';

vi.mock('./pages/AppWorkspace', () => ({
  AppWorkspace: () => {
    const location = useLocation();
    return <div>workspace:{location.pathname}</div>;
  },
}));

const renderWorkspace = (path: string, role: string) => {
  localStorage.setItem('token', 'test-token');
  localStorage.setItem('user', JSON.stringify({
    userId: 4,
    firstName: 'Test',
    lastName: 'User',
    email: 'test@example.com',
    role,
  }));
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/employee/*" element={<RoleWorkspace role="Employee" />} />
          <Route path="/manager/*" element={<RoleWorkspace role="Manager" />} />
          <Route path="/finance/*" element={<RoleWorkspace role="Finance" />} />
          <Route path="/admin/*" element={<RoleWorkspace role="Admin" />} />
          <Route path="/login" element={<div>login</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
};

describe('role workspaces', () => {
  beforeEach(() => localStorage.clear());

  it.each([
    ['Employee', '/employee/claims'],
    ['Manager', '/manager/claims'],
    ['Finance', '/finance/audit-logs'],
    ['Admin', '/admin/user-roles'],
  ])('%s can open only its dedicated workspace route', (role, path) => {
    renderWorkspace(path, role);

    expect(screen.getByText(`workspace:${path}`)).toBeVisible();
  });

  it('redirects an employee away from the manager route', async () => {
    renderWorkspace('/manager/claims', 'Employee');

    expect(await screen.findByText('workspace:/employee/dashboard')).toBeVisible();
  });
});
