import { useMemo, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { isAxiosError } from 'axios';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import {
  ArrowDownToLine, BadgeCheck, Bell, Building2, CalendarDays,
  Check, ChevronDown, CircleDollarSign, Clock3, FileText, LayoutDashboard,
  LogOut, Menu, Plus, Receipt, Search, ShieldCheck, WalletCards, X,
} from 'lucide-react';
import api, { getAllPages } from '../api/axios';
import type { CreateExpenseDto, Expense as ApiExpense, UpdateBudgetRequest, UpdateExpenseStatusDto } from '../api/models';
import { useAuth } from '../context/auth';
import { BrandMark } from './PublicSite';

type Expense = ApiExpense;

interface Category { categoryId: number; name: string }
interface Department { departmentId: number; name: string; code: string }
interface ManagedUser {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  roleId: number;
  roleName: string;
  departmentId: number | null;
  departmentName: string | null;
  entraTenantId: string | null;
  entraObjectId: string | null;
  isActive: boolean;
}
interface UserRole { roleId: number; name: string }
interface WorkspaceNotification {
  userNotificationId: number;
  expenseId: number | null;
  title: string;
  message: string;
  createdAt: string;
  readAt: string | null;
}
interface NotificationList {
  unreadCount: number;
  notifications: WorkspaceNotification[];
}
interface Budget {
  budgetId: number;
  departmentId: number;
  departmentName: string;
  fiscalYear: number;
  fiscalQuarter: number;
  allocatedAmount: number;
  committedAmount: number;
  approvedAmount: number;
  reimbursedAmount: number;
  remainingAmount: number;
  rowVersion: string;
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
interface UserAccessAuditRecord {
  userAccessAuditLogId: number;
  occurredAt: string;
  user: string;
  email: string;
  actor: string;
  isActive: boolean;
}

const money = (value: number) => `R ${value.toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const dateLabel = (value: string) => new Date(value).toLocaleDateString('en-ZA', { day: '2-digit', month: 'short', year: 'numeric' });
const currentQuarter = Math.floor(new Date().getMonth() / 3) + 1;
const currentYear = new Date().getFullYear();
const emptyExpenses: Expense[] = [];
const emptyCategories: Category[] = [];
const emptyDepartments: Department[] = [];
const claimSchema = z.object({
  title: z.string().trim().min(1, 'Enter an expense title.').max(150),
  categoryId: z.string().min(1, 'Select a category.'),
  departmentId: z.string().min(1, 'Select a department.'),
  amount: z.string().regex(/^(?:\d+)(?:\.\d{1,2})?$/, 'Enter an amount with up to two decimal places.')
    .refine((value) => Number(value) > 0, 'Amount must be greater than zero.'),
  expenseDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Select a valid expense date.'),
  description: z.string().max(500, 'Notes cannot exceed 500 characters.'),
});
type ClaimFormValues = z.infer<typeof claimSchema>;
interface WorkspaceCore {
  expenses: Expense[];
  categories: Category[];
  departments: Department[];
}

export const AppWorkspace = () => {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [roleChanges, setRoleChanges] = useState<Record<number, number>>({});
  const [pageError, setPageError] = useState('');
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('All statuses');
  const [departmentFilter, setDepartmentFilter] = useState('All departments');
  const [categoryFilter, setCategoryFilter] = useState('All categories');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [year, setYear] = useState(currentYear);
  const [quarter, setQuarter] = useState(currentQuarter);
  const [showClaimForm, setShowClaimForm] = useState(false);
  const [showBudgetForm, setShowBudgetForm] = useState(false);
  const [showDepartmentForm, setShowDepartmentForm] = useState(false);
  const [showUserForm, setShowUserForm] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [editingDepartment, setEditingDepartment] = useState<Department | null>(null);
  const [identityTarget, setIdentityTarget] = useState<ManagedUser | null>(null);
  const [reviewTarget, setReviewTarget] = useState<Expense | null>(null);
  const [reviewAction, setReviewAction] = useState<'Approved' | 'Rejected' | 'Changes Requested'>('Approved');
  const [reviewComment, setReviewComment] = useState('');
  const [receiptFile, setReceiptFile] = useState<File | null>(null);
  const [formError, setFormError] = useState('');
  const [busy, setBusy] = useState(false);
  const claimForm = useForm<ClaimFormValues>({
    resolver: zodResolver(claimSchema),
    defaultValues: {
      title: '',
      categoryId: '',
      departmentId: '',
      amount: '',
      expenseDate: new Date().toISOString().slice(0, 10),
      description: '',
    },
  });
  const selectedClaimDepartment = useWatch({ control: claimForm.control, name: 'departmentId' });
  const selectedClaimDate = useWatch({ control: claimForm.control, name: 'expenseDate' });
  const selectedClaimAmount = useWatch({ control: claimForm.control, name: 'amount' });
  const [allocation, setAllocation] = useState({ departmentId: '', amount: '' });
  const [departmentForm, setDepartmentForm] = useState({ name: '', code: '' });
  const [managedUserForm, setManagedUserForm] = useState({ firstName: '', lastName: '', email: '', roleId: '', departmentId: '' });
  const [entraIdentityForm, setEntraIdentityForm] = useState({ tenantId: '', objectId: '' });
  const [departmentChanges, setDepartmentChanges] = useState<Record<number, string>>({});

  const role = user?.role ?? 'Employee';
  const isAdmin = role === 'Admin';
  const isManager = role === 'Manager';
  const isFinance = role === 'Finance';
  const canReview = isAdmin || isManager;
  const canSeeBudgets = canReview || isFinance;
  const canSeeAudit = isAdmin || isFinance;
  const routeBase = `/${location.pathname.split('/')[1]}`;
  const activePage = location.pathname.replace(/\/$/, '').split('/').pop() ?? 'dashboard';
  const normalizedPage = activePage === 'overview' ? 'dashboard' : activePage;
  const isClaims = normalizedPage === 'claims';
  const isBudgets = normalizedPage === 'budgets';
  const isAudit = normalizedPage === 'audit-logs';
  const isDepartments = normalizedPage === 'departments';
  const isUsers = normalizedPage === 'users' || normalizedPage === 'user-roles';
  const canSubmitClaim = role === 'Employee' || isManager;
  const coreKey = ['workspace-core', user?.userId] as const;
  const coreQuery = useQuery({
    queryKey: coreKey,
    queryFn: async (): Promise<WorkspaceCore> => {
      const [expenses, categories, departments] = await Promise.all([
        getAllPages<Expense>('/Expenses'),
        getAllPages<Category>('/Categories'),
        getAllPages<Department>('/Departments'),
      ]);
      return { expenses, categories, departments };
    },
  });
  const expenses = coreQuery.data?.expenses ?? emptyExpenses;
  const categories = coreQuery.data?.categories ?? emptyCategories;
  const departments = coreQuery.data?.departments ?? emptyDepartments;
  const updateExpenses = (update: (items: Expense[]) => Expense[]) => {
    queryClient.setQueryData<WorkspaceCore>(coreKey, (current) => current
      ? { ...current, expenses: update(current.expenses) }
      : current);
  };
  const budgetsQuery = useQuery({
    queryKey: ['budgets', user?.userId, year, quarter],
    queryFn: () => getAllPages<Budget>('/Budgets', { fiscalYear: year, fiscalQuarter: quarter }),
    enabled: isBudgets || activePage === 'dashboard',
  });
  const budgets = budgetsQuery.data ?? [];
  const reviewBudgetQuery = useQuery({
    queryKey: ['review-budget', user?.userId, reviewTarget?.expenseId],
    queryFn: async () => {
      if (!reviewTarget) throw new Error('A claim must be selected to load its budget.');
      const date = new Date(reviewTarget.expenseDate);
      return getAllPages<Budget>('/Budgets', {
        fiscalYear: date.getFullYear(),
        fiscalQuarter: Math.floor(date.getMonth() / 3) + 1,
      });
    },
    enabled: reviewTarget !== null,
  });
  const reviewBudget = reviewBudgetQuery.data?.find((item) => item.departmentId === reviewTarget?.departmentId);
  const claimBudgetQuery = useQuery({
    queryKey: ['claim-budget-preview', user?.userId, selectedClaimDepartment, selectedClaimDate],
    queryFn: async () => {
      if (!selectedClaimDepartment || !selectedClaimDate) {
        throw new Error('A department and expense date are required to check the budget.');
      }
      const date = new Date(`${selectedClaimDate}T00:00:00`);
      return getAllPages<Budget>('/Budgets', {
        fiscalYear: date.getFullYear(),
        fiscalQuarter: Math.floor(date.getMonth() / 3) + 1,
      });
    },
    enabled: showClaimForm && Boolean(selectedClaimDepartment) && Boolean(selectedClaimDate),
  });
  const claimBudget = claimBudgetQuery.data?.find((item) => item.departmentId === Number(selectedClaimDepartment));
  const notificationsQuery = useQuery({
    queryKey: ['notifications', user?.userId],
    queryFn: async () => (await api.get<NotificationList>('/Notifications')).data,
    refetchInterval: 30_000,
  });
  const notificationItems = notificationsQuery.data?.notifications ?? [];
  const unreadNotificationCount = notificationsQuery.data?.unreadCount ?? 0;
  const auditQuery = useQuery({
    queryKey: ['audit-records', user?.userId],
    queryFn: () => getAllPages<AuditRecord>('/AuditLogs'),
    enabled: isAudit && canSeeAudit,
  });
  const auditRecords = auditQuery.data ?? [];
  const userAccessAuditQuery = useQuery({
    queryKey: ['user-access-audit', user?.userId],
    queryFn: () => getAllPages<UserAccessAuditRecord>('/AuditLogs/user-access'),
    enabled: isAudit && isAdmin,
  });
  const userAccessAuditRecords = userAccessAuditQuery.data ?? [];
  const usersQuery = useQuery({
    queryKey: ['managed-users', user?.userId],
    queryFn: async () => {
      const [users, { data: roles }] = await Promise.all([
        getAllPages<ManagedUser>('/Users'),
        api.get<UserRole[]>('/Users/roles'),
      ]);
      return { users, roles };
    },
    enabled: isUsers && isAdmin,
  });
  const managedUsers = usersQuery.data?.users ?? [];
  const roles = usersQuery.data?.roles ?? [];
  const loading = coreQuery.isLoading;

  const queryErrorMessage = coreQuery.error
    ? 'Workspace data could not be loaded. Refresh and try again.'
    : budgetsQuery.error
      ? 'Budget data could not be loaded for this period.'
      : auditQuery.error
        ? 'Audit records could not be loaded.'
        : userAccessAuditQuery.error
          ? 'Account access history could not be loaded.'
        : usersQuery.error
          ? 'User role data could not be loaded.'
          : '';

  const visibleExpenses = useMemo(() => expenses.filter((expense) => {
    const matchesQuery = `${expense.title} ${expense.userFullName} ${expense.categoryName} ${expense.departmentName} ${expense.expenseId}`.toLowerCase().includes(query.toLowerCase());
    const matchesStatus = statusFilter === 'All statuses' || expense.status === statusFilter;
    const matchesDepartment = departmentFilter === 'All departments' || String(expense.departmentId) === departmentFilter;
    const matchesCategory = categoryFilter === 'All categories' || String(expense.categoryId) === categoryFilter;
    const expenseDate = expense.expenseDate.slice(0, 10);
    return matchesQuery && matchesStatus && matchesDepartment && matchesCategory && (!fromDate || expenseDate >= fromDate) && (!toDate || expenseDate <= toDate);
  }), [categoryFilter, departmentFilter, expenses, fromDate, query, statusFilter, toDate]);

  const totalExpenses = expenses.reduce((sum, item) => sum + item.amount, 0);
  const pendingCount = expenses.filter((item) => item.status === 'Pending Approval').length;
  const yearApproved = expenses.filter((item) => (item.status === 'Approved' || item.status === 'Reimbursed') && new Date(item.expenseDate).getFullYear() === currentYear).reduce((sum, item) => sum + item.amount, 0);
  const allocatedTotal = budgets.reduce((sum, budget) => sum + budget.allocatedAmount, 0);
  const remainingTotal = budgets.reduce((sum, budget) => sum + budget.remainingAmount, 0);
  const utilization = allocatedTotal > 0 ? Math.max(0, Math.min(((allocatedTotal - remainingTotal) / allocatedTotal) * 100, 100)) : 0;

  const navItems = [
    { to: `${routeBase}/dashboard`, label: 'Overview', icon: LayoutDashboard, visible: true },
    { to: `${routeBase}/claims`, label: role === 'Employee' ? 'My claims' : isManager ? 'Team claims' : 'Claims', icon: Receipt, visible: true },
    { to: `${routeBase}/budgets`, label: isManager ? 'Department budget' : 'Budgets', icon: WalletCards, visible: canSeeBudgets },
    { to: `${routeBase}/audit-logs`, label: 'Audit logs', icon: ShieldCheck, visible: canSeeAudit },
    { to: `${routeBase}/departments`, label: 'Departments', icon: Building2, visible: isAdmin },
    { to: `${routeBase}/user-roles`, label: 'User roles', icon: ShieldCheck, visible: isAdmin },
  ].filter((item) => item.visible);

  const handleLogout = () => { logout(); navigate('/login'); };

  const openNotification = async (notification: WorkspaceNotification) => {
    try {
      if (!notification.readAt) {
        await api.post(`/Notifications/${notification.userNotificationId}/read`);
        await queryClient.invalidateQueries({ queryKey: ['notifications', user?.userId] });
      }
      setShowNotifications(false);
      navigate(notification.expenseId
        ? `${routeBase}/claims#claim-${notification.expenseId}`
        : `${routeBase}/claims`);
    } catch (error) {
      console.error('Failed to open notification', error);
      setPageError('This notification could not be opened. Refresh and try again.');
    }
  };

  const markAllNotificationsRead = async () => {
    try {
      await api.post('/Notifications/read-all');
      await queryClient.invalidateQueries({ queryKey: ['notifications', user?.userId] });
    } catch (error) {
      console.error('Failed to mark notifications as read', error);
      setPageError('Notifications could not be marked as read.');
    }
  };

  const setUserActive = async (managedUser: ManagedUser) => {
    const nextActiveState = !managedUser.isActive;
    const action = nextActiveState ? 'reactivate' : 'deactivate';
    const explanation = nextActiveState
      ? `Reactivate access for ${managedUser.firstName} ${managedUser.lastName}?`
      : `Deactivate access for ${managedUser.firstName} ${managedUser.lastName}? Their existing claims and audit history will be preserved.`;
    if (!window.confirm(explanation)) return;

    setBusy(true);
    setPageError('');
    try {
      await api.put(`/Users/${managedUser.userId}/active`, { isActive: nextActiveState });
      await queryClient.invalidateQueries({ queryKey: ['managed-users', user?.userId] });
      await queryClient.invalidateQueries({ queryKey: ['user-access-audit', user?.userId] });
    } catch (error) {
      console.error(`Failed to ${action} user account`, error);
      const detail = isAxiosError<{ detail?: string }>(error) ? error.response?.data?.detail : undefined;
      setPageError(detail ?? `The account could not be ${action}d.`);
    } finally { setBusy(false); }
  };

  const handleClaimSubmit = async (values: ClaimFormValues) => {
    setBusy(true);
    setFormError('');
    try {
      const request = {
        title: values.title,
        categoryId: Number(values.categoryId),
        departmentId: Number(values.departmentId),
        amount: Number(values.amount),
        expenseDate: `${values.expenseDate}T00:00:00`,
        description: values.description || null,
      } satisfies CreateExpenseDto;
      const { data } = await api.post<Expense>('/Expenses', request);
      updateExpenses((current) => [data, ...current.filter((item) => item.expenseId !== data.expenseId)]);
      if (receiptFile) {
        const form = new FormData();
        form.append('file', receiptFile);
        await api.post(`/Expenses/${data.expenseId}/attachments`, form);
      }
      await api.post(`/Expenses/${data.expenseId}/submit`, { notes: null });
      const { data: submitted } = await api.get<Expense>(`/Expenses/${data.expenseId}`);
      updateExpenses((current) => current.map((item) => item.expenseId === data.expenseId ? submitted : item));
      setShowClaimForm(false);
      setReceiptFile(null);
      claimForm.reset();
    } catch (error) {
      console.error('Failed to submit claim', error);
      await queryClient.invalidateQueries({ queryKey: coreKey });
      setFormError('The claim could not be fully submitted. Any saved draft remains available to submit again.');
    } finally { setBusy(false); }
  };

  const submitExistingClaim = async (expense: Expense) => {
    setBusy(true);
    setPageError('');
    try {
      const action = expense.status === 'Changes Requested' ? 'resubmit' : 'submit';
      await api.post(`/Expenses/${expense.expenseId}/${action}`, { notes: null });
      const { data } = await api.get<Expense>(`/Expenses/${expense.expenseId}`);
      updateExpenses((current) => current.map((item) => item.expenseId === data.expenseId ? data : item));
    } catch (error) {
      console.error('Failed to submit claim draft', error);
      setPageError('The claim could not be submitted. Refresh and try again.');
    } finally { setBusy(false); }
  };

  const submitReview = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!reviewTarget) return;
    setBusy(true);
    setFormError('');
    try {
      const request: UpdateExpenseStatusDto = {
        status: reviewAction,
        comments: reviewComment.trim() || null,
        rowVersion: reviewTarget.rowVersion,
      };
      await api.put(`/Expenses/${reviewTarget.expenseId}/status`, request);
      await queryClient.invalidateQueries({ queryKey: coreKey });
      setReviewTarget(null);
      setReviewComment('');
    } catch (error) {
      console.error('Failed to update claim status', error);
      const detail = isAxiosError<{ detail?: string }>(error) ? error.response?.data?.detail : undefined;
      const responseText = isAxiosError(error) && typeof error.response?.data === 'string'
        ? error.response.data
        : undefined;
      setFormError(detail ?? responseText ?? 'The decision could not be saved. Refresh the claim and try again.');
    } finally { setBusy(false); }
  };

  const saveAllocation = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setFormError('');
    try {
      const existingBudget = budgets.find((item) => item.departmentId === Number(allocation.departmentId));
      const request: UpdateBudgetRequest = {
        departmentId: Number(allocation.departmentId),
        fiscalYear: year,
        fiscalQuarter: quarter,
        allocatedAmount: Number(allocation.amount),
        ...(existingBudget ? { rowVersion: existingBudget.rowVersion } : {}),
      };
      await api.put('/Budgets', request);
      await queryClient.invalidateQueries({ queryKey: ['budgets', user?.userId, year, quarter] });
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
      } else {
        await api.post<Department>('/Departments', payload);
      }
      await queryClient.invalidateQueries({ queryKey: coreKey });
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
      await queryClient.invalidateQueries({ queryKey: ['managed-users', user?.userId] });
      setRoleChanges((current) => { const next = { ...current }; delete next[managedUser.userId]; return next; });
    } catch (error) {
      console.error('Failed to update user role', error);
      setPageError('The role change was rejected. Your own role and the last Admin account are protected.');
    } finally { setBusy(false); }
  };

  const saveManagedUser = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setPageError('');
    try {
      await api.post('/Users', {
        firstName: managedUserForm.firstName.trim(),
        lastName: managedUserForm.lastName.trim(),
        email: managedUserForm.email.trim(),
        roleId: Number(managedUserForm.roleId),
        departmentId: managedUserForm.departmentId ? Number(managedUserForm.departmentId) : null,
      });
      await queryClient.invalidateQueries({ queryKey: ['managed-users', user?.userId] });
      setShowUserForm(false);
      setManagedUserForm({ firstName: '', lastName: '', email: '', roleId: '', departmentId: '' });
    } catch (error) {
      console.error('Failed to provision user', error);
      setPageError('The user account could not be created. Check the email, role, and department.');
    } finally { setBusy(false); }
  };

  const saveUserDepartment = async (managedUser: ManagedUser) => {
    const departmentValue = departmentChanges[managedUser.userId] ?? managedUser.departmentId?.toString() ?? '';
    setBusy(true);
    setPageError('');
    try {
      await api.put(`/Users/${managedUser.userId}/department`, {
        departmentId: departmentValue ? Number(departmentValue) : null,
      });
      await queryClient.invalidateQueries({ queryKey: ['managed-users', user?.userId] });
      setDepartmentChanges((current) => {
        const next = { ...current };
        delete next[managedUser.userId];
        return next;
      });
    } catch (error) {
      console.error('Failed to update user department', error);
      setPageError('The department assignment could not be saved.');
    } finally { setBusy(false); }
  };

  const saveEntraIdentity = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!identityTarget) return;
    setBusy(true);
    setPageError('');
    try {
      await api.put(`/Users/${identityTarget.userId}/entra-identity`, entraIdentityForm);
      await queryClient.invalidateQueries({ queryKey: ['managed-users', user?.userId] });
      setIdentityTarget(null);
      setEntraIdentityForm({ tenantId: '', objectId: '' });
    } catch (error) {
      console.error('Failed to link Entra identity', error);
      setPageError('The Entra identity could not be linked. Verify the tenant and object IDs are GUIDs and are not assigned elsewhere.');
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

  const downloadReconciliationCsv = async () => {
    setPageError('');
    try {
      const params = new URLSearchParams();
      if (fromDate) params.set('fromDate', fromDate);
      if (toDate) params.set('toDate', toDate);
      if (departmentFilter !== 'All departments') params.set('departmentId', departmentFilter);
      if (categoryFilter !== 'All categories') params.set('categoryId', categoryFilter);
      const { data } = await api.get<Blob>(`/Expenses/reconciliation.csv?${params.toString()}`, { responseType: 'blob' });
      const url = URL.createObjectURL(data);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `himledger-reconciliation-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Failed to export reconciliation CSV', error);
      setPageError('The reconciliation export could not be downloaded.');
    }
  };

  if (!['dashboard', 'overview', 'claims', 'budgets', 'audit-logs', 'departments', 'users', 'user-roles'].includes(normalizedPage)) return <Navigate to={`${routeBase}/dashboard`} replace />;
  if ((isBudgets && !canSeeBudgets) || (isAudit && !canSeeAudit) || ((isDepartments || isUsers) && !isAdmin)) return <Navigate to={`${routeBase}/dashboard`} replace />;

  const pageTitle = isClaims ? (role === 'Employee' ? 'My claims' : isManager ? 'Team claims' : 'Claims') : isBudgets ? 'Budget allocations' : isAudit ? 'Audit logs' : isDepartments ? 'Departments' : isUsers ? 'User roles' : 'Overview';
  const pageDescription = isClaims ? 'Track, submit and review expense claims.' : isBudgets ? 'Quarterly department allocations and spend.' : isAudit ? 'Immutable record of claim decisions and account access changes.' : isDepartments ? 'Manage the departments used across claims and budgets.' : isUsers ? 'Manage account roles, departments, and access.' : 'A clear view of company spend and approvals.';

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <Link className="app-brand" to={`${routeBase}/dashboard`} aria-label="HimLedger overview"><span className="app-brand-mark"><BrandMark size={25} /></span><span>HimLedger</span></Link>
        <div className="app-workspace-tag"><span className="app-online-dot" /> {role} workspace</div>
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
            <div className="app-notification-wrap">
              <button type="button" className="app-icon-button app-notifications" title={`${unreadNotificationCount} unread notifications`} aria-label={`${unreadNotificationCount} unread notifications`} aria-expanded={showNotifications} aria-controls="app-notification-menu" onClick={() => setShowNotifications((visible) => !visible)}><Bell size={18} />{unreadNotificationCount > 0 && <span>{unreadNotificationCount > 9 ? '9+' : unreadNotificationCount}</span>}</button>
              {showNotifications && <section className="app-notification-menu" id="app-notification-menu" aria-label="Notifications">
                <div className="app-notification-heading"><strong>Notifications</strong>{unreadNotificationCount > 0 && <button type="button" onClick={() => void markAllNotificationsRead()}>Mark all read</button>}</div>
                {notificationsQuery.isLoading ? <p className="app-notification-empty">Loading notifications...</p>
                  : notificationsQuery.error ? <p className="app-notification-empty" role="alert">Notifications could not be loaded.</p>
                    : notificationItems.length === 0 ? <p className="app-notification-empty">You’re all caught up.</p>
                      : <div className="app-notification-list">{notificationItems.map((notification) => <button type="button" key={notification.userNotificationId} className={`app-notification-item ${notification.readAt ? '' : 'is-unread'}`} onClick={() => void openNotification(notification)}>
                        <span className="app-notification-item-title">{notification.title}</span>
                        <span className="app-notification-item-message">{notification.message}</span>
                        <small>{dateLabel(notification.createdAt)}</small>
                      </button>)}</div>}
              </section>}
            </div>
            <div className="app-profile"><span className="app-avatar">{`${user?.firstName?.[0] ?? 'U'}${user?.lastName?.[0] ?? ''}`}</span><span className="app-profile-copy"><strong>{user?.firstName} {user?.lastName}</strong><small>{role}</small></span><ChevronDown size={14} /></div>
            <button type="button" className="app-signout" onClick={handleLogout} title="Sign out"><LogOut size={16} /><span>Sign out</span></button>
            <button type="button" className="app-icon-button app-mobile-menu" aria-label="Open navigation" onClick={() => document.querySelector('.app-sidebar')?.classList.toggle('is-open')}><Menu size={19} /></button>
          </div>
        </header>

        <main className="app-content">
          <div className="app-page-heading"><div><div className="app-breadcrumb">HimLedger <span>/</span> Workspace <span>/</span> <strong>{pageTitle}</strong></div><h1>{pageTitle}</h1><p>{pageDescription}</p></div>
            {isDepartments && <button type="button" className="app-primary-button" onClick={() => { setEditingDepartment(null); setDepartmentForm({ name: '', code: '' }); setFormError(''); setShowDepartmentForm(true); }}><Plus size={17} /> Add department</button>}
            {canSubmitClaim && !isBudgets && !isAudit && !isDepartments && !isUsers && <button type="button" className="app-primary-button" onClick={() => { claimForm.reset(); setReceiptFile(null); setFormError(''); setShowClaimForm(true); }}><Plus size={17} /> Submit claim</button>}
            {isBudgets && isAdmin && <button type="button" className="app-primary-button" onClick={() => { setFormError(''); setShowBudgetForm(true); }}><Plus size={17} /> Adjust allocation</button>}
            {isUsers && isAdmin && <button type="button" className="app-primary-button" onClick={() => { setManagedUserForm({ firstName: '', lastName: '', email: '', roleId: roles.find((item) => item.name === 'Employee')?.roleId.toString() ?? '', departmentId: '' }); setShowUserForm(true); }}><Plus size={17} /> Add user</button>}
          </div>
          {(pageError || queryErrorMessage) && <div className="app-error" role="alert">{pageError || queryErrorMessage}{pageError && <button type="button" onClick={() => setPageError('')} aria-label="Dismiss error"><X size={15} /></button>}</div>}

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

          {isClaims && <section className="app-panel app-claims-panel"><div className="app-filter-row"><label className="app-filter-select"><span>Status</span><select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option>All statuses</option>{['Draft', 'Submitted', 'Pending Approval', 'Approved', 'Rejected', 'Changes Requested', 'Resubmitted', 'Reimbursed'].map((status) => <option key={status}>{status}</option>)}</select></label><label className="app-filter-select"><span>Department</span><select value={departmentFilter} onChange={(event) => setDepartmentFilter(event.target.value)}><option>All departments</option>{departments.map((department) => <option key={department.departmentId} value={department.departmentId}>{department.name}</option>)}</select></label><label className="app-filter-select"><span>Category</span><select value={categoryFilter} onChange={(event) => setCategoryFilter(event.target.value)}><option>All categories</option>{categories.map((category) => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label><label className="app-filter-date"><span>From</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></label><label className="app-filter-date"><span>To</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></label><span className="app-results-count">{visibleExpenses.length} {visibleExpenses.length === 1 ? 'claim' : 'claims'}</span>{canSeeAudit && <button type="button" className="app-secondary-button" onClick={() => void downloadReconciliationCsv()}><ArrowDownToLine size={15} /> Export reconciliation</button>}</div>
            <div className="app-table-scroll"><table className="app-table"><thead><tr><th>Claim ID</th><th>Employee</th><th>Expense</th><th>Category</th><th>Department</th><th>Date</th><th>Amount</th><th>Status</th><th>Actions</th></tr></thead><tbody>
              {loading ? <tr><td colSpan={9} className="app-table-message">Loading claims...</td></tr> : visibleExpenses.map((expense) => <tr id={`claim-${expense.expenseId}`} key={expense.expenseId}><td className="app-id-cell">HL-{String(expense.expenseId).padStart(5, '0')}</td><td>{expense.userFullName}</td><td><span className="app-expense-title">{expense.title}</span>{expense.description && <small className="app-table-subtitle">{expense.description}</small>}</td><td>{expense.categoryName}</td><td>{expense.departmentName}</td><td>{dateLabel(expense.expenseDate)}</td><td className="app-money-cell">{money(expense.amount)}</td><td><span className={`app-status app-status--${expense.status.toLowerCase().replace(/\s+/g, '-')}`}>{expense.status}</span></td><td><div className="app-row-actions">{expense.receiptUrl && <a className="app-icon-button" href={expense.receiptUrl} target="_blank" rel="noreferrer" title="View receipt"><FileText size={15} /></a>}{expense.userId === user?.userId && (expense.status === 'Draft' || expense.status === 'Changes Requested') && <button type="button" className="app-icon-button" title="Submit claim" aria-label={`Submit ${expense.title}`} onClick={() => void submitExistingClaim(expense)}><ArrowDownToLine size={15} /></button>}{canReview && expense.status === 'Pending Approval' && <><button type="button" className="app-icon-button app-approve" title="Approve" aria-label={`Approve ${expense.title}`} onClick={() => { setReviewTarget(expense); setReviewAction('Approved'); setFormError(''); }}><Check size={16} /></button><button type="button" className="app-icon-button app-reject" title="Reject" aria-label={`Reject ${expense.title}`} onClick={() => { setReviewTarget(expense); setReviewAction('Rejected'); setFormError(''); }}><X size={16} /></button><button type="button" className="app-icon-button" title="Request changes" aria-label={`Request changes for ${expense.title}`} onClick={() => { setReviewTarget(expense); setReviewAction('Changes Requested'); setFormError(''); }}><FileText size={15} /></button></>}</div></td></tr>)}
              {!loading && visibleExpenses.length === 0 && <tr><td colSpan={9} className="app-table-message">No claims match these filters.</td></tr>}
            </tbody></table></div>
          </section>}

          {isBudgets && <><section className="app-budget-toolbar"><div><span className="app-section-kicker">PERIOD VIEW</span><p>Approved expenses are counted against each allocation.</p></div><div className="app-period-controls"><label><span>Fiscal year</span><select value={year} onChange={(event) => setYear(Number(event.target.value))}>{[currentYear - 1, currentYear, currentYear + 1].map((item) => <option key={item}>{item}</option>)}</select></label><div className="app-quarter-tabs" role="group" aria-label="Fiscal quarter">{[1, 2, 3, 4].map((item) => <button type="button" key={item} className={quarter === item ? 'is-selected' : ''} onClick={() => setQuarter(item)}>Q{item}</button>)}</div></div></section><section className="app-budget-grid" aria-label="Department budget allocations">{budgets.map((budget) => <BudgetMeter key={budget.budgetId} budget={budget} />)}{budgets.length === 0 && <div className="app-panel app-empty">No allocations exist for FY {year}, Q{quarter}.</div>}</section></>}

          {isAudit && <section className="app-panel app-audit-panel"><div className="app-audit-heading"><div><span className="app-section-kicker">READ-ONLY LEDGER</span><p>All timestamps are shown in UTC.</p></div><div className="app-export-actions"><button type="button" className="app-secondary-button" onClick={downloadAuditCsv}><ArrowDownToLine size={15} /> Export CSV</button><button type="button" className="app-secondary-button" onClick={() => window.print()}><FileText size={15} /> Print / PDF</button></div></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>Timestamp (UTC)</th><th>Claim ID</th><th>Action</th><th>Reviewed by</th><th>Manager comments</th></tr></thead><tbody>{auditRecords.map((record) => <tr key={record.approvalLogId}><td>{new Date(record.timestampUtc).toLocaleString('en-GB', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' })}</td><td><strong>HL-{String(record.expenseId).padStart(5, '0')}</strong><small className="app-table-subtitle">{record.expenseTitle}</small></td><td><span className={`app-status app-status--${record.action.toLowerCase()}`}>{record.action}</span></td><td>{record.reviewedBy}</td><td>{record.comments || <span className="app-muted">No comment recorded</span>}</td></tr>)}{auditRecords.length === 0 && <tr><td colSpan={5} className="app-table-message">No review actions have been recorded.</td></tr>}</tbody></table></div></section>}
          {isDepartments && <section className="app-panel app-admin-panel"><div className="app-admin-section-heading"><div><span className="app-section-kicker">ORGANIZATION</span><p>Departments are referenced by existing claims and budgets. Records cannot be deleted.</p></div><span className="app-results-count">{departments.length} departments</span></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>Department</th><th>Code</th><th>Actions</th></tr></thead><tbody>{departments.map((department) => <tr key={department.departmentId}><td><strong className="app-expense-title">{department.name}</strong></td><td><span className="app-department-code">{department.code}</span></td><td><button type="button" className="app-secondary-button app-edit-button" onClick={() => { setEditingDepartment(department); setDepartmentForm({ name: department.name, code: department.code }); setFormError(''); setShowDepartmentForm(true); }}>Edit department</button></td></tr>)}{departments.length === 0 && <tr><td colSpan={3} className="app-table-message">No departments configured.</td></tr>}</tbody></table></div></section>}

          {isUsers && <section className="app-panel app-admin-panel"><div className="app-admin-section-heading"><div><span className="app-section-kicker">ACCESS CONTROL</span><p>Deactivated accounts can no longer sign in. Existing claims and audit history are retained.</p></div><span className="app-results-count">{managedUsers.length} accounts</span></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>User</th><th>Department</th><th>Current role</th><th>Assign role</th><th>Entra identity</th><th>Actions</th><th>Account access</th></tr></thead><tbody>{managedUsers.map((managedUser) => { const selectedRole = roleChanges[managedUser.userId] ?? managedUser.roleId; const selectedDepartment = departmentChanges[managedUser.userId] ?? managedUser.departmentId?.toString() ?? ''; return <tr key={managedUser.userId}><td><strong className="app-expense-title">{managedUser.firstName} {managedUser.lastName}</strong><small className="app-table-subtitle">{managedUser.email}</small></td><td><select className="app-role-select" aria-label={`Department for ${managedUser.firstName} ${managedUser.lastName}`} value={selectedDepartment} onChange={(event) => setDepartmentChanges((current) => ({ ...current, [managedUser.userId]: event.target.value }))}><option value="">Unassigned</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select></td><td>{managedUser.roleName}</td><td><select className="app-role-select" aria-label={`Role for ${managedUser.firstName} ${managedUser.lastName}`} value={selectedRole} onChange={(event) => setRoleChanges((current) => ({ ...current, [managedUser.userId]: Number(event.target.value) }))}>{roles.map((item) => <option key={item.roleId} value={item.roleId}>{item.name}</option>)}</select></td><td>{managedUser.entraObjectId ? 'Linked' : 'Not linked'}</td><td><div className="app-row-actions"><button type="button" className="app-secondary-button app-edit-button" disabled={busy || selectedRole === managedUser.roleId || managedUser.userId === user?.userId} onClick={() => void saveUserRole(managedUser)}>{managedUser.userId === user?.userId ? 'Current account' : 'Save role'}</button><button type="button" className="app-secondary-button app-edit-button" disabled={busy || selectedDepartment === (managedUser.departmentId?.toString() ?? '')} onClick={() => void saveUserDepartment(managedUser)}>Save department</button><button type="button" className="app-secondary-button app-edit-button" disabled={busy} onClick={() => { setIdentityTarget(managedUser); setEntraIdentityForm({ tenantId: managedUser.entraTenantId ?? '', objectId: managedUser.entraObjectId ?? '' }); }}>Link Entra ID</button></div></td><td><span className={managedUser.isActive ? 'app-access-state is-active' : 'app-access-state is-inactive'}>{managedUser.isActive ? 'Active' : 'Deactivated'}</span><button type="button" className="app-secondary-button app-edit-button" disabled={busy || managedUser.userId === user?.userId} onClick={() => void setUserActive(managedUser)}>{managedUser.isActive ? 'Deactivate access' : 'Reactivate access'}</button></td></tr>})}{managedUsers.length === 0 && <tr><td colSpan={7} className="app-table-message">No user accounts found.</td></tr>}</tbody></table></div></section>}

          {isAudit && isAdmin && <section className="app-panel app-audit-panel app-access-audit-panel"><div className="app-audit-heading"><div><span className="app-section-kicker">ACCOUNT ACCESS</span><p>Admin account activation and deactivation history.</p></div></div><div className="app-table-scroll"><table className="app-table"><thead><tr><th>Timestamp (UTC)</th><th>Account</th><th>Change</th><th>Changed by</th></tr></thead><tbody>{userAccessAuditRecords.map((record) => <tr key={record.userAccessAuditLogId}><td>{new Date(record.occurredAt).toLocaleString('en-GB', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' })}</td><td><strong>{record.user}</strong><small className="app-table-subtitle">{record.email}</small></td><td>{record.isActive ? 'Access reactivated' : 'Access deactivated'}</td><td>{record.actor}</td></tr>)}{userAccessAuditRecords.length === 0 && <tr><td colSpan={4} className="app-table-message">No account access changes have been recorded.</td></tr>}</tbody></table></div></section>}
        </main>
      </div>

      {showClaimForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowClaimForm(false); }}>
        <section className="app-modal" role="dialog" aria-modal="true" aria-labelledby="new-claim-title">
          <div className="app-modal-heading"><div><span className="app-section-kicker">CLAIM DETAILS</span><h2 id="new-claim-title">Submit an expense</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowClaimForm(false)}><X size={18} /></button></div>
          <form className="app-form-grid" onSubmit={claimForm.handleSubmit(handleClaimSubmit)}>
            <label className="app-form-field app-form-field--full"><span>Expense title</span><input maxLength={150} {...claimForm.register('title')} placeholder="e.g. Client travel" />{claimForm.formState.errors.title && <small role="alert" className="app-form-error">{claimForm.formState.errors.title.message}</small>}</label>
            <label className="app-form-field"><span>Category</span><select {...claimForm.register('categoryId')}><option value="">Select category</option>{categories.map((item) => <option key={item.categoryId} value={item.categoryId}>{item.name}</option>)}</select>{claimForm.formState.errors.categoryId && <small role="alert" className="app-form-error">{claimForm.formState.errors.categoryId.message}</small>}</label>
            <label className="app-form-field"><span>Department</span><select {...claimForm.register('departmentId')}><option value="">Select department</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select>{claimForm.formState.errors.departmentId && <small role="alert" className="app-form-error">{claimForm.formState.errors.departmentId.message}</small>}</label>
            <label className="app-form-field"><span>Amount (ZAR)</span><input type="number" min="0.01" step="0.01" {...claimForm.register('amount')} placeholder="0.00" />{claimForm.formState.errors.amount && <small role="alert" className="app-form-error">{claimForm.formState.errors.amount.message}</small>}</label>
            <label className="app-form-field"><span>Expense date</span><input type="date" {...claimForm.register('expenseDate')} />{claimForm.formState.errors.expenseDate && <small role="alert" className="app-form-error">{claimForm.formState.errors.expenseDate.message}</small>}</label>
            <div className="app-claim-budget-preview app-form-field--full" aria-live="polite"><strong>Budget availability</strong>{claimBudgetQuery.isLoading ? <span>Checking this department’s allocation...</span> : claimBudgetQuery.error ? <span>Budget availability could not be checked. Your claim can still be submitted for review.</span> : !selectedClaimDepartment ? <span>Select a department to see its budget.</span> : claimBudget ? <><span>{money(claimBudget.remainingAmount)} currently remains{Number(selectedClaimAmount) > 0 ? `; approximately ${money(claimBudget.remainingAmount - Number(selectedClaimAmount))} would remain after this claim` : ''}. Approval depends on the available allocation.</span>{Number(selectedClaimAmount) > 0 && claimBudget.remainingAmount - Number(selectedClaimAmount) < 0 && <span className="app-form-error">This amount appears to exceed the remaining budget and may be blocked at approval.</span>}</> : <span>No budget is allocated for this department and quarter. Ask Finance or an administrator about allocation before submitting.</span>}</div>
            <label className="app-form-field app-form-field--full"><span>Notes</span><textarea rows={3} {...claimForm.register('description')} placeholder="Add business purpose or context" />{claimForm.formState.errors.description && <small role="alert" className="app-form-error">{claimForm.formState.errors.description.message}</small>}</label>
            <label className="app-form-field app-form-field--full"><span>Receipt (PDF, JPEG or PNG; max 10 MiB)</span><input type="file" accept="application/pdf,image/jpeg,image/png" onChange={(event) => setReceiptFile(event.target.files?.[0] ?? null)} />{receiptFile && <small>{receiptFile.name}</small>}</label>
            {formError && <p role="alert" className="app-form-error app-form-field--full">{formError}</p>}
            <div className="app-modal-actions app-form-field--full"><button type="button" className="app-secondary-button" onClick={() => setShowClaimForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Submitting...' : 'Submit claim'}</button></div>
          </form>
        </section>
      </div>}

      {reviewTarget && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setReviewTarget(null); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="review-title"><div className="app-modal-heading"><div><span className="app-section-kicker">CLAIM HL-{String(reviewTarget.expenseId).padStart(5, '0')}</span><h2 id="review-title">{reviewAction} claim</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setReviewTarget(null)}><X size={18} /></button></div><div className="app-review-summary"><strong>{reviewTarget.title}</strong><span>{reviewTarget.userFullName} · {money(reviewTarget.amount)}</span><span>{reviewTarget.categoryName} · {reviewTarget.departmentName} · {dateLabel(reviewTarget.expenseDate)}</span><p><b>Description</b>{reviewTarget.description?.trim() || 'No description was provided.'}</p></div><div className="app-review-budget" aria-live="polite"><strong>Budget check</strong>{reviewBudgetQuery.isLoading ? <span>Checking the allocation for this claim...</span> : reviewBudgetQuery.error ? <span>Budget details could not be loaded. The server will validate the budget when you approve.</span> : reviewBudget ? <span>Allocated: {money(reviewBudget.allocatedAmount)} · Remaining after this pending claim: {money(reviewBudget.remainingAmount)}</span> : <span>No budget is allocated for this department and fiscal quarter. Ask Finance or an administrator to allocate one before approving.</span>}</div><form onSubmit={submitReview} className="app-review-form"><label className="app-form-field"><span>Comment</span><textarea maxLength={500} rows={4} value={reviewComment} onChange={(event) => setReviewComment(event.target.value)} placeholder="Add context for this decision" /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setReviewTarget(null)}>Cancel</button><button type="submit" className={`app-primary-button ${reviewAction === 'Rejected' ? 'is-danger' : ''}`} disabled={busy}>{busy ? 'Saving...' : `Confirm ${reviewAction.toLowerCase()}`}</button></div></form></section></div>}

      {showBudgetForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowBudgetForm(false); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="allocation-title"><div className="app-modal-heading"><div><span className="app-section-kicker">FY {year} · Q{quarter}</span><h2 id="allocation-title">Adjust allocation</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowBudgetForm(false)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveAllocation}><label className="app-form-field"><span>Department</span><select required value={allocation.departmentId} onChange={(event) => setAllocation({ ...allocation, departmentId: event.target.value })}><option value="">Select department</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select></label><label className="app-form-field"><span>Allocated amount (ZAR)</span><input required type="number" min="0" step="0.01" value={allocation.amount} onChange={(event) => setAllocation({ ...allocation, amount: event.target.value })} placeholder="0.00" /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setShowBudgetForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Saving...' : 'Save allocation'}</button></div></form></section></div>}

      {showDepartmentForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowDepartmentForm(false); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="department-title"><div className="app-modal-heading"><div><span className="app-section-kicker">ORGANIZATION</span><h2 id="department-title">{editingDepartment ? 'Edit department' : 'Add department'}</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowDepartmentForm(false)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveDepartment}><label className="app-form-field"><span>Department name</span><input required maxLength={100} value={departmentForm.name} onChange={(event) => setDepartmentForm({ ...departmentForm, name: event.target.value })} /></label><label className="app-form-field"><span>Department code</span><input required maxLength={10} value={departmentForm.code} onChange={(event) => setDepartmentForm({ ...departmentForm, code: event.target.value.toUpperCase() })} /></label>{formError && <p role="alert" className="app-form-error">{formError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setShowDepartmentForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Saving...' : editingDepartment ? 'Save changes' : 'Create department'}</button></div></form></section></div>}

      {showUserForm && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowUserForm(false); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="managed-user-title"><div className="app-modal-heading"><div><span className="app-section-kicker">USER PROVISIONING</span><h2 id="managed-user-title">Add user account</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setShowUserForm(false)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveManagedUser}><label className="app-form-field"><span>First name</span><input required maxLength={50} value={managedUserForm.firstName} onChange={(event) => setManagedUserForm({ ...managedUserForm, firstName: event.target.value })} /></label><label className="app-form-field"><span>Last name</span><input required maxLength={50} value={managedUserForm.lastName} onChange={(event) => setManagedUserForm({ ...managedUserForm, lastName: event.target.value })} /></label><label className="app-form-field"><span>Work email</span><input required type="email" maxLength={100} value={managedUserForm.email} onChange={(event) => setManagedUserForm({ ...managedUserForm, email: event.target.value })} /></label><label className="app-form-field"><span>Role</span><select required value={managedUserForm.roleId} onChange={(event) => setManagedUserForm({ ...managedUserForm, roleId: event.target.value })}><option value="">Select role</option>{roles.map((item) => <option key={item.roleId} value={item.roleId}>{item.name}</option>)}</select></label><label className="app-form-field"><span>Department</span><select value={managedUserForm.departmentId} onChange={(event) => setManagedUserForm({ ...managedUserForm, departmentId: event.target.value })}><option value="">Unassigned</option>{departments.map((item) => <option key={item.departmentId} value={item.departmentId}>{item.name}</option>)}</select></label><p className="app-form-note">Employee and Manager accounts need a department. Link the Entra ID after creating the account.</p>{pageError && <p role="alert" className="app-form-error">{pageError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setShowUserForm(false)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Creating...' : 'Create account'}</button></div></form></section></div>}

      {identityTarget && <div className="app-modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setIdentityTarget(null); }}><section className="app-modal app-modal--small" role="dialog" aria-modal="true" aria-labelledby="entra-identity-title"><div className="app-modal-heading"><div><span className="app-section-kicker">{identityTarget.email}</span><h2 id="entra-identity-title">Link Entra identity</h2></div><button type="button" className="app-icon-button" aria-label="Close" onClick={() => setIdentityTarget(null)}><X size={18} /></button></div><form className="app-review-form" onSubmit={saveEntraIdentity}><label className="app-form-field"><span>Tenant ID</span><input required minLength={36} maxLength={36} value={entraIdentityForm.tenantId} onChange={(event) => setEntraIdentityForm({ ...entraIdentityForm, tenantId: event.target.value })} placeholder="00000000-0000-0000-0000-000000000000" /></label><label className="app-form-field"><span>Entra object ID</span><input required minLength={36} maxLength={36} value={entraIdentityForm.objectId} onChange={(event) => setEntraIdentityForm({ ...entraIdentityForm, objectId: event.target.value })} placeholder="00000000-0000-0000-0000-000000000000" /></label>{pageError && <p role="alert" className="app-form-error">{pageError}</p>}<div className="app-modal-actions"><button type="button" className="app-secondary-button" onClick={() => setIdentityTarget(null)}>Cancel</button><button type="submit" className="app-primary-button" disabled={busy}>{busy ? 'Linking...' : 'Save identity'}</button></div></form></section></div>}
    </div>
  );
};

const BudgetMeter = ({ budget, compact = false }: { budget: Budget; compact?: boolean }) => {
  const committedAndApproved = budget.committedAmount + budget.approvedAmount;
  const percent = budget.allocatedAmount > 0 ? Math.max(0, (committedAndApproved / budget.allocatedAmount) * 100) : 0;
  const state = percent > 90 ? 'critical' : percent >= 75 ? 'warning' : 'healthy';
  return <article className={`app-budget-card ${compact ? 'is-compact' : ''}`}><div className="app-budget-card-top"><div className="app-budget-title"><span className="app-department-icon"><Building2 size={16} /></span><h3>{budget.departmentName}</h3></div><span className={`app-budget-percent is-${state}`}>{percent.toFixed(0)}%</span></div><div className={`app-budget-track is-${state}`} role="progressbar" aria-valuenow={Math.round(percent)} aria-valuemin={0} aria-valuemax={100} aria-label={`${budget.departmentName} budget committed or approved`}><span style={{ width: `${Math.min(percent, 100)}%` }} /></div><div className="app-budget-figures"><span><small>Allocated</small><strong>{money(budget.allocatedAmount)}</strong></span><span><small>Committed / approved</small><strong>{money(committedAndApproved)}</strong></span><span><small>Remaining</small><strong>{money(budget.remainingAmount)}</strong></span></div></article>;
};