import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowRight, Droplet, LockKeyhole, Mail } from 'lucide-react';
import { isAxiosError } from 'axios';
import api from '../api/axios';
import { useAuth } from '../context/AuthContext';

export const Login: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError('');

    try {
      const response = await api.post('/Auth/login', { email, password });
      const { token, userId, firstName, lastName, role } = response.data;

      login(token, { userId, firstName, lastName, email, role });
      navigate('/dashboard');
    } catch (err: unknown) {
      const message = isAxiosError<{ message?: string }>(err) ? err.response?.data?.message : null;
      setError(message || 'Login failed. Check your credentials.');
    }
  };

  return (
    <main className="login-page">
      <div className="login-frame">
        <section className="login-story" aria-label="HimLedger">
          <div className="login-brand">
            <span className="brand-mark"><Droplet size={20} strokeWidth={2.2} /></span>
            HimLedger
          </div>
          <div className="login-story-copy">
            <span className="eyebrow">Finance workspace</span>
            <h1>Every expense,<br /><span>accounted for.</span></h1>
            <p>A clear view of company spending, from the first claim to final approval.</p>
          </div>
          <div className="login-story-foot"><span className="status-dot" /> Secure company access</div>
        </section>

        <section className="login-panel" aria-labelledby="login-title">
          <div className="login-panel-heading">
            <h2 id="login-title">Welcome back</h2>
            <p>Sign in to continue to your workspace.</p>
          </div>

          {error && <div role="alert" className="login-error">{error}</div>}

          <form onSubmit={handleSubmit} className="login-form">
            <div className="login-field">
              <label htmlFor="login-email">Work email</label>
              <div className="login-input-wrap">
                <Mail aria-hidden="true" />
                <input
                  id="login-email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  required
                  placeholder="name@company.com"
                />
              </div>
            </div>

            <div className="login-field">
              <label htmlFor="login-password">Password</label>
              <div className="login-input-wrap">
                <LockKeyhole aria-hidden="true" />
                <input
                  id="login-password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                  placeholder="Enter your password"
                />
              </div>
            </div>

            <button type="submit" className="login-submit">
              Sign in <ArrowRight size={17} aria-hidden="true" />
            </button>
          </form>
          <div className="login-panel-foot">HimLedger · Expense operations</div>
        </section>
      </div>
    </main>
  );
};
