import { describe, expect, it } from 'vitest';
import { buildHouseholdInviteLink } from './household-settings.utils';

describe('buildHouseholdInviteLink', () => {
  it('builds setup invite link', () => {
    expect(buildHouseholdInviteLink('ABC123', 'https://app.example.com'))
      .toBe('https://app.example.com/household/setup?invite=ABC123');
  });
});
