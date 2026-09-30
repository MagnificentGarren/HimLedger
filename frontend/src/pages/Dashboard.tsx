import React, { useEffect, useState } from 'react';
import {
  BadgeCheck,
  Building2,
  Check,
  CircleDollarSign,
  Clock3,
  Droplet,
  LayoutDashboard,
  LogOut,
  Plus,
  Receipt,
  WalletCards,
  X,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axios';
import { useAuth } from '../context/AuthContext';

interface Expense {
  expenseId: number;
  title: string;
  categoryName: string;
  departmentName: string;
  amount: number;
  status: string;
  createdAt: string;
  expenseDate: string;
}

interface Category {
  categoryId: number;
  name: string;
}

interface Department {
  departmentId: number;
  name: string;
}

interface Budget {
  budgetId: number;
  departmentId: number;
  departmentName: string;
  allocatedAmount: number;
  spentAmount: number;
  remainingAmount: number;
}

export const Dashboard: React.FC = () => {
  const { user, logout } = useAuth();
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [budgets, setBudgets] = useState<Budget[]>([]);
  const [loading, setLoading] = useState(true);
  const [showClaimForm, setShowClaimForm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [updatingExpenseId, setUpdatingExpenseId] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [formError, setFormError] = useState('');
  const [claim, setClaim] = useState({
    title: '',
    categoryId: '',
    departmentId: '',
    amount: '',
    expenseDate: new Date().toISOString().slice(0, 10),
  });
  const navigate = useNavigate();
  const canReview = user?.role === 'Manager' || user?.role === 'Admin';

  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        await Promise.all([
          api.get<Expense[]>('/Expenses').then(({ data }) => setExpenses(data)),
          api.get<Category[]>('/Categories').then(({ data }) => setCategories(data)),
          api.get<Department[]>('/Departments').then(({ data }) => setDepartments(data)),
          api.get<Budget[]>('/Budgets').then(({ data }) => setBudgets(data)),
        ]);
      } catch (error) {
        console.error('Failed to load dashboard data', error);
        setError('Dashboard data could not be loaded. Please refresh and try again.');
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, []);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleClaimSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError('');

    try {
      const response = await api.post<Expense>('/Expenses', {
        title: claim.title,
        categoryId: Number(claim.categoryId),
        departmentId: Number(claim.departmentId),
        amount: Number(claim.amount),
        expenseDate: `${claim.expenseDate}T00:00:00`,
        description: null,
        receiptUrl: null,
      });
      setExpenses((current) => [response.data, ...current]);
      setShowClaimForm(false);
      setClaim({
        title: '',
        categoryId: '',
        departmentId: '',
        amount: '',
        expenseDate: new Date().toISOString().slice(0, 10),
      });
    } catch (submitError) {
      console.error('Failed to submit expense claim', submitError);
      setFormError('The claim could not be submitted. Check the details and try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleReview = async (expenseId: number, status: 'Approved' | 'Rejected') => {
    setUpdatingExpenseId(expenseId);
    setError('');
    try {
      await api.put(`/Expenses/${expenseId}/status`, { status, comments: null });
      setExpenses((current) => current.map((expense) =>
        expense.expenseId === expenseId ? { ...expense, status } : expense,
      ));
    } catch (reviewError) {
      console.error(`Failed to ${status.toLowerCase()} expense`, reviewError);
      setError(`The expense could not be ${status.toLowerCase()}. Please try again.`);
    } finally {
      setUpdatingExpenseId(null);
    }
  };

  const totalExpenses = expenses.reduce((total, expense) => total + expense.amount, 0);
  const pendingCount = expenses.filter((expense) => expense.status === 'Pending').length;
  const approvedCount = expenses.filter((expense) => expense.status === 'Approved').length;
  const formatAmount = (amount: number) => amount.toLocaleString('en-ZA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

  return (
    <div className="workspace-shell">
      <header className="workspace-nav">
        <a className="workspace-brand" href="/dashboard" aria-label="HimLedger overview">
          <span className="brand-mark"><Droplet size={20} strokeWidth={2.2} /></span>
          HimLedger
        </a>
        <nav className="workspace-links" aria-label="Workspace">
          <a className="workspace-link is-active" href="#overview"><LayoutDashboard size={16} /> Overview</a>
          <a className="workspace-link" href="#budgets"><WalletCards size={16} /> Budgets</a>
          <a className="workspace-link" href="#expense-list"><Receipt size={16} /> Claims</a>
        </nav>
        <div className="workspace-user">
          <div className="user-copy">
            <strong>{user?.firstName ?? 'User'} {user?.lastName ?? ''}</strong>
            <span>{user?.role ?? 'Employee'}</span>
          </div>
          <button type="button" onClick={handleLogout} className="signout-button" title="Sign out">
            <LogOut size={16} /><span>Sign out</span>
          </button>
        </div>
      </header>

      <main className="workspace-main" id="overview">
        <div className="workspace-heading">
          <div>
            <span className="eyebrow">Finance / Operations</span>
            <h1>Expense overview</h1>
            <p>Company spend, budgets and approvals in one place.</p>
          </div>
          <button
            type="button"
            onClick={() => {
              setFormError('');
              setShowClaimForm(true);
            }}
            className="primary-action"
          >
            <Plus size={17} /> New claim
          </button>
        </div>

        {error && <p role="alert" className="workspace-alert">{error}</p>}

        <section className="metric-grid" aria-label="Expense summary">
          <article className="metric-card metric-card--spend">
            <span className="metric-icon"><CircleDollarSign size={19} /></span>
            <span className="metric-label">Total claimed</span>
            <strong className="metric-value">R {formatAmount(totalExpenses)}</strong>
            <span className="metric-note">Across all expense claims</span>
          </article>
          <article className="metric-card">
            <span className="metric-icon"><Receipt size={19} /></span>
            <span className="metric-label">Claims submitted</span>
            <strong className="metric-value">{expenses.length}</strong>
            <span className="metric-note">All recorded expenses</span>
          </article>
          <article className="metric-card">
            <span className="metric-icon metric-icon--amber"><Clock3 size={19} /></span>
            <span className="metric-label">Awaiting review</span>
            <strong className="metric-value">{pendingCount}</strong>
            <span className="metric-note">Pending decisions</span>
          </article>
          <article className="metric-card">
            <span className="metric-icon metric-icon--green"><BadgeCheck size={19} /></span>
            <span className="metric-label">Approved claims</span>
            <strong className="metric-value">{approvedCount}</strong>
            <span className="metric-note">Cleared for processing</span>
          </article>
        </section>

        <section className="budget-section" id="budgets" aria-label="Departmental budget health">
          <div className="section-heading">
            <div>
              <span className="eyebrow">Allocation</span>
              <h2>Department budgets</h2>
            </div>
            <span className="section-meta"><Building2 size={15} /> {budgets.length} departments</span>
          </div>
          <div className="budget-grid">
            {budgets.map((budget) => {
              const percentSpent = budget.allocatedAmount > 0
                ? Math.min((budget.spentAmount / budget.allocatedAmount) * 100, 100)
                : 0;
              const budgetState = percentSpent >= 90 ? 'is-critical' : percentSpent >= 70 ? 'is-warning' : '';

              return (
                <article key={budget.budgetId} className="budget-card">
                  <div className="budget-card-top">
                    <div className="budget-name-wrap">
                      <span className="budget-icon"><Building2 size={16} /></span>
                      <h3>{budget.departmentName}</h3>
                    </div>
                    <span className={`budget-percent ${budgetState}`}>{percentSpent.toFixed(0)}%</span>
                  </div>
                  <div className="budget-track" role="progressbar" aria-valuenow={Math.round(percentSpent)} aria-valuemin={0} aria-valuemax={100} aria-label={`${budget.departmentName} budget used`}>
                    <span className={budgetState} style={{ width: `${percentSpent}%` }} />
                  </div>
                  <div className="budget-values">
                    <span>R {formatAmount(budget.spentAmount)} <small>spent</small></span>
                    <span>R {formatAmount(budget.remainingAmount)} <small>remaining</small></span>
                  </div>
                </article>
              );
            })}
            {!loading && budgets.length === 0 && (
              <p className="empty-inline">No budgets are configured for the current period.</p>
            )}
            {loading && budgets.length === 0 && <div className="budget-skeleton" aria-label="Loading budgets" />}
          </div>
        </section>

        <section className="claims-section" id="expense-list" aria-labelledby="claims-title">
          <div className="section-heading claims-heading">
            <div>
              <span className="eyebrow">Activity</span>
              <h2 id="claims-title">Expense claims</h2>
            </div>
            <span className="section-meta">{expenses.length} {expenses.length === 1 ? 'record' : 'records'}</span>
          </div>

          <div className="claims-table-wrap">
            {loading ? (
              <div className="table-message"><span className="loading-indicator" />Loading expense data...</div>
            ) : (
              <table className="claims-table">
                <thead>
                  <tr>
                    <th scope="col">Expense</th>
                    <th scope="col">Category</th>
                    <th scope="col">Department</th>
                    <th scope="col">Date</th>
                    <th scope="col">Amount</th>
                    <th scope="col">Status</th>
                    {canReview && <th scope="col">Review</th>}
                  </tr>
                </thead>
                <tbody>
                  {expenses.length === 0 ? (
                    <tr>
                      <td colSpan={canReview ? 7 : 6}>
                        <div className="table-empty"><span><Receipt size={19} /></span><strong>No expense records yet</strong><small>New claims will appear here.</small></div>
                      </td>
                    </tr>
                  ) : (
                    expenses.map((expense) => (
                      <tr key={expense.expenseId}>
                        <td data-label="Expense"><span className="expense-title">{expense.title}</span></td>
                        <td data-label="Category">{expense.categoryName}</td>
                        <td data-label="Department">{expense.departmentName}</td>
                        <td data-label="Date">{new Date(expense.expenseDate).toLocaleDateString('en-ZA', { day: '2-digit', month: 'short', year: 'numeric' })}</td>
                        <td data-label="Amount" className="amount-cell">R {formatAmount(expense.amount)}</td>
                        <td data-label="Status"><span className={`status-label status--${expense.status.toLowerCase()}`}>{expense.status}</span></td>
                        {canReview && (
                          <td data-label="Review">
                            {expense.status === 'Pending' && (
                              <div className="review-actions">
                                <button
                                  type="button"
                                  title="Approve expense"
                                  aria-label={`Approve ${expense.title}`}
                                  disabled={updatingExpenseId === expense.expenseId}
                                  onClick={() => handleReview(expense.expenseId, 'Approved')}
                                  className="review-button is-approved"
                                ><Check size={16} /></button>
                                <button
                                  type="button"
                                  title="Reject expense"
                                  aria-label={`Reject ${expense.title}`}
                                  disabled={updatingExpenseId === expense.expenseId}
                                  onClick={() => handleReview(expense.expenseId, 'Rejected')}
                                  className="review-button is-rejected"
                                ><X size={16} /></button>
                              </div>
                            )}
                          </td>
                        )}
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            )}
          </div>
        </section>
      </main>

      {showClaimForm && (
        <div className="dialog-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget) setShowClaimForm(false);
        }}>
          <section role="dialog" aria-modal="true" aria-labelledby="claim-dialog-title" className="claim-dialog">
            <div className="dialog-heading">
              <div>
                <span className="eyebrow">Expense operations</span>
                <h2 id="claim-dialog-title">New expense claim</h2>
              </div>
              <button type="button" aria-label="Close claim form" onClick={() => setShowClaimForm(false)} className="dialog-close"><X size={19} /></button>
            </div>
            <form onSubmit={handleClaimSubmit} className="claim-form">
              <label className="form-field form-field--full">
                <span>Expense title</span>
                <input
                  required
                  maxLength={150}
                  value={claim.title}
                  onChange={(event) => setClaim({ ...claim, title: event.target.value })}
                  placeholder="e.g. Client travel"
                />
              </label>
              <label className="form-field">
                <span>Category</span>
                <select required value={claim.categoryId} onChange={(event) => setClaim({ ...claim, categoryId: event.target.value })}>
                  <option value="" disabled>Select category</option>
                  {categories.map((category) => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}
                </select>
              </label>
              <label className="form-field">
                <span>Department</span>
                <select required value={claim.departmentId} onChange={(event) => setClaim({ ...claim, departmentId: event.target.value })}>
                  <option value="" disabled>{departments.length === 0 ? 'No departments available' : 'Select department'}</option>
                  {departments.map((department) => <option key={department.departmentId} value={department.departmentId}>{department.name}</option>)}
                </select>
              </label>
              <label className="form-field">
                <span>Amount (R)</span>
                <input
                  required
                  min="0.01"
                  step="0.01"
                  type="number"
                  value={claim.amount}
                  onChange={(event) => setClaim({ ...claim, amount: event.target.value })}
                  placeholder="0.00"
                />
              </label>
              <label className="form-field">
                <span>Expense date</span>
                <input
                  required
                  type="date"
                  value={claim.expenseDate}
                  onChange={(event) => setClaim({ ...claim, expenseDate: event.target.value })}
                />
              </label>
              {formError && <p role="alert" className="form-error">{formError}</p>}
              <div className="dialog-actions">
                <button type="button" onClick={() => setShowClaimForm(false)} className="secondary-action">Cancel</button>
                <button type="submit" disabled={submitting || categories.length === 0 || departments.length === 0} className="primary-action">
                  {submitting ? 'Submitting...' : 'Submit claim'}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  );
};