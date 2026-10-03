import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, ArrowRight, Eye, EyeOff, LockKeyhole, Mail } from 'lucide-react';
import { isAxiosError } from 'axios';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import api from '../api/axios';
import type { LoginDto } from '../api/generated/types.gen';
import { useAuth, workspacePathForRole } from '../context/auth';
import { beginEntraSignIn, completeEntraSignIn, isEntraConfigured } from '../auth/entra';
import { BrandMark } from './PublicSite';

const loginSchema = z.object({
  email: z.email('Enter a valid work email.'),
  password: z.string().min(1, 'Enter your password.'),
});
type LoginFormValues = z.infer<typeof loginSchema>;
interface CurrentUser {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  departmentId: number | null;
}

export const Login: React.FC = () => {
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const localLoginEnabled = import.meta.env.DEV || import.meta.env.VITE_ENABLE_LOCAL_LOGIN === 'true';
  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  useEffect(() => {
    let active = true;
    void completeEntraSignIn()
      .then(async (result) => {
        if (!active || !result?.accessToken) return;
        const { data: user } = await api.get<CurrentUser>('/Auth/me', {
          headers: { Authorization: `Bearer ${result.accessToken}` },
        });
        if (!active) return;
        login(result.accessToken, user, 'entra');
        navigate(workspacePathForRole(user.role), { replace: true });
      })
      .catch((cause: unknown) => {
        console.error('Entra sign-in could not be completed', cause);
        if (active) setError('Company sign-in could not be completed. Try again or contact your administrator.');
      });
    return () => { active = false; };
  }, [login, navigate]);

  const handleSubmit = async ({ email, password }: LoginFormValues) => {
    setError('');
    setIsSubmitting(true);

    try {
      const request: LoginDto = { email, password };
      const response = await api.post('/Auth/login', request);
      const { token, userId, firstName, lastName, role } = response.data;

      login(token, { userId, firstName, lastName, email, role });
      navigate(workspacePathForRole(role), { replace: true });
    } catch (err: unknown) {
      const message = isAxiosError<{ detail?: string; message?: string }>(err)
        ? err.response?.data?.detail ?? err.response?.data?.message
        : null;
      const isUnavailable = isAxiosError(err) && !err.response;
      setError(message || (isUnavailable
        ? 'Unable to reach HimLedger. Check that the API is running and try again.'
        : "We couldn't sign you in. Verify your work email and password, or contact your administrator."));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEntraSignIn = async () => {
    setError('');
    try {
      await beginEntraSignIn();
    } catch (cause: unknown) {
      console.error('Entra sign-in could not be started', cause);
      setError('Company sign-in could not be started. Try again or contact your administrator.');
    }
  };

  return (
    <main className="login-page">
      <div className="login-frame">
        <section className="login-story" aria-label="HimLedger">
          <Link className="login-brand" to="/" aria-label="HimLedger home">
            <span className="brand-mark"><BrandMark size={25} /></span>
            HimLedger
          </Link>
          <div className="login-story-copy">
            <span className="eyebrow">Finance workspace</span>
            <h1>Every expense,<br /><span>accounted for.</span></h1>
            <p>A clear view of company spending, from the first claim to final approval.</p>
          </div>
          <div className="login-story-foot"><span className="status-dot" /> Secure company access</div>
        </section>

        <section className="login-panel" aria-labelledby="login-title">
          <Link to="/" className="login-back-link"><ArrowLeft size={15} aria-hidden="true" /> Back to website</Link>
          <div className="login-panel-heading">
            <h2 id="login-title">Welcome back</h2>
            <p>Sign in to continue to your workspace.</p>
          </div>

          {error && <div role="alert" className="login-error">{error}</div>}

          {isEntraConfigured && <button type="button" className="login-submit" onClick={() => void handleEntraSignIn()}>
            Sign in with your organization <ArrowRight size={17} aria-hidden="true" />
          </button>}
          {isEntraConfigured && localLoginEnabled && <p className="login-panel-foot">or use local development sign-in</p>}
          {localLoginEnabled ? <form onSubmit={form.handleSubmit(handleSubmit)} className="login-form">
            <div className="login-field">
              <label htmlFor="login-email">Work email</label>
              <div className="login-input-wrap">
                <Mail aria-hidden="true" />
                <input id="login-email" type="email" autoComplete="username" {...form.register('email')} placeholder="name@company.com" />
              </div>
              {form.formState.errors.email && <small role="alert" className="login-error">{form.formState.errors.email.message}</small>}
            </div>
            <div className="login-field">
              <label htmlFor="login-password">Password</label>
              <div className="login-input-wrap">
                <LockKeyhole aria-hidden="true" />
                <input id="login-password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" {...form.register('password')} placeholder="Enter your password" />
                <button type="button" className="login-password-toggle" aria-label={showPassword ? 'Hide password' : 'Show password'} aria-pressed={showPassword} onClick={() => setShowPassword((visible) => !visible)}>
                  {showPassword ? <EyeOff size={17} aria-hidden="true" /> : <Eye size={17} aria-hidden="true" />}
                </button>
              </div>
              {form.formState.errors.password && <small role="alert" className="login-error">{form.formState.errors.password.message}</small>}
            </div>
            <button type="submit" className="login-submit" disabled={isSubmitting}>
              {isSubmitting ? 'Signing in...' : 'Sign in'} <ArrowRight size={17} aria-hidden="true" />
            </button>
            <p className="login-help">Forgot your password or need access? <a href="mailto:support@himledger.com">Contact support</a> for help.</p>
          </form> : !isEntraConfigured && <p role="status">Company sign-in is not configured. Contact your administrator.</p>}
          <div className="login-panel-foot">HimLedger · Expense operations</div>
        </section>
      </div>
    </main>
  );
};
