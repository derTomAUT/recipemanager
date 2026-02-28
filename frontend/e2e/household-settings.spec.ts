import { expect, test } from '@playwright/test';

const validJwt = [
  'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0',
  'eyJleHAiOjQxMDI0NDQ4MDB9',
  'signature'
].join('.');

async function authenticate(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.evaluate(([token]) => {
    localStorage.setItem('auth_token', token);
    localStorage.setItem('user', JSON.stringify({
      id: 'owner-1',
      email: 'owner@test.local',
      name: 'Owner',
      householdId: 'house-1',
      role: 'Owner'
    }));
  }, [validJwt]);
}

test('renders household settings page shell', async ({ page }) => {
  await authenticate(page);
  await page.goto('/household/settings');

  await expect(page.getByRole('heading', { name: 'Household Settings' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Invite' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Members' })).toBeVisible();
});

test('shows member-management owner guidance text', async ({ page }) => {
  await authenticate(page);
  await page.goto('/household/settings');

  await expect(
    page.getByText('Owners can disable members. To disable an owner, promote another active member to Owner first.')
  ).toBeVisible();
});
