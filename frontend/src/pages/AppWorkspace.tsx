import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import {
  ArrowDownToLine, BadgeCheck, Bell, Building2, CalendarDays,
  Check, ChevronDown, CircleDollarSign, Clock3, FileText, LayoutDashboard,
  LogOut, Menu, Plus, Receipt, Search, ShieldCheck, WalletCards, X,
} from 'lucide-react';
import api from '../api/axios';
import { useAuth } from '../context/AuthContext';

interface Expense {
  expenseId: number;
  userId: number;
  userFullName: string;
  categoryId: number;
  categoryName: string;
  departmentId: number;
  departmentName: string;
  title: string;
  description: string | null;
  amount: number;
  expenseDate: string;
  receiptUrl: string | null;
  status: string;
  createdAt: string;
}

interface Category { categoryId: number; name: string }
interface Department { departmentId: number; name: string; code: string }
interface ManagedUser {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  roleId: number;
  roleName: string;
  departmentName: string | null;
}
interface UserRole { roleId: number; name: string }
interface Budget {
  budgetId: number;
  departmentId: number;
  departmentName: string;
  fiscalYear: number;
  fiscalQuarter: number;
  allocatedAmount: number;
  spentAmount: number;
  remainingAmount: number;
}
interface AuditRecord {
  approvalLogId: number;
  timestampUtc: string;
  expenseId: number;
  expenseTitle: string;
  action: string;
  reviewedBy: string;
  comments: string | null;
}

const money = (value: number) => `R ${value.toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const dateLabel = (value: string) => new Date(value).toLocaleDateString('en-ZA', { day: '2-digit', month: 'short', year: 'numeric' });
const currentQuarter = Math.floor(new Date().getMonth() / 3) + 1;
const currentYear = new Date().getFullYear();

export const AppWorkspace = () => {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [managedUsers, setManagedUsers] = useState<ManagedUser[]>([]);
  const [roles, setRoles] = useState<UserRole[]>([]);
  const [roleChanges, setRoleChanges] = useState<Record<number, number>>({});
  const [budgets, setBudgets] = useState<Budget[]>([]);
  const [auditRecords, setAuditRecords] = useState<AuditRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState('');
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('All statuses');
  const [departmentFilter, setDepartmentFilter] = useState('All departments');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [year, setYear] = useState(currentYear);
  const [quarter, setQuarter] = useState(currentQuarter);
  const [showClaimForm, setShowClaimForm] = useState(false);
  const [showBudgetForm, setShowBudgetForm] = useState(false);
  const [showDepartmentForm, setShowDepartmentForm] = useState(false);
  const [editingDepartment, setEditingDepartment] = useState<Department | null>(null);
  const [reviewTarget, setReviewTarget] = useState<Expense | null>(null);
  const [reviewAction, setReviewAction] = useState<'Approved' | 'Rejected'>('Approved');
  const [reviewComment, setReviewComment] = useState('');
  const [formError, setFormError] = useState('');
  const [busy, setBusy] = useState(false);
  const [claim, setClaim] = useState({ title: '', categoryId: '', departmentId: '', amount: '', expenseDate: new Date().toISOString().slice(0, 10), description: '' });
  const [allocation, setAllocation] = useState({ departmentId: '', amount: '' });
  const [departmentForm, setDepartmentForm] = useState({ name: '', code: '' });

  const role = user?.role ?? 'Employee';
  const isAdmin = role === 'Admin';
  const isManager = role === 'Manager';
  const canReview = isAdmin || isManager;
  const routeBase = location.pathname.startsWith('/workspace') ? '/workspace' : '/app';
  const activePage = location.pathname.replace(/\/$/, '').split('/').pop() ?? 'dashboard';
  const normalizedPage = activePage === 'overview' ? 'dashboard' : activePage;
  const isClaims = normalizedPage === 'claims';
  const isBudgets = normalizedPage === 'budgets';
  const isAudit = normalizedPage === 'audit-logs';
  const isDepartments = normalizedPage === 'departments';
  const isUsers = normalizedPage === 'users' || normalizedPage === 'user-roles';

  useEffect(() => {
    const loadCoreData = async () => {
      try {
        const [expenseResponse, categoryResponse, departmentResponse] = await Promise.all([
          api.get<Expense[]>('/Expenses'),
          api.get<Category[]>('/Categories'),
          api.get<Department[]>('/Departments'),
        ]);
        setExpenses(expenseResponse.data);
        setCategories(categoryResponse.data);
        setDepartments(departmentResponse.data);
      } catch (error) {
        console.error('Failed to load workspace data', error);
        setPageError('Workspace data could not be loaded. Refresh and try again.');
      } finally {
        setLoading(false);
      }
    };
    void loadCoreData();
  }, []);

  useEffect(() => {
    if (!isBudgets && activePage !== 'dashboard') return;
    api.get<Budget[]>('/Budgets', { params: { fiscalYear: year, fiscalQuarter: quarter } })
      .then(({ data }) => setBudgets(data))
      .catch((error: unknown) => {
        console.error('Failed to load budgets', error);
        setPageError('Budget data could not be loaded for this period.');
      });
  }, [activePage, isBudgets, quarter, year]);

  useEffect(() => {
    if (!isAudit) return;
    api.get<AuditRecord[]>('/AuditLogs')
      .then(({ data }) => setAuditRecords(data))
      .catch((error: unknown) => {
        console.error('Failed to load audit records', error);
        setPageError('Audit records could not be loaded.');
      });
  }, [isAudit]);

  useEffect(() => {
    if (!isUsers) return;
    api.get<{ users: ManagedUser[]; roles: UserRole[] }>('/Users')
      .then(({ data }) => { setManagedUsers(data.users); setRoles(data.roles); })
      .catch((error: unknown) => {
        console.error('Failed to load user roles', error);
        setPageError('User role data could not be loaded.');
      });
  }, [isUsers]);

  const visibleExpenses = useMemo(() => expenses.filter((expense) => {
    const matchesQuery = `${expense.title} ${expense.userFullName} ${expense.categoryName} ${expense.departmentName} ${expense.expenseId}`.toLowerCase().includes(query.toLowerCase());
    const matchesStatus = statusFilter === 'All statuses' || expense.status === statusFilter;
    const matchesDepartment = departmentFilter === 'All departments' || String(expense.departmentId) === departmentFilter;
    const expenseDate = expense.expenseDate.slice(0, 10);
    return matchesQuery && matchesStatus && matchesDepartment && (!fromDate || expenseDate >= fromDate) && (!toDate || expenseDate <= toDate);
  }), [departmentFilter, expenses, fromDate, query, statusFilter, toDate]);

  const totalExpenses = expenses.reduce((sum, item) => sum + item.amount, 0);
  const pendingCount = expenses.filter((item) => item.status === 'Pending').length;
  const yearApproved = expenses.filter((item) => item.status === 'Approved' && new Date(item.expenseDate).getFullYear() === currentYear).reduce((sum, item) => sum + item.amount, 0);
  const allocatedTotal = budgets.reduce((sum, budget) => sum + budget.allocatedAmount, 0);
  const remainingTotal = budgets.reduce((sum, budget) => sum + budget.remainingAmount, 0);
  const utilization = allocatedTotal > 0 ? Math.max(0, Math.min(((allocatedTotal - remainingTotal) / allocatedTotal) * 100, 100)) : 0;

  const navItems = [
    { to: `${routeBase}/dashboard`, label: 'Overview', icon: LayoutDashboard, visible: true },
    { to: `${routeBase}/claims`, label: role === 'Employee' ? 'My claims' : isManager ? 'Team claims' : 'Claims', icon: Receipt, visible: true },
    { to: `${routeBase}/budgets`, label: isManager ? 'Department budget' : 'Budgets', icon: WalletCards, visible: canReview },
    { to: `${routeBase}/audit-logs`, label: 'Audit logs', icon: ShieldCheck, visible: isAdmin },
    { to: `${routeBase}/departments`, label: 'Departments', icon: Building2, visible: isAdmin },
    { to: `${routeBase}/user-roles`, label: 'User roles', icon: ShieldCheck, visible: isAdmin },
  ].filter((item) => item.visible);

  const handleLogout = () => { logout(); navigate('/login'); };

  const handleClaimSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setFormError('');
    try {
      const { data } = await api.post<Expense>('/Expenses', {
        title: claim.title,
        categoryId: Number(claim.categoryId),
        departmentId: Number(claim.departmentId),
        amount: Number(claim.amount),
        expenseDate: `${claim.expenseDate}T00:00:00`,
        description: claim.description || null,
        receiptUrl: null,
      });
      setExpenses((current) => [data, ...current]);
      setShowClaimForm(false);
      setClaim({ title: '', categoryId: '', departmentId: '', amount: '', expenseDate: new Date().toISOString().slice(0, 10), description: '' });
    } catch (error) {
      console.error('Failed to submit claim', error);
      setFormError('The claim could not be submitted. Check the details and try again.');
    } finally { setBusy(false); }
  };

  const submitReview = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!reviewTarget) return;
    setBusy(true);
    setFormError('');
    try {
      await api.put(`/Expenses/${reviewTarget.expenseId}/status`, { status: reviewAction, comments: reviewComment.trim() || null });
      setExpenses((current) => current.map((item) => item.expenseId === reviewTarget.expenseId ? { ...item, status: reviewAction } : item));
      setReviewTarget(null);
      setReviewComment('');
    } catch (error) {
      console.error('Failed to update claim status', error);
      setFormError('The decision could not be saved. Try again.');
    } finally { setBusy(false); }
  };

  const saveAllocation = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setFormError('');
    try {
      await api.put('/Budgets', { departmentId: Number(allocation.departmentId), fiscalYear: year, fiscalQuarter: quarter, allocatedAmount: Number(allocation.amount) });
      const { data } = await api.get<Budget[]>('/Budgets', { params: { fiscalYear: year, fiscalQuarter: quarter } });
      setBudgets(data);
      setShowBudgetForm(false);
      setAllocation({ departmentId: '', amount: '' });
    } catch (error) {
      console.error('Failed to update budget allocation', error);
      setFormError('The allocation could not be saved. Check the amount and try again.');
    } finally { setBusy(false); }
  };

  const saveDepartment = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setFormError('');
    const payload = { name: departmentForm.name.trim(), code: departmentForm.code.trim().toUpperCase() };
    try {
      if (editingDepartment) {
        await api.put(`/Departments/${editingDepartment.departmentId}`, payload);
        setDepartments((current) => current.map((item) => item.departmentId === editingDepartment.departmentId ? { ...item, ...payload } : item));
      } else {
        const { data } = await api.post<Department>('/Departments', payload);
        setDepartments((current) => [...current, data].sort((first, second) => first.name.localeCompare(second.name)));
      }
      setShowDepartmentForm(false);
      setEditingDepartment(null);
      setDepartmentForm({ name: '', code: '' });
    } catch (error) {
      console.error('Failed to save department', error);
      setFormError('The department could not be saved. Check that its name and code are unique.');
    } finally { setBusy(false); }
  };

  const saveUserRole = async (managedUser: ManagedUser) => {
    const roleId = roleChanges[managedUser.userId] ?? managedUser.roleId;
    setBusy(true);
    setPageError('');
    try {
      await api.put(`/Users/${managedUser.userId}/role`, { roleId });
      const roleName = roles.find((item) => item.roleId === roleId)?.name ?? managedUser.roleName;
      setManagedUsers((current) => current.map((item) => item.userId === managedUser.userId ? { ...item, roleId, roleName } : item));
      setRoleChanges((current) => { const next = { ...current }; delete next[managedUser.userId]; return next; });
    } catch (error) {
      console.error('Failed to update user role', error);
      setPageError('The role change was rejected. Your own role and the last Admin account are protected.');
    } finally { setBusy(false); }
  };

  const downloadAuditCsv = () => {
    const fields = ['Timestamp UTC', 'Claim ID', 'Expense Title', 'Action', 'Reviewed By', 'Comments'];
    const rows = auditRecords.map((item) => [item.timestampUtc, item.expenseId, item.expenseTitle, item.action, item.reviewedBy, item.comments ?? '']);
    const csv = [fields, ...rows].map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `himledger-audit-${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  if (!['dashboard', 'overview', 'claims', 'budgets', 'audit-logs', 'departments', 'users', 'user-roles'].includes(normalizedPage)) return <Navigate to="/app/dashboard" replace />;
  if ((isBudgets && !canReview) || ((isAudit || isDepartments || isUsers) && !isAdmin)) return <Navigate to="/app/dashboard" replace />;

  const pageTitle = isClaims ? (role === 'Employee' ? 'My claims' : isManager ? 'Team claims' : 'Claims') : isBudgets ? 'Budget allocations' : isAudit ? 'Audit logs' : isDepartments ? 'Departments' : isUsers ? 'User roles' : 'Overview';
  const pageDescription = isClaims ? 'Track, submit and review expense claims.' : isBudgets ? 'Quarterly department allocations and spend.' : isAudit ? 'Immutable record of claim decisions.' : isDepartments ? 'Manage the departments used across claims and budgets.' : isUsers ? 'Assign workspace roles to registered accounts.' : 'A clear view of company spend and approvals.';

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <Link className="app-brand" to={`${routeBase}/dashboard`} aria-label="HimLedger overview"><span className="app-brand-mark">H</span><span>HimLedger</span></Link>
        <div className="app-workspace-tag"><span className="app-online-dot" /> Finance workspace</div>
        <nav className="app-nav" aria-label="Workspace navigation">
          <span className="app-nav-label">WORKSPACE</span>
          {navItems.map(({ to, label, icon: Icon }) => <Link key={to} to={to} onClick={() => document.querySelector('.app-sidebar')?.classList.remove('is-open')} className={`app-nav-link ${normalizedPage === to.split('/').pop() || (normalizedPage === 'users' && to.endsWith('/user-roles')) ? 'is-active' : ''}`}><Icon size={17} /><span>{label}</span></Link>)}
        </nav>
        <div className="app-sidebar-bottom"><div className="app-secure-note"><ShieldCheck size={16} /><span>Protected workspace</span></div><span>HimLedger · {new Date().getFullYear()}</span></div>
      </aside>

      <div className="app-main-column">
        <header className="app-topbar">
          <label className="app-search"><Search size={17} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search claims, people, departments" aria-label="Search workspace" /></label>
          <div className="app-top-actions">
            <button type="button" className="app-icon-button app-notifications" title={`${pendingCount} pending claims`} aria-label={`${pendingCount} pending claims`} onClick={() => navigate(`${routeBase}/claims`)}><Bell size={18} />{pendingCount > 0 && <span>{pendingCount > 9 ? '9+' : pendingCount}</span>}</button>
            <div className="app-profile"><span className="app-avatar">{`${user?.firstName?.[0] ?? 'U'}${user?.lastName?.[0] ?? ''}`}</span><span className="app-profile-copy"><strong>{user?.firstName} {user?.lastName}</strong><small>{role}</small></span><ChevronDown size={14} /></div>
            <button type="button" className="app-signout" onClick={handleLogout} title="Sign out"><LogOut size={16} /><span>Sign out</span></button>
            <button type="button" className="app-icon-button app-mobile-menu" aria-label="Open navigation" onClick={() => document.querySelector('.app-sidebar')?.classList.toggle('is-open')}><Menu size={19} /></button>
          </div>
        </header>

        <main className="app-content">
          <div className="app-page-heading"><div><div className="app-breadcrumb">HimLedger <span>/</span> Workspace <span>/</span> <strong>{pageTitle}</strong></div><h1>{pageTitle}</h1><p>{pageDescription}</p></div>
            {isDepartments && <button type="button" className="app-primary-button" onClick={() => { setEditingDepartment(null); setDepartmentForm({ name: '', code: '' }); setFormError(''); setShowDepartmentForm(true); }}><Plus size={17} /> Add department</button>}
            {(!isBudgets && !isAudit && !isDepartments && !isUsers) && <button type="button" className="app-primary-button" onClick={() => { setFormError(''); setShowClaimForm(true); }}><Plus size={17} /> Submit claim</button>}
            {isBudgets && isAdmin && <button type="button" className="app-primary-button" onClick={() => { setFormError(''); setShowBudgetForm(true); }}><Plus size={17} /> Adjust allocation</button>}
          </div>
          {pageError && <div className="app-error" role="alert">{pageError}<button type="button" onClick={() => setPageError('')} aria-label="Dismiss error"><X size={15} /></button></div>}

          {normalizedPage === 'dashboard' && <>
            <section className="app-kpis" aria-label="Financial overview">
              <article className="app-kpi app-kpi--featured"><span className="app-kpi-icon"><CircleDollarSign size={18} /></span><span className="app-kpi-label">Total expense volume</span><strong>{money(totalExpenses)}</strong><small>All recorded claims</small></article>
              <article className="app-kpi"><span className="app-kpi-icon app-kpi-icon--amber"><Clock3 size={18} /></span><span className="app-kpi-label">Pending decisions</span><strong>{pendingCount}</strong><small>Awaiting manager sign-off</small></article>
              <article className="app-kpi"><span className="app-kpi-icon app-kpi-icon--green"><BadgeCheck size={18} /></span><span className="app-kpi-label">Approved YTD</span><strong>{money(yearApproved)}</strong><small>Cleared in {currentYear}</small></article>
              <article className="app-kpi"><span className="app-kpi-icon app-kpi-icon--blue"><WalletCards size={18} /></span><span className="app-kpi-label">Budget remaining</span><strong>{money(remainingTotal)}</strong><small>{money(allocatedTotal)} allocated · {utilization.toFixed(0)}% utilized</small></article>
            </section>
            <div className="app-overview-grid">
              <section className="app-panel app-recent-panel"><div className="app-panel-heading"><div><span className="app-section-kicker">LATEST ACTIVITY</span><h2>Recent claims</h2></div><Link to={`${routeBase}/claims`} className="app-text-link">View all <ChevronDown size={14} /></Link></div>
                <div className="app-recent-list">{loading ? <div className="app-loading">Loading claims...</div> : expenses.slice(0, 6).map((expense) => <div className="app-recent-row" key={expense.expenseId}><span className="app-receipt-icon"><Receipt size={17} /></span><span className="app-recent-details"><strong>{expense.title}</strong><small>{expense.userFullName} · {expense.departmentName}</small></span><span className="app-recent-amount"><strong>{money(expense.amount)}</strong><small>{dateLabel(expense.expenseDate)}</small></span><span className={`app-status app-status--${expense.status.toLowerCase()}`}>{expense.status}</span></div>)}{!loading && expenses.length === 0 && <div className="app-empty">No claims have been submitted yet.</div>}</div>
              </section>
              <section className="app-panel app-budget-panel"><div className="app-panel-heading"><div><span className="app-section-kicker">FISCAL PERIOD</span><h2>Department budgets</h2></div><Link to={`${routeBase}/budgets`} className="app-icon-link" aria-label="View budgets"><ChevronDown size={17} /></Link></div>
                <div className="app-mini-budget-list">{budgets.slice(0, 5).map((budget) => <BudgetMeter key={budget.budgetId} budget={budget} compact />)}{budgets.length === 0 && <div className="app-empty">No budgets configured for this quarter.</div>}</div><div className="app-period-caption"><CalendarDays size={14} /> FY {year} · Q{quarter}</div>
              </section>
            </div>
          </>}

          {isClaims && <section className="app-panel app-claims-panel"><div className="app-filter-row"><label className="app-filter-select"><span>Status</span><select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option>All statuses</option><option>Pending</option><option>Approved</option><option>Rejected</option></select></label><label className="app-filter-select"><span>Department</span><select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}><option>All departments</option>{departments.map((department) => <option key={department.departmentId} value={department.departmentId}>{department.name}</option>)}</select></label><label className="app-filter-date"><span>From</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></label><label className="app-filter-date"><span>To</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></label><span className="app-results-count">{visibleExpenses.length} {visibleExpenses.length === 1 ? 'claim' : 'claims'}</span></div>
            <div className="app-table-scroll"><table className="app-table"><thead><tr><th>Claim ID</th><th>Employee</th><th>Expense</th><th>Category</th><th>Department</th><th>Date</th><th>Amount</th><th>Status</th><th>Actions</th></tr></thead><tbody>
              {loading ? <tr><td colSpan={9} className="app-table-message">Loading claims...</td></tr> : visibleExpenses.map((expense) => <tr key={expense.expenseId}><td className="app-id-cell">HL-{String(expense.expenseId).padStart(5, '0')}</td><td>{expense.userFullName}</td><td><span className="app-expense-title">{expense.title}</span>{expense.description && <small className="app-table-subtitle">{expense.description}</small>}</td><td>{expense.categoryName}</td><td>{expense.departmentName}</td><td>{dateLabel(expense.expenseDate)}</td><td className="app-money-cell">{money(expense.amount)}</td><td><span className={`app-status app-status--${expense.status.toLowerCase()}`}>{expense.status}</span></td><td><div className="app-row-actions">{expense.receiptUrl && <a className="app-icon-button" href={expense.receiptUrl} target="_blank" rel="noreferrer" title="View receipt"><FileText size={15} /></a>}{canReview && expense.status === 'Pending' && <><button type="button" className="app-icon-button app-approve" title="Approve" aria-label={`Approve ${expense.title}`} onClick={() => { setReviewTarget(expense); setReviewAction('Approved'); setFormError(''); }}><Check size={16} /></button><button type="button" className="app-icon-button app-reject" title="Reject" aria-label={`Reject ${expense.title}`} onClick={() => { setReviewTarget(expense); setReviewAction('Rejected'); setFormError(''); }}><X size={16} /></button></>}</div></td></tr>)}
              {!loading && visibleExpenses.length === 0 && <tr><td colSpan={9} className="app-table-message">No claims match these filters.</td></tr>}
            </tbody></table></div>
          </section>}

          {isBudgets && <><section className="app-budget-toolbar"><div><span className="app-section-kicker">PERIOD VIEW</span><p>Approved expenses are counted against each allocation.</p></div><div className="app-period-controls"><label><span>Fiscal year</span><select value={year} onChange={(event) => setYear(Number(event.target.value))}>{[currentYear - 1, currentYear, currentYear + 1].map((item) => <option key={item}>{item}</option>)}</select></label><div className="app-quarter-tabs" role="group" aria-label="Fiscal quarter">{[1, 2, 3, 4].map((item) => <button type="button" key={item} className={quarter === item ? 'is-selected' : ''} onClick={() => setQuarter(item)}>Q{item}</button>)}</div></div></section><section className="app-budget-grid" aria-label="Department budget allocations">{budgets.map((budget) => <BudgetMeter key={budget.budgetId} budget={budget} />)}{budgets.length === 0 && <div className="app-panel app-empty">No allocations exist for FY {year}, Q{quarter}.</div>}</section></>}

          {isAudit && <section className="app-panel app-audit-panel"><div className="app-audit-heading"><div><span className="app-section-kicker">READ-ONLY LEDGER</span><p>All timestamps are shown in UTC.</p></div><div className="app-export-actions"><button type="button" className="app-secondary-button" onClick={downloadAuditCsv}><ArrowDownToLine size={15} /> Export CSV</button><button type="button" className="app-secondary-button" onClick={() => window.print()}><FileText size={15} /> Print / PDF</button></div></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>Timestamp (UTC)</th><th>Claim ID</th><th>Action</th><th>Reviewed by</th><th>Manager comments</th></tr></thead><tbody>{auditRecords.map((record) => <tr key={record.approvalLogId}><td>{new Date(record.timestampUtc).toLocaleString('en-GB', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' })}</td><td><strong>HL-{String(record.expenseId).padStart(5, '0')}</strong><small className="app-table-subtitle">{record.expenseTitle}</small></td><td><span className={`app-status app-status--${record.action.toLowerCase()}`}>{record.action}</span></td><td>{record.reviewedBy}</td><td>{record.comments || <span className="app-muted">No comment recorded</span>}</td></tr>)}{auditRecords.length === 0 && <tr><td colSpan={5} className="app-table-message">No review actions have been recorded.</td></tr>}</tbody></table></div></section>}
          {isDepartments && <section className="app-panel app-admin-panel"><div className="app-admin-section-heading"><div><span className="app-section-kicker">ORGANIZATION</span><p>Departments are referenced by existing claims and budgets. Records cannot be deleted.</p></div><span className="app-results-count">{departments.length} departments</span></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>Department</th><th>Code</th><th>Actions</th></tr></thead><tbody>{departments.map((department) => <tr key={department.departmentId}><td><strong className="app-expense-title">{department.name}</strong></td><td><span className="app-department-code">{department.code}</span></td><td><button type="button" className="app-secondary-button app-edit-button" onClick={() => { setEditingDepartment(department); setDepartmentForm({ name: department.name, code: department.code }); setFormError(''); setShowDepartmentForm(true); }}>Edit department</button></td></tr>)}{departments.length === 0 && <tr><td colSpan={3} className="app-table-message">No departments configured.</td></tr>}</tbody></table></div></section>}

          {isUsers && <section className="app-panel app-admin-panel"><div className="app-admin-section-heading"><div><span className="app-section-kicker">ACCESS CONTROL</span><p>Role changes take effect on the user’s next authenticated request.</p></div><span className="app-results-count">{managedUsers.length} accounts</span></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>User</th><th>Department</th><th>Current role</th><th>Assign role</th><th>Action</th></tr></thead><tbody>{managedUsers.map((managedUser) => { const selectedRole = roleChanges[managedUser.userId] ?? managedUser.roleId; return <tr key={managedUser.userId}><td><strong className="app-expense-title">{managedUser.firstName} {managedUser.lastName}</strong><small className="app-table-subtitle">{managedUser.email}</small></td><td>{managedUser.departmentName ?? 'Unassigned'}</td><td>{managedUser.roleName}</td><td><select className="app-role-select" aria-label={`Role for ${managedUser.firstName} ${managedUser.lastName}`} value={selectedRole} onChange={(event) => setRoleChanges((current) => ({ ...current, [managedUser.userId]: Number(event.target.value) }))}>{roles.map((item) => <option key={item.roleId} value={item.roleId}>{item.name}</option>)}</select></td><td><button type="button" className="app-secondary-button app-edit-button" disabled={busy || selectedRole === managedUser.roleId || managedUser.userId === user?.userId} onClick={() => void saveUserRole(managedUser)}>{managedUser.userId === user?.userId ? 'Current account' : 'Save role'}</button></td></tr>})}{managedUsers.length === 0 && <tr><td colSpan={5} className="app-table-message">No user accounts found.</td></tr>}</tbody></table></div></section>}
        </main>
      </div>

      {showClaimForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowClaimForm(false); }}><section className="app-modal" role="dialog" aria-modal="true" aria-labelledby="new-claim-title"><div className="app-modal-heading"><div><span className="app-section-kicker">CLAIM DETAILS</span><h2 id="new-claim-title">Submit an expense</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowClaimForm(false)}><X size={18} /></button></div><form className="app-form-grid" onSubmit={handleClaimSubmit}><label className="app-form-field app-form-field--full"><span>Expense title</span><input required maxLength={150} value={claim.title} onChange={(event) => setClaim({ ...claim, title: event.target.value })} placeholder="e.g. Client travel" /></label><label className="app-form-field"><span>Category</span><select required value={claim.categoryId} onChange={(event) => setClaim({ ...claim, categoryId: event.target.value })}><option value="">Select category</option>{categories.map((item) => <option key={item.categoryId} value={item.categoryId}>{item.name}</option>)}</select></label><label className="app-form-field"><span>Department</span><select required value={claim.departmentId} onChange={(event) => setClaim({ ...claim, departmentId: event.target.value })}><option value="">Select department</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select></label><label className="app-form-field"><span>Amount (ZAR)</span><input required type="number" min="0.01" step="0.01" value={claim.amount} onChange={(event) => setClaim({ ...claim, amount: event.target.value })} placeholder="0.00" /></label><label className="app-form-field"><span>Expense date</span><input required type="date" value={claim.expenseDate} onChange={(event) => setClaim({ ...claim, expenseDate: event.target.value })} /></label><label className="app-form-field app-form-field--full"><span>Notes</span><textarea maxLength={500} rows={3} value={claim.description} onChange={(event) => setClaim({ ...claim, description: event.target.value })} placeholder="Add business purpose or context" /></label><p className="app-form-note app-form-field--full">Receipt attachment is not enabled in this workspace yet.</p>{formError && <p role="alert" className="app-form-error app-form-field--full">{formError}</p>}<div className="app-modal-actions app-form-field--full"><button type="button" className="app-secondary-button" onClick={() => setShowClaimForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Submitting...' : 'Submit claim'}</button></div></form></section></div>}

      {reviewTarget && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setReviewTarget(null); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="review-title"><div className="app-modal-heading"><div><span className="app-section-kicker">CLAIM HL-{String(reviewTarget.expenseId).padStart(5, '0')}</span><h2 id="review-title">{reviewAction} claim</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setReviewTarget(null)}><X size={18} /></button></div><p className="app-review-summary"><strong>{reviewTarget.title}</strong><span>{reviewTarget.userFullName} · {money(reviewTarget.amount)}</span></p><form onSubmit={submitReview} className="app-review-form"><label className="app-form-field"><span>Comment</span><textarea maxLength={500} rows={4} value={reviewComment} onChange={(event) => setReviewComment(event.target.value)} placeholder="Add context for this decision" /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setReviewTarget(null)}>Cancel</button><button type="submit" className={`app-primary-button ${reviewAction === 'Rejected' ? 'is-danger' : ''}`} disabled={busy}>{busy ? 'Saving...' : `Confirm ${reviewAction.toLowerCase()}`}</button></div></form></section></div>}

      {showBudgetForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowBudgetForm(false); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="allocation-title"><div className="app-modal-heading"><div><span className="app-section-kicker">FY {year} · Q{quarter}</span><h2 id="allocation-title">Adjust allocation</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowBudgetForm(false)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveAllocation}><label className="app-form-field"><span>Department</span><select required value={allocation.departmentId} onChange={(event) => setAllocation({ ...allocation, departmentId: event.target.value })}><option value="">Select department</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select></label><label className="app-form-field"><span>Allocated amount (ZAR)</span><input required type="number" min="0" step="0.01" value={allocation.amount} onChange={(event) => setAllocation({ ...allocation, amount: event.target.value })} placeholder="0.00" /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setShowBudgetForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Saving...' : 'Save allocation'}</button></div></form></section></div>}

      {showDepartmentForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowDepartmentForm(false); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="department-title"><div className="app-modal-heading"><div><span className="app-section-kicker">ORGANIZATION</span><h2 id="department-title">{editingDepartment ? 'Edit department' : 'Add department'}</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowDepartmentForm(false)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveDepartment}><label className="app-form-field"><span>Department name</span><input required maxLength={100} value={departmentForm.name} onChange={(event) => setDepartmentForm({ ...departmentForm, name: event.target.value })} /></label><label className="app-form-field"><span>Department code</span><input required maxLength={10} value={departmentForm.code} onChange={(event) => setDepartmentForm({ ...departmentForm, code: event.target.value.toUpperCase() })} /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setShowDepartmentForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Saving...' : editingDepartment ? 'Save changes' : 'Create department'}</button></div></form></section></div>}
    </div>
  );
};

const BudgetMeter = ({ budget, compact = false }: { budget: Budget; compact?: boolean }) => {
  const percent = budget.allocatedAmount > 0 ? Math.max(0, (budget.spentAmount / budget.allocatedAmount) * 100) : 0;
  const state = percent > 90 ? 'critical' : percent >= 75 ? 'warning' : 'healthy';
  return <article className={`app-budget-card ${compact ? 'is-compact' : ''}`}><div className="app-budget-card-top"><div className="app-budget-title"><span className="app-department-icon"><Building2 size={16} /></span><h3>{budget.departmentName}</h3></div><span className={`app-budget-percent is-${state}`}>{percent.toFixed(0)}%</span></div><div className={`app-budget-track is-${state}`} role="progressbar" aria-valuenow={Math.round(percent)} aria-valuemin={0} aria-valuemax={100} aria-label={`${budget.departmentName} budget used`}><span style={{ width: `${Math.min(percent, 100)}%` }} /></div><div className="app-budget-figures"><span><small>Allocated</small><strong>{money(budget.allocatedAmount)}</strong></span><span><small>Spent</small><strong>{money(budget.spentAmount)}</strong></span><span><small>Remaining</small><strong>{money(budget.remainingAmount)}</strong></span></div></article>;
};