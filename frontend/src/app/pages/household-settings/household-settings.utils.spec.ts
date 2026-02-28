import { describe, expect, it } from 'vitest';
import { buildHouseholdInviteLink, getApiErrorMessage } from './household-settings.utils';

describe('buildHouseholdInviteLink', () => {
  it('builds setup invite link', () => {
    expect(buildHouseholdInviteLink('ABC123', 'https://app.example.com'))
      .toBe('https://app.example.com/household/setup?invite=ABC123');
  });

  it('returns api error string payload when present', () => {
    const value = getApiErrorMessage({ error: { error: 'Invite code expired' } }, 'Fallback');
    expect(value).toBe('Invite code expired');
  });

  it('returns api error title when object has title only', () => {
    const value = getApiErrorMessage({ error: { title: 'Forbidden' } }, 'Fallback');
    expect(value).toBe('Forbidden');
  });

  it('falls back when payload has no useful message', () => {
    const value = getApiErrorMessage({ error: { status: 400 } }, 'Fallback');
    expect(value).toBe('Fallback');
  });
});
