import { expect, test } from '@playwright/test';

const validJwt = [
  'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0',
  'eyJleHAiOjQxMDI0NDQ4MDB9',
  'signature'
].join('.');

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([token]) => {
    localStorage.setItem('auth_token', token);
    localStorage.setItem('user', JSON.stringify({
      id: 'owner-1',
      email: 'owner@test.local',
      name: 'Owner',
      householdId: 'house-1',
      role: 'Owner'
    }));
  }, [validJwt]);
});

test('shows invite metadata and supports regeneration', async ({ page }) => {
  await page.route('**/api/household/settings', async route => {
    await route.fulfill({ status: 200, json: { hasApiKey: false, aiProvider: '', aiModel: '' } });
  });
  await page.route('**/api/household/me', async route => {
    await route.fulfill({
      status: 200,
      json: {
        id: 'house-1',
        name: 'Home',
        inviteCode: 'ABC12345',
        members: [
          { id: 'owner-1', name: 'Owner', email: 'owner@test.local', role: 'Owner', isActive: true },
          { id: 'member-1', name: 'Member', email: 'member@test.local', role: 'Member', isActive: true }
        ]
      }
    });
  });
  await page.route('**/api/household/invite', async route => {
    await route.fulfill({
      status: 200,
      json: {
        inviteCode: 'ABC12345',
        createdAtUtc: '2026-02-20T10:00:00Z',
        expiresAtUtc: '2026-02-25T10:00:00Z',
        isExpired: true
      }
    });
  });
  await page.route('**/api/household/invite/regenerate', async route => {
    await route.fulfill({
      status: 200,
      json: {
        inviteCode: 'NEWCODE1',
        createdAtUtc: '2026-02-28T10:00:00Z',
        expiresAtUtc: '2026-03-05T10:00:00Z',
        isExpired: false
      }
    });
  });
  await page.route('**/api/household/activity', async route => {
    await route.fulfill({
      status: 200,
      json: [{ id: 'a1', eventType: 'HouseholdCreated', createdAtUtc: '2026-02-20T10:00:00Z' }]
    });
  });

  await page.goto('/household/settings');

  await expect(page.getByRole('heading', { name: 'Household Settings' })).toBeVisible();
  await expect(page.getByText('Expired')).toBeVisible();

  await page.getByRole('button', { name: 'Regenerate Link' }).click();
  await expect(page.locator('input[readonly]')).toHaveValue(/NEWCODE1/);
  await expect(page.getByText('Invite link regenerated.')).toBeVisible();
});

test('shows backend instruction when disabling last active owner is blocked', async ({ page }) => {
  await page.route('**/api/household/settings', async route => {
    await route.fulfill({ status: 200, json: { hasApiKey: false, aiProvider: '', aiModel: '' } });
  });
  await page.route('**/api/household/me', async route => {
    await route.fulfill({
      status: 200,
      json: {
        id: 'house-1',
        name: 'Home',
        inviteCode: 'ABC12345',
        members: [{ id: 'owner-1', name: 'Owner', email: 'owner@test.local', role: 'Owner', isActive: true }]
      }
    });
  });
  await page.route('**/api/household/invite', async route => {
    await route.fulfill({
      status: 200,
      json: {
        inviteCode: 'ABC12345',
        createdAtUtc: '2026-02-20T10:00:00Z',
        expiresAtUtc: '2026-02-25T10:00:00Z',
        isExpired: true
      }
    });
  });
  await page.route('**/api/household/activity', async route => {
    await route.fulfill({ status: 200, json: [] });
  });
  await page.route('**/api/household/members/owner-1/disable', async route => {
    await route.fulfill({
      status: 400,
      json: { error: 'Cannot disable the last active owner. Promote another member to Owner first.' }
    });
  });

  await page.goto('/household/settings');
  await page.getByRole('button', { name: 'Disable' }).click();

  await expect(
    page.getByText('Cannot disable the last active owner. Promote another member to Owner first.')
  ).toBeVisible();
});
