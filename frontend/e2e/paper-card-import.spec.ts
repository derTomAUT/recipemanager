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

test('paper card flow enables parse only after two images selected', async ({ page }) => {
  await authenticate(page);
  await page.goto('/import/paper-card');

  await expect(page.getByRole('heading', { name: 'Import from Paper Card' })).toBeVisible();
  const parseButton = page.getByRole('button', { name: 'Parse Paper Card' });
  await expect(parseButton).toBeDisabled();

  const frontInput = page.getByLabel('Front Side');
  const backInput = page.getByLabel('Back Side');
  await frontInput.setInputFiles({ name: 'front.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('front') });
  await expect(parseButton).toBeDisabled();

  await backInput.setInputFiles({ name: 'back.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('back') });
  await expect(parseButton).toBeEnabled();
});

test('paper card flow shows parse error without backend response', async ({ page }) => {
  await authenticate(page);
  await page.goto('/import/paper-card');

  const frontInput = page.getByLabel('Front Side');
  const backInput = page.getByLabel('Back Side');
  await frontInput.setInputFiles({ name: 'front.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('front') });
  await backInput.setInputFiles({ name: 'back.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('back') });

  await page.getByRole('button', { name: 'Parse Paper Card' }).click();
  await expect(page.getByText('Failed to parse paper card photos.')).toBeVisible();
});
