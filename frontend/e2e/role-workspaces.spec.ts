import { expect, test, type Page } from '@playwright/test';

interface ExpenseFixture {
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
  rowVersion: string;
}

interface WorkspaceMockData {
  expenses?: ExpenseFixture[];
  categories?: { categoryId: number; name: string }[];
  departments?: { departmentId: number; name: string; code: string }[];
  roles?: { roleId: number; name: string }[];
}

const mockWorkspaceApi = async (page: Page, data: WorkspaceMockData = {}) => {
  await page.route('http://localhost:5228/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (method === 'POST' && path === '/api/Expenses') {
      await route.fulfill({
        status: 201,
        json: {
          expenseId: 501,
          userId: 4,
          userFullName: 'Test User',
          categoryId: 1,
          categoryName: 'Travel',
          departmentId: 1,
          departmentName: 'North',
          title: 'Client travel',
          description: 'Customer meeting',
          amount: 42,
          expenseDate: '2026-01-15T00:00:00',
          receiptUrl: null,
          status: 'Pending',
          createdAt: '2026-01-15T10:00:00Z',
          rowVersion: 'AQIDBAUGBwg=',
        } satisfies ExpenseFixture,
      });
      return;
    }
    if (method === 'PUT' && /\/api\/Expenses\/\d+\/status$/.test(path)) {
      await route.fulfill({ status: 204 });
      return;
    }
    if (method === 'POST' && path === '/api/Users') {
      await route.fulfill({ status: 201, json: { userId: 20 } });
      return;
    }
    if (path === '/api/Users/roles') {
      await route.fulfill({ json: data.roles ?? [] });
      return;
    }

    const items = path === '/api/Expenses'
      ? data.expenses ?? []
      : path === '/api/Categories'
        ? data.categories ?? []
        : path === '/api/Departments'
          ? data.departments ?? []
          : [];
    await route.fulfill({ json: { items, page: 1, pageSize: 100, totalCount: items.length } });
  });
};

const signInAs = async (page: Page, role: string) => {
  await page.addInitScript((userRole) => {
    localStorage.setItem('token', 'test-token');
    localStorage.setItem('user', JSON.stringify({
      userId: 4,
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.com',
      role: userRole,
    }));
  }, role);
};

test.describe('role workspaces', () => {
  const workspaces = [
    { role: 'Employee', path: '/employee/claims', heading: 'My claims', action: 'Submit claim', hiddenLink: 'Audit logs', hiddenAction: null },
    { role: 'Manager', path: '/manager/claims', heading: 'Team claims', action: 'Submit claim', hiddenLink: 'User roles', hiddenAction: null },
    { role: 'Finance', path: '/finance/audit-logs', heading: 'Audit logs', action: null, hiddenLink: null, hiddenAction: 'Submit claim' },
    { role: 'Admin', path: '/admin/user-roles', heading: 'User roles', action: 'Add user', hiddenLink: null, hiddenAction: null },
  ];

  for (const workspace of workspaces) {
    test(`${workspace.role} sees role-specific capabilities`, async ({ page }) => {
      await mockWorkspaceApi(page);
      await signInAs(page, workspace.role);
      await page.goto(workspace.path);

      await expect(page.getByRole('heading', { name: workspace.heading, level: 1 })).toBeVisible();
      if (workspace.action) {
        await expect(page.getByRole('button', { name: workspace.action })).toBeVisible();
      }
      if (workspace.hiddenLink) {
        await expect(page.getByRole('link', { name: workspace.hiddenLink })).toHaveCount(0);
      }
      if (workspace.hiddenAction) {
        await expect(page.getByRole('button', { name: workspace.hiddenAction })).toHaveCount(0);
      }
    });
  }

  test('redirects an employee away from admin-only user management', async ({ page }) => {
    await mockWorkspaceApi(page);
    await signInAs(page, 'Employee');
    await page.goto('/admin/user-roles');

    await expect(page).toHaveURL(/\/employee\/dashboard$/);
    await expect(page.getByRole('heading', { name: 'Overview', level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add user' })).toHaveCount(0);
  });

  test('employee submits a claim with validated details', async ({ page }) => {
    await mockWorkspaceApi(page, {
      categories: [{ categoryId: 1, name: 'Travel' }],
      departments: [{ departmentId: 1, name: 'North', code: 'N' }],
    });
    await signInAs(page, 'Employee');
    await page.goto('/employee/claims');
    await page.getByRole('button', { name: 'Submit claim' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Expense title').fill('Client travel');
    await dialog.getByLabel('Category').selectOption('1');
    await dialog.getByLabel('Department').selectOption('1');
    await dialog.getByLabel('Amount (ZAR)').fill('42.00');
    await dialog.getByLabel('Notes').fill('Customer meeting');

    const submitted = page.waitForRequest((request) =>
      request.method() === 'POST' && request.url().endsWith('/api/Expenses'));
    await dialog.getByRole('button', { name: 'Submit claim' }).click();
    const request = await submitted;

    expect(request.postDataJSON()).toMatchObject({
      title: 'Client travel',
      categoryId: 1,
      departmentId: 1,
      amount: 42,
      description: 'Customer meeting',
    });
    await expect(dialog).toHaveCount(0);
  });

  test('manager approves a pending department claim', async ({ page }) => {
    const expense: ExpenseFixture = {
      expenseId: 501,
      userId: 8,
      userFullName: 'Morgan Employee',
      categoryId: 1,
      categoryName: 'Travel',
      departmentId: 1,
      departmentName: 'North',
      title: 'Client travel',
      description: 'Customer meeting',
      amount: 42,
      expenseDate: '2026-01-15T00:00:00',
      receiptUrl: null,
      status: 'Pending',
      createdAt: '2026-01-15T10:00:00Z',
      rowVersion: 'AQIDBAUGBwg=',
    };
    await mockWorkspaceApi(page, { expenses: [expense] });
    await signInAs(page, 'Manager');
    await page.goto('/manager/claims');
    await page.getByRole('button', { name: 'Approve Client travel' }).click();

    const dialog = page.getByRole('dialog');
    const approval = page.waitForRequest((request) =>
      request.method() === 'PUT' && request.url().endsWith('/api/Expenses/501/status'));
    await dialog.getByRole('button', { name: 'Confirm approved' }).click();
    const request = await approval;

    expect(request.postDataJSON()).toMatchObject({
      status: 'Approved',
      rowVersion: expense.rowVersion,
    });
    await expect(dialog).toHaveCount(0);
  });

  test('finance exports the audit ledger', async ({ page }) => {
    await mockWorkspaceApi(page);
    await signInAs(page, 'Finance');
    await page.goto('/finance/audit-logs');

    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Export CSV' }).click();

    expect((await download).suggestedFilename()).toMatch(/^himledger-audit-.*\.csv$/);
  });

  test('admin provisions an employee with a department assignment', async ({ page }) => {
    await mockWorkspaceApi(page, {
      departments: [{ departmentId: 1, name: 'North', code: 'N' }],
      roles: [{ roleId: 3, name: 'Employee' }],
    });
    await signInAs(page, 'Admin');
    await page.goto('/admin/user-roles');
    await page.getByRole('button', { name: 'Add user' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('First name').fill('Casey');
    await dialog.getByLabel('Last name').fill('Employee');
    await dialog.getByLabel('Work email').fill('casey@example.com');
    await dialog.getByLabel('Role').selectOption('3');
    await dialog.getByLabel('Department').selectOption('1');

    const created = page.waitForRequest((request) =>
      request.method() === 'POST' && request.url().endsWith('/api/Users'));
    await dialog.getByRole('button', { name: 'Create account' }).click();
    const request = await created;

    expect(request.postDataJSON()).toMatchObject({
      firstName: 'Casey',
      lastName: 'Employee',
      email: 'casey@example.com',
      roleId: 3,
      departmentId: 1,
    });
    await expect(dialog).toHaveCount(0);
  });
});
