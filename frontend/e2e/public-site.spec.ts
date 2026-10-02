import { expect, test } from '@playwright/test';

test('public landing page loads the HimLedger experience', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle(/HimLedger/i);
  await expect(page.getByRole('link', { name: /sign in/i })).toBeVisible();
});
