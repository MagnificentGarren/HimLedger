import { HttpResponse, http } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { AuthProvider } from '../context/AuthContext';
import { server } from '../test/server';
import { Login } from './Login';

describe('Login', () => {
  it('submits credentials and stores the authenticated session', async () => {
    server.use(http.post('http://localhost:5228/api/Auth/login', () => HttpResponse.json({
      token: 'test-token',
      userId: 17,
      firstName: 'Ari',
      lastName: 'Lee',
      role: 'Employee',
    })));

    render(
      <AuthProvider>
        <MemoryRouter>
          <Login />
        </MemoryRouter>
      </AuthProvider>,
    );

    await userEvent.type(screen.getByLabelText('Work email'), 'ari@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'correct-horse');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(localStorage.getItem('token')).toBe('test-token');
      expect(JSON.parse(localStorage.getItem('user') ?? '{}')).toMatchObject({
        userId: 17,
        email: 'ari@example.com',
        role: 'Employee',
      });
    });
  });
});
