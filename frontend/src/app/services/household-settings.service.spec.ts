import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { HouseholdSettingsService } from './household-settings.service';

describe('HouseholdSettingsService', () => {
  it('calls disable and enable member endpoints', () => {
    const post = vi.fn().mockReturnValue(of(void 0));
    const http = { post, get: vi.fn(), put: vi.fn() } as any;
    const service = new HouseholdSettingsService(http);

    service.disableMember('u1').subscribe();
    service.enableMember('u1').subscribe();

    expect(post).toHaveBeenNthCalledWith(1, 'http://localhost:5000/api/household/members/u1/disable', {});
    expect(post).toHaveBeenNthCalledWith(2, 'http://localhost:5000/api/household/members/u1/enable', {});
  });
});
